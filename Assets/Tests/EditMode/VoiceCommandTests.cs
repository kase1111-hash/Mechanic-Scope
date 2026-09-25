using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using MechanicScope.Core;
using MechanicScope.Voice;
using Object = UnityEngine.Object;

namespace MechanicScope.Tests
{
    /// <summary>
    /// Voice command handling: phrase matching, the native plugin message format, and the real
    /// VoiceCommandManager driving a real ProcedureRunner. Speech recognition itself is platform
    /// code, so a scripted recognizer stands in for it; everything after "text was recognized"
    /// is the production path.
    /// </summary>
    [TestFixture]
    public class VoiceCommandTests
    {
        private const string ProcedureJson = @"{
            ""id"": ""voice_test"",
            ""name"": ""Voice Test"",
            ""engineId"": ""test_engine"",
            ""steps"": [
                { ""id"": 1, ""action"": ""Step one"", ""tools"": [""10mm socket""] },
                { ""id"": 2, ""action"": ""Step two"", ""requires"": [1] },
                { ""id"": 3, ""action"": ""Step three"", ""requires"": [2] }
            ]
        }";

        private GameObject voiceGO;
        private GameObject runnerGO;
        private VoiceCommandManager manager;
        private ProcedureRunner runner;
        private ScriptedRecognizer recognizer;
        private List<string> recognized;

        [SetUp]
        public void SetUp()
        {
            runnerGO = new GameObject("VoiceTestRunner");
            runner = runnerGO.AddComponent<ProcedureRunner>();
            runner.LoadProcedureFromJson(ProcedureJson, "test_engine");

            voiceGO = new GameObject("VoiceCommands");
            manager = voiceGO.AddComponent<VoiceCommandManager>();
            recognizer = new ScriptedRecognizer();
            manager.UseRecognizer(recognizer);
            manager.SetReferences(runner, null);
            manager.SetVoiceFeedbackEnabled(false);

            recognized = new List<string>();
            manager.OnCommandRecognized += recognized.Add;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(voiceGO);
            Object.DestroyImmediate(runnerGO);
        }

        // === Matcher ===

        [Test]
        public void Normalize_IgnoresCasePunctuationAndSpacing()
        {
            Assert.AreEqual("next step", VoiceCommandMatcher<string>.Normalize("  Next   Step. "));
            Assert.AreEqual("whats this", VoiceCommandMatcher<string>.Normalize("What's this?"));
            Assert.AreEqual("read step", VoiceCommandMatcher<string>.Normalize("read-step!"));
            Assert.AreEqual(string.Empty, VoiceCommandMatcher<string>.Normalize(null));
        }

        [Test]
        public void Match_RequiresWholeWords()
        {
            var matcher = new VoiceCommandMatcher<string>();
            matcher.Add("lock", "LOCK");
            matcher.Add("stop", "STOP");

            Assert.IsNull(matcher.Match("unlock", out _), "'unlock' must not trigger 'lock'");
            Assert.IsNull(matcher.Match("start the stopwatch", out _), "'stopwatch' must not trigger 'stop'");
            Assert.AreEqual("LOCK", matcher.Match("Lock it.", out string phrase));
            Assert.AreEqual("lock", phrase);
        }

        [Test]
        public void Match_PrefersThePhraseWithMostWords()
        {
            var matcher = new VoiceCommandMatcher<string>();
            matcher.Add("next", "SKIP");
            matcher.Add("next step", "COMPLETE");

            Assert.AreEqual("COMPLETE", matcher.Match("okay next step please", out _));
            Assert.AreEqual("SKIP", matcher.Match("next", out _));
        }

        [Test]
        public void Match_NothingRecognized_ReturnsNull()
        {
            var matcher = new VoiceCommandMatcher<string>();
            matcher.Add("next step", "COMPLETE");

            Assert.IsNull(matcher.Match("hand me the wrench", out string phrase));
            Assert.IsNull(phrase);
            Assert.IsNull(matcher.Match("", out _));
            Assert.IsNull(matcher.Match(null, out _));
        }

        // === Native message format ===

        [Test]
        public void NativeResult_ParsesConfidenceAndText()
        {
            Assert.IsTrue(NativeSpeechMessage.TryParseResult("0.82|Next step", out string text, out float confidence));
            Assert.AreEqual("Next step", text);
            Assert.AreEqual(0.82f, confidence, 0.0001f);
        }

