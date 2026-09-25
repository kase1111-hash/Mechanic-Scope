using System;
using System.Collections.Generic;
using UnityEngine;
using MechanicScope.Core;

namespace MechanicScope.Voice
{
    /// <summary>
    /// Manages voice command recognition and execution.
    ///
    /// Activation modes:
    ///   PushToTalk      - each StartListening/ToggleListening hears one command, then stops.
    ///   WakeWord        - listens continuously; commands only count for a few seconds after the wake word.
    ///   AlwaysListening - listens continuously and acts on every recognized command.
    ///
    /// Results that arrive while the app is speaking (or just after) are dropped, so the
    /// microphone hearing the app's own "Step completed" cannot trigger another command.
    /// </summary>
    public class VoiceCommandManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ProcedureRunner procedureRunner;
        [SerializeField] private ARAlignment arAlignment;

        [Header("Settings")]
        [SerializeField] private bool enableVoiceCommands = true;
        [SerializeField] private ActivationMode activationMode = ActivationMode.PushToTalk;
        [SerializeField] private string wakeWord = "hey mechanic";
        [SerializeField] private float wakeWordTimeout = 5f;
        [SerializeField] private float commandConfidenceThreshold = 0.6f;

        [Header("Recognition")]
        [Tooltip("BCP-47 language tag passed to the platform recognizer")]
        [SerializeField] private string languageTag = "en-US";
        [Tooltip("Off keeps audio on the device: iOS requires on-device recognition, Android prefers it. " +
                 "On lets the platform send audio to its cloud service when on-device is unavailable.")]
        [SerializeField] private bool allowCloudRecognition = false;
        [Tooltip("Seconds after the app finishes speaking during which recognized speech is ignored")]
        [SerializeField] private float echoGuardSeconds = 0.6f;

        [Header("Feedback")]
        [SerializeField] private bool enableVoiceFeedback = true;
        [SerializeField] private bool enableHapticFeedback = true;

        // Events
        public event Action OnListeningStarted;
        public event Action OnListeningStopped;
        public event Action<string> OnCommandRecognized;
        public event Action<string> OnCommandExecuted;
        public event Action<string> OnRecognitionError;
        /// <summary>What the recognizer is hearing, partial or final, for on-screen display.</summary>
        public event Action<string> OnTranscript;
        public event Action OnWakeWordDetected;

        // Properties
        public bool IsEnabled => enableVoiceCommands;
        public bool IsAvailable => voiceRecognizer != null && voiceRecognizer.IsAvailable;
        public bool IsListening { get; private set; }
        public bool IsWakeWordActive { get; private set; }
        public ActivationMode CurrentActivationMode => activationMode;

        // Components
        private IVoiceRecognizer voiceRecognizer;
        private VoiceFeedback voiceFeedback;
        private readonly VoiceCommandMatcher<VoiceCommand> matcher = new VoiceCommandMatcher<VoiceCommand>();
        private readonly List<VoiceCommand> commands = new List<VoiceCommand>();
        private float wakeWordTimer;
        private float echoGuardUntil;

        public enum ActivationMode
        {
            PushToTalk,
            WakeWord,
            AlwaysListening
        }

        private void Awake()
        {
            voiceFeedback = GetComponent<VoiceFeedback>();
            if (voiceFeedback == null)
            {
                voiceFeedback = gameObject.AddComponent<VoiceFeedback>();
            }
            voiceFeedback.OnSpeakCompleted += StartEchoGuard;
            voiceFeedback.OnSpeakError += StartEchoGuard;

            InitializeRecognizer();
            RegisterDefaultCommands();
        }

        private void Update()
        {
            // Handle wake word timeout
            if (IsWakeWordActive && activationMode == ActivationMode.WakeWord)
            {
                wakeWordTimer -= Time.deltaTime;
                if (wakeWordTimer <= 0)
                {
                    DeactivateWakeWord();
                }
            }
        }

        private void OnDestroy()
        {
            StopListening();
            if (voiceFeedback != null)
            {
                voiceFeedback.OnSpeakCompleted -= StartEchoGuard;
                voiceFeedback.OnSpeakError -= StartEchoGuard;
            }
            DetachRecognizer()?.Dispose();
        }

        private void InitializeRecognizer()
        {
            // Create platform-specific recognizer
            #if UNITY_IOS && !UNITY_EDITOR
            UseRecognizer(gameObject.AddComponent<IOSVoiceRecognizer>());
            #elif UNITY_ANDROID && !UNITY_EDITOR
            UseRecognizer(gameObject.AddComponent<AndroidVoiceRecognizer>());
            #else
            UseRecognizer(gameObject.AddComponent<EditorVoiceRecognizer>());
            #endif
        }

