# Voice Commands

Hands-free control for when your hands are covered in oil. Tap **MIC** in the header, say a
command, and the app acts on it and answers out loud.

> **Status:** Implemented but **never run on a device**. The command handling is covered by
> headless tests; the native speech code for iOS and Android has only been checked as far as this
> repo's tooling allows (see [What has and hasn't been verified](#what-has-and-hasnt-been-verified)).
> Work through the [device checklist](#device-test-checklist) before relying on it.

## Commands

| Say | Does |
|-----|------|
| "next step", "done", "complete", "finished" | Completes the current step |
| "next", "skip" | Moves to the next available step without completing |
| "previous step", "go back", "undo" | Moves to the previous step |
| "read step", "repeat", "say again" | Reads the current step aloud |
| "what tools", "tools needed", "what do I need" | Reads the tools for the current step |
| "read warnings", "any warnings", "safety" | Reads the current step's warnings |
| "lock alignment", "lock model", "lock" | Locks the model alignment |
| "unlock alignment", "unlock model", "unlock", "adjust" | Unlocks alignment for adjustment |
| "reset alignment", "reset model", "reset" | Resets the model to its default position |
| "help", "commands", "what can I say" | Lists some commands |
| "stop listening", "stop", "quiet" | Stops listening |

Commands match on whole words anywhere in what you say, so "okay, next step please" works and
"unlock" never triggers "lock". When two commands match, the one with more words wins.

## Modes

Set **Activation Mode** on the `VoiceCommands` object (`VoiceCommandManager`):

| Mode | Behaviour |
|------|-----------|
| **Push to talk** (default) | Each tap of MIC hears one command, then stops. Lowest battery use, and nothing is heard unless you ask. |
| **Wake word** | Listens continuously; commands count only for a few seconds after "hey mechanic". |
| **Always listening** | Listens continuously and acts on every command it hears. Unmatched speech is ignored silently. |

In every mode, speech heard while the app is talking (and for 0.6 s after) is ignored, so the
microphone picking up "Step completed" can't trigger another command.

## Privacy: recognition stays on the device

`allowCloudRecognition` on `VoiceCommandManager` is **off** by default, matching the app's
local-first design:

- **iOS** requires on-device recognition (iOS 13+). Devices or languages without it report voice
  as unavailable, and the MIC button hides itself.
- **Android 12+** uses the on-device recognizer when the phone has one. Otherwise Android's
  standard recognizer is used with the "prefer offline" hint. That is only a hint: on phones
  without offline speech data, **Android may send audio to its recognition service.** Android
  gives apps no way to forbid this on older recognizers.

Turning `allowCloudRecognition` on lets both platforms fall back to cloud recognition. If you do,
reword `SpeechRecognitionUsage` in `Assets/Scripts/Editor/VoiceBuildPostprocessor.cs`, which
tells iOS users that recognition runs on their device.

## How it fits together

```
MIC button (VoiceButtonUI)
   │ ToggleListening
   ▼
VoiceCommandManager ── matches text (VoiceCommandMatcher) ── acts on ProcedureRunner / ARAlignment
   │        ▲                                                  │
   │        │ OnResult / OnError / OnListeningEnded            ▼
   ▼        │                                            VoiceFeedback (speaks replies)
IVoiceRecognizer
   ├─ EditorVoiceRecognizer   keys 1-5 and 0 simulate commands in the Editor
   ├─ IOSVoiceRecognizer   ─► Assets/Plugins/iOS/MechanicScopeSpeech.mm      (Speech framework)
   └─ AndroidVoiceRecognizer ► Assets/Plugins/Android/MechanicScopeSpeech.java (SpeechRecognizer)
```

The native plugins call back into Unity with `UnitySendMessage` on the `VoiceCommands`
GameObject. That name must stay unique. The message format is defined and tested in
`NativeSpeechMessage` (`VoiceRecognizers.cs`).

The iOS plugin also implements text-to-speech for `VoiceFeedback` (AVSpeechSynthesizer). Android
text-to-speech goes through `android.speech.tts.TextToSpeech` directly from C#.

### Build settings added automatically

`Assets/Scripts/Editor/VoiceBuildPostprocessor.cs` runs on every build:

- **Android:** adds `RECORD_AUDIO`, plus `<queries>` for the speech recognition and
  text-to-speech services. Without those, Android 11+ reports that no recognizer or TTS engine
  exists. The microphone permission is requested the first time you tap MIC.
- **iOS:** adds `NSMicrophoneUsageDescription` and `NSSpeechRecognitionUsageDescription` to
  Info.plist (iOS kills the app on first use without them) and links `Speech.framework`.

## Trying it in the Editor

There is no speech recognition in the Editor. Press Play, tap MIC, then press:
**1** next step · **2** go back · **3** what tools · **4** read warnings · **5** read step ·
**0** stop listening. Spoken replies are logged to the Console as `[TTS] ...`.

## Device test checklist

Run on both platforms if you can, in a quiet room first and then near a running engine.

1. **Permission prompts.** On first tap of MIC: Android asks for the microphone; iOS asks for
   speech recognition, then the microphone. Deny each once and confirm the app says so and
   doesn't crash, then grant.
2. **Push to talk.** Start a procedure, tap MIC, say "next step". The step completes, the app says
   "Step completed", and MIC returns to idle.
3. **Nothing said.** Tap MIC and stay silent. The app should say "Sorry, I didn't catch that" and
   stop listening (Android after its own timeout; iOS after the task ends).
4. **Unrecognized speech.** Tap MIC and say "hand me the wrench". The app says it didn't understand.
5. **Read-backs.** "read step", "what tools", "read warnings" speak the right content.
6. **Echo.** Switch to Always listening, say "read step", and let it read a long step. The app
   must not act on its own voice.
7. **Always listening for a few minutes.** It should keep hearing commands. This covers the
   automatic restart after each utterance and after silence on both platforms.
8. **Airplane mode.** Repeat step 2 offline. On iOS it must still work; on Android it works if
   the phone has offline speech data.
9. **Background and return.** Leave the app while listening, come back, and tap MIC again.

## What has and hasn't been verified

| Part | Verified by |
|------|-------------|
| Command matching, modes, echo guard, native message parsing | 20 headless tests (`VoiceCommandTests`) |
| C# recognizers and manager | Compile in the headless harness |
| Android Java bridge | Type-checked against hand-written stubs of the Android APIs it uses (not the real SDK) |
| Android manifest post-processor | Run against a sample manifest (output checked, idempotent) |
| iOS plugin (`.mm`) and iOS post-processor | **Not compiled.** No Apple toolchain here. The first Xcode build is its first compile |
| Anything involving a microphone | **Not tested.** Needs a device |