        [Test]
        public void NativeResult_MissingOrNegativeConfidence_IsTreatedAsUnknown()
        {
            Assert.IsTrue(NativeSpeechMessage.TryParseResult("-1|next step", out _, out float negative));
            Assert.AreEqual(NativeSpeechMessage.UnknownConfidence, negative);

            Assert.IsTrue(NativeSpeechMessage.TryParseResult("next step", out string text, out float none));
            Assert.AreEqual("next step", text);
            Assert.AreEqual(NativeSpeechMessage.UnknownConfidence, none);
        }

        [Test]
        public void NativeResult_EmptyText_IsRejected()
        {
            Assert.IsFalse(NativeSpeechMessage.TryParseResult("0.9|   ", out _, out _));
            Assert.IsFalse(NativeSpeechMessage.TryParseResult(null, out _, out _));
        }

        [Test]
        public void NativeError_MapsCodes()
        {
            NativeSpeechMessage.ParseError("permission|Denied by user", out VoiceErrorKind kind, out string detail);
            Assert.AreEqual(VoiceErrorKind.PermissionDenied, kind);
            Assert.AreEqual("Denied by user", detail);

            NativeSpeechMessage.ParseError("no_speech|Nothing heard", out kind, out _);
            Assert.AreEqual(VoiceErrorKind.NoSpeech, kind);

            NativeSpeechMessage.ParseError("something odd", out kind, out detail);
            Assert.AreEqual(VoiceErrorKind.Other, kind);
            Assert.AreEqual("something odd", detail);
        }

        // === Manager ===

        [Test]
        public void NextStep_CompletesTheActiveStep()
        {
            manager.StartListening();
            recognizer.Hear("Next step.");

            CollectionAssert.AreEqual(new[] { "next step" }, recognized);
            Assert.AreEqual(1, runner.CompletedSteps.Count);
            Assert.AreEqual(1, runner.CompletedSteps[0].id);
        }

        [Test]
        public void PushToTalk_HearsOneCommandThenStops()
        {
            manager.StartListening();
            Assert.IsFalse(recognizer.LastStartWasContinuous, "push-to-talk must start single-shot recognition");

            recognizer.Hear("next step");
            recognizer.Hear("next step");

            Assert.IsFalse(manager.IsListening);
            Assert.IsFalse(recognizer.IsListening);
            Assert.AreEqual(1, runner.CompletedSteps.Count, "the second result arrived after listening stopped");
        }

        [Test]
        public void AlwaysListening_KeepsActingOnCommands()
        {
            manager.SetActivationMode(VoiceCommandManager.ActivationMode.AlwaysListening);

            Assert.IsTrue(manager.IsListening);
            Assert.IsTrue(recognizer.LastStartWasContinuous);

            recognizer.Hear("next step");
            recognizer.Hear("next step");

            Assert.AreEqual(2, runner.CompletedSteps.Count);
            Assert.IsTrue(manager.IsListening);
        }

        [Test]
        public void Unlock_DoesNotTriggerLock()
        {
            manager.SetActivationMode(VoiceCommandManager.ActivationMode.AlwaysListening);

            recognizer.Hear("unlock");
            recognizer.Hear("please lock it");

            CollectionAssert.AreEqual(new[] { "unlock", "lock" }, recognized);
        }

        [Test]
        public void LowConfidenceResults_AreIgnored()
        {
            manager.SetActivationMode(VoiceCommandManager.ActivationMode.AlwaysListening);

            recognizer.Hear("next step", 0.2f);

            CollectionAssert.IsEmpty(recognized);
            Assert.AreEqual(0, runner.CompletedSteps.Count);
        }

        [Test]
        public void ResultsWhileNotListening_AreIgnored()
        {
            recognizer.Hear("next step");

            CollectionAssert.IsEmpty(recognized);
            Assert.AreEqual(0, runner.CompletedSteps.Count);
        }