        /// <summary>
        /// Replaces the speech recognizer. The platform one is chosen automatically; this exists so
        /// tests (and alternative engines) can supply their own.
        /// </summary>
        public void UseRecognizer(IVoiceRecognizer recognizer)
        {
            StopListening();
            DetachRecognizer();

            voiceRecognizer = recognizer;
            if (voiceRecognizer == null) return;

            voiceRecognizer.Configure(languageTag, allowCloudRecognition);
            voiceRecognizer.OnResult += HandleRecognitionResult;
            voiceRecognizer.OnError += HandleRecognitionError;
            voiceRecognizer.OnPartialResult += HandlePartialResult;
            voiceRecognizer.OnListeningEnded += HandleListeningEnded;
        }

        private IVoiceRecognizer DetachRecognizer()
        {
            IVoiceRecognizer old = voiceRecognizer;
            if (old != null)
            {
                old.OnResult -= HandleRecognitionResult;
                old.OnError -= HandleRecognitionError;
                old.OnPartialResult -= HandlePartialResult;
                old.OnListeningEnded -= HandleListeningEnded;
            }
            voiceRecognizer = null;
            return old;
        }

        /// <summary>Sets the systems commands act on. The scene wires these as serialized fields.</summary>
        public void SetReferences(ProcedureRunner runner, ARAlignment alignment)
        {
            procedureRunner = runner;
            arAlignment = alignment;
        }

        private void RegisterDefaultCommands()
        {
            // Argument order is (description, action, ...phrases) — see RegisterCommand.
            // Navigation commands
            RegisterCommand("Completes the current step", () =>
            {
                if (procedureRunner?.ActiveStep != null)
                {
                    procedureRunner.CompleteStep(procedureRunner.ActiveStep.id);
                    Speak("Step completed");
                }
            }, "next step", "done", "complete", "finished");

            RegisterCommand("Goes to the previous step", () =>
            {
                procedureRunner?.PreviousStep();
                Speak("Previous step");
            }, "previous step", "go back", "undo");

            RegisterCommand("Moves to the next available step without completing", () =>
            {
                procedureRunner?.NextStep();
                Speak("Next step");
            }, "skip", "next");

            // Information commands
            RegisterCommand("Enters part identification mode", () =>
            {
                Speak("Tap on a part to identify it");
            }, "what is this", "identify", "what part");

            RegisterCommand("Expands the current step details", () =>
            {
                // Trigger UI expansion
                Speak("Showing details");
            }, "show details", "more info", "expand");

            RegisterCommand("Collapses the step details", () =>
            {
                // Trigger UI collapse
                Speak("Hiding details");
            }, "hide details", "collapse", "less");

            RegisterCommand("Reads the tools needed for the current step", () =>
            {
                ReadCurrentStepTools();
            }, "what tools", "tools needed", "what do i need");

            RegisterCommand("Reads any warnings for the current step", () =>
            {
                ReadCurrentStepWarnings();
            }, "read warnings", "any warnings", "safety");

            RegisterCommand("Reads the current step aloud", () =>
            {
                ReadCurrentStep();
            }, "read step", "repeat", "say again");

            // Control commands
            RegisterCommand("Disables voice commands", () =>
            {
                StopListening();
                Speak("Voice commands disabled");
            }, "stop listening", "stop", "quiet");

            RegisterCommand("Lists available voice commands", () =>
            {
                ListAvailableCommands();
            }, "help", "commands", "what can i say");

            // Alignment commands
            RegisterCommand("Locks the model alignment", () =>
            {
                arAlignment?.LockAlignment();
                Speak("Alignment locked");
            }, "lock alignment", "lock model", "lock");

            RegisterCommand("Unlocks the model for adjustment", () =>
            {
                arAlignment?.UnlockAlignment();
                Speak("Alignment unlocked, you can adjust the model");
            }, "unlock alignment", "unlock model", "unlock", "adjust");

            RegisterCommand("Resets the model to default position", () =>
            {
                arAlignment?.ResetAlignment();
                Speak("Alignment reset");
            }, "reset alignment", "reset model", "reset");
        }

        /// <summary>
        /// Registers a voice command with multiple trigger phrases.
        /// </summary>
        public void RegisterCommand(string description, Action action, params string[] phrases)
        {
            var command = new VoiceCommand
            {
                Phrases = new List<string>(phrases),
                Action = action,
                Description = description
            };

            commands.Add(command);
            foreach (string phrase in phrases)
            {
                matcher.Add(phrase, command);
            }
        }

        // A `RegisterCommand(params object[] args)` overload used to sit here, intended to accept
        // (...phrases, action, description) in any order. It could never work: a lambda has no
        // natural conversion to object, so that overload was never applicable, and every call site
        // bound to the typed overload above and failed to compile. Callers now use the typed form.

        /// <summary>
        /// Starts listening for voice commands.
        /// </summary>
        public void StartListening()
        {
            if (!enableVoiceCommands || IsListening) return;

            if (!IsAvailable)
            {
                OnRecognitionError?.Invoke("Voice recognition not available");
                return;
            }

            // A deliberate tap means "listen to me now": cut off any reply still being read out,
            // or the echo guard would discard what the user says next.
            if (activationMode == ActivationMode.PushToTalk && voiceFeedback != null && voiceFeedback.IsSpeaking)
            {
                voiceFeedback.StopSpeaking();
                echoGuardUntil = 0f;
            }

            // Set before starting: a recognizer may deliver results or end synchronously.
            IsListening = true;
            voiceRecognizer.StartListening(activationMode != ActivationMode.PushToTalk);
            if (!voiceRecognizer.IsListening)
            {
                IsListening = false;
                return;
            }
            OnListeningStarted?.Invoke();

            if (enableHapticFeedback)
            {
                Handheld.Vibrate();
            }
        }

        /// <summary>
        /// Stops listening for voice commands.
        /// </summary>
        public void StopListening()
        {
            if (!IsListening) return;

            voiceRecognizer?.StopListening();
            IsListening = false;
            IsWakeWordActive = false;
            OnListeningStopped?.Invoke();
        }

        /// <summary>
        /// Toggles listening state.
        /// </summary>
        public void ToggleListening()
        {
            if (IsListening)
            {
                StopListening();
            }
            else
            {
                StartListening();
            }
        }

        /// <summary>
        /// Sets the activation mode.
        /// </summary>
        public void SetActivationMode(ActivationMode mode)
        {
            // The recognizer's continuous/single-shot mode is fixed when it starts.
            StopListening();
            activationMode = mode;

            if (mode == ActivationMode.AlwaysListening && enableVoiceCommands)
            {
                StartListening();
            }
        }

        /// <summary>
        /// Enables or disables voice commands.
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            enableVoiceCommands = enabled;