        [Test]
        public void WakeWordMode_ActsOnlyAfterTheWakeWord()
        {
            manager.SetActivationMode(VoiceCommandManager.ActivationMode.WakeWord);
            manager.StartListening();

            recognizer.Hear("next step");
            Assert.AreEqual(0, runner.CompletedSteps.Count, "commands before the wake word are ignored");

            recognizer.Hear("Hey, Mechanic!");
            Assert.IsTrue(manager.IsWakeWordActive);

            recognizer.Hear("next step");
            Assert.AreEqual(1, runner.CompletedSteps.Count);
        }

        [Test]
        public void RecognizerEndingOnItsOwn_StopsTheManager()
        {
            manager.SetActivationMode(VoiceCommandManager.ActivationMode.AlwaysListening);
            bool stopped = false;
            manager.OnListeningStopped += () => stopped = true;

            recognizer.EndOnItsOwn();

            Assert.IsFalse(manager.IsListening);
            Assert.IsTrue(stopped);
        }

        [Test]
        public void UnavailableRecognizer_ReportsErrorAndDoesNotListen()
        {
            recognizer.Available = false;
            string error = null;
            manager.OnRecognitionError += e => error = e;

            manager.StartListening();

            Assert.IsFalse(manager.IsListening);
            Assert.IsNotNull(error);
        }

        [Test]
        public void RecognizerErrors_AreForwarded()
        {
            string error = null;
            manager.OnRecognitionError += e => error = e;
            manager.StartListening();

            recognizer.Fail(VoiceErrorKind.NoSpeech, "Nothing heard");

            Assert.AreEqual("Nothing heard", error);
        }

        [Test]
        public void SpeechHeardWhileTheAppIsTalking_IsIgnored()
        {
            // With spoken replies on, "next step" answers "Step completed". A microphone that picks
            // that up must not act on it, or the app could talk itself through a procedure.
            manager.SetVoiceFeedbackEnabled(true);
            manager.SetActivationMode(VoiceCommandManager.ActivationMode.AlwaysListening);

            recognizer.Hear("next step");
            Assert.IsTrue(manager.IsHearingOwnVoice);

            recognizer.Hear("next step");
            Assert.AreEqual(1, runner.CompletedSteps.Count);
        }

        [Test]
        public void EveryDefaultPhrase_ResolvesToItsOwnCommand()
        {
            // Guards against one command's phrase shadowing another's.
            foreach ((string phrases, string description) in manager.GetRegisteredCommands())
            {
                foreach (string phrase in phrases.Split(new[] { " / " }, StringSplitOptions.None))
                {
                    manager.SetActivationMode(VoiceCommandManager.ActivationMode.AlwaysListening);
                    var heard = new List<string>();
                    Action<string> capture = heard.Add;
                    manager.OnCommandRecognized += capture;

                    recognizer.Hear(phrase);

                    manager.OnCommandRecognized -= capture;
                    CollectionAssert.AreEqual(new[] { VoiceCommandMatcher<string>.Normalize(phrase) }, heard,
                        $"'{phrase}' ({description}) did not resolve to itself");
                }
            }
        }

        /// <summary>Stands in for a platform recognizer: tests say what it "heard".</summary>
        private class ScriptedRecognizer : IVoiceRecognizer
        {
            public event Action<string, float> OnResult;
            public event Action<string> OnPartialResult;
            public event Action<VoiceErrorKind, string> OnError;
            public event Action OnListeningEnded;

            public bool Available = true;
            public bool IsAvailable => Available;
            public bool IsListening { get; private set; }
            public bool LastStartWasContinuous { get; private set; }

            private bool continuous;

            public void Configure(string languageTag, bool allowCloudRecognition) { }

            public void StartListening(bool keepListening)
            {
                if (!Available) return;
                IsListening = true;
                continuous = keepListening;
                LastStartWasContinuous = keepListening;
            }

            public void StopListening() => IsListening = false;

            public void Hear(string text, float confidence = 0.95f)
            {
                OnPartialResult?.Invoke(text);
                OnResult?.Invoke(text, confidence);
                if (IsListening && !continuous) EndOnItsOwn();
            }

            public void EndOnItsOwn()
            {
                if (!IsListening) return;
                IsListening = false;
                OnListeningEnded?.Invoke();
            }

            public void Fail(VoiceErrorKind kind, string message) => OnError?.Invoke(kind, message);

            public void Dispose() => StopListening();
        }
    }
}