            if (!enabled)
            {
                StopListening();
            }
        }

        /// <summary>Enables or disables spoken replies.</summary>
        public void SetVoiceFeedbackEnabled(bool enabled)
        {
            enableVoiceFeedback = enabled;
        }

        /// <summary>True while the app is speaking, or within echoGuardSeconds of finishing.</summary>
        public bool IsHearingOwnVoice =>
            (voiceFeedback != null && voiceFeedback.IsSpeaking) || Time.realtimeSinceStartup < echoGuardUntil;

        private void StartEchoGuard(string unused)
        {
            echoGuardUntil = Time.realtimeSinceStartup + echoGuardSeconds;
        }

        private void HandleRecognitionResult(string text, float confidence)
        {
            if (!IsListening || string.IsNullOrEmpty(text)) return;

            if (IsHearingOwnVoice)
            {
                Debug.Log($"[Voice] Ignored while speaking: \"{text}\"");
                return;
            }

            Debug.Log($"Voice recognized: \"{text}\" (confidence: {confidence:F2})");
            OnTranscript?.Invoke(text);

            // Push-to-talk hears one command per press.
            if (activationMode == ActivationMode.PushToTalk)
            {
                StopListening();
            }

            // Check for wake word
            if (activationMode == ActivationMode.WakeWord && !IsWakeWordActive)
            {
                string padded = " " + VoiceCommandMatcher<VoiceCommand>.Normalize(text) + " ";
                if (padded.Contains(" " + VoiceCommandMatcher<VoiceCommand>.Normalize(wakeWord) + " "))
                {
                    ActivateWakeWord();
                    return;
                }
            }

            // Only process commands if wake word is active or in appropriate mode
            if (activationMode == ActivationMode.WakeWord && !IsWakeWordActive)
            {
                return;
            }

            if (confidence < commandConfidenceThreshold)
            {
                Debug.Log($"Confidence too low: {confidence} < {commandConfidenceThreshold}");
                return;
            }

            VoiceCommand matchedCommand = matcher.Match(text, out string matchedPhrase);

            if (matchedCommand != null)
            {
                OnCommandRecognized?.Invoke(matchedPhrase);
                ExecuteCommand(matchedCommand, matchedPhrase);

                // Reset wake word timer
                if (activationMode == ActivationMode.WakeWord)
                {
                    wakeWordTimer = wakeWordTimeout;
                }
            }
            else if (activationMode != ActivationMode.AlwaysListening)
            {
                // In always-listening mode most speech is conversation, not commands: stay quiet.
                Speak("Sorry, I didn't understand that");
            }
        }

        private void HandlePartialResult(string text)
        {
            if (IsListening && !IsHearingOwnVoice)
            {
                OnTranscript?.Invoke(text);
            }
        }

        private void HandleRecognitionError(VoiceErrorKind kind, string error)
        {
            Debug.LogWarning($"Voice recognition error ({kind}): {error}");
            OnRecognitionError?.Invoke(error);

            switch (kind)
            {
                case VoiceErrorKind.NoSpeech:
                    if (activationMode == ActivationMode.PushToTalk) Speak("Sorry, I didn't catch that");
                    break;
                case VoiceErrorKind.PermissionDenied:
                    Speak("Microphone permission is needed for voice commands");
                    break;
                case VoiceErrorKind.Unavailable:
                    Speak("Voice commands are not available on this device");
                    break;
            }
        }

        /// <summary>The recognizer stopped on its own (end of utterance, error, permission denied).</summary>
        private void HandleListeningEnded()
        {
            if (!IsListening) return;

            IsListening = false;
            IsWakeWordActive = false;
            OnListeningStopped?.Invoke();
        }

        private void ExecuteCommand(VoiceCommand command, string phrase)
        {
            try
            {
                command.Action?.Invoke();
                OnCommandExecuted?.Invoke(phrase);

                if (enableHapticFeedback)
                {
                    // Light haptic for command execution
                    #if UNITY_IOS
                    // iOS haptic feedback
                    #endif
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error executing voice command: {e.Message}");
                Speak("Sorry, there was an error");
            }
        }

        private void ActivateWakeWord()
        {
            IsWakeWordActive = true;
            wakeWordTimer = wakeWordTimeout;
            OnWakeWordDetected?.Invoke();
            Speak("I'm listening");

            if (enableHapticFeedback)
            {
                Handheld.Vibrate();
            }
        }

        private void DeactivateWakeWord()
        {
            IsWakeWordActive = false;
            Speak("Goodbye");
        }

        private void Speak(string message)
        {
            if (enableVoiceFeedback && voiceFeedback != null)
            {
                voiceFeedback.Speak(message);
            }
        }

        private void ReadCurrentStep()
        {
            if (procedureRunner?.ActiveStep == null)
            {
                Speak("No active step");
                return;
            }

            var step = procedureRunner.ActiveStep;
            string text = step.action;

            if (!string.IsNullOrEmpty(step.details))
            {
                text += ". " + step.details;
            }

            Speak(text);
        }

        private void ReadCurrentStepTools()
        {
            if (procedureRunner?.ActiveStep?.tools == null || procedureRunner.ActiveStep.tools.Length == 0)
            {
                Speak("No tools required for this step");
                return;
            }

            string tools = string.Join(", ", procedureRunner.ActiveStep.tools);
            Speak($"You'll need: {tools}");
        }

        private void ReadCurrentStepWarnings()
        {
            if (procedureRunner?.ActiveStep?.warnings == null || procedureRunner.ActiveStep.warnings.Length == 0)
            {
                Speak("No warnings for this step");
                return;
            }

            foreach (string warning in procedureRunner.ActiveStep.warnings)
            {
                Speak($"Warning: {warning}");
            }
        }

        private void ListAvailableCommands()
        {
            Speak("You can say: next step, go back, what tools, read warnings, stop listening, and more");
        }

        /// <summary>
        /// Gets all registered commands for display.
        /// </summary>
        public List<(string phrase, string description)> GetRegisteredCommands()
        {
            var result = new List<(string, string)>();
            foreach (VoiceCommand command in commands)
            {
                result.Add((string.Join(" / ", command.Phrases), command.Description));
            }
            return result;
        }
    }

    /// <summary>
    /// Represents a voice command with multiple trigger phrases.
    /// </summary>
    public class VoiceCommand
    {
        public List<string> Phrases { get; set; }
        public Action Action { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// Interface for platform-specific voice recognition.
    /// </summary>
    public interface IVoiceRecognizer : IDisposable
    {
        /// <summary>A final transcript and its confidence (0..1).</summary>
        event Action<string, float> OnResult;
        event Action<string> OnPartialResult;
        event Action<VoiceErrorKind, string> OnError;

        /// <summary>Raised when the recognizer stops on its own; not raised by StopListening.</summary>
        event Action OnListeningEnded;

        bool IsAvailable { get; }
        bool IsListening { get; }

        void Configure(string languageTag, bool allowCloudRecognition);

        /// <summary>
        /// Starts recognition. With <paramref name="continuous"/> false the recognizer delivers one
        /// utterance and then ends (raising OnListeningEnded); with true it keeps going until stopped.
        /// </summary>
        void StartListening(bool continuous);

        void StopListening();
    }
}
