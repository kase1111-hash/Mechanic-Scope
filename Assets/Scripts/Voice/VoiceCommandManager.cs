using System;
using System.Collections.Generic;
using UnityEngine;
using MechanicScope.Core;

namespace MechanicScope.Voice
{
    /// <summary>
    /// Manages voice command recognition and execution.
    /// Supports both push-to-talk and wake word activation modes.
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

        [Header("Feedback")]
        [SerializeField] private bool enableVoiceFeedback = true;
        [SerializeField] private bool enableHapticFeedback = true;

        // Events
        public event Action OnListeningStarted;
        public event Action OnListeningStopped;
        public event Action<string> OnCommandRecognized;
        public event Action<string> OnCommandExecuted;
        public event Action<string> OnRecognitionError;
        public event Action OnWakeWordDetected;

        // Properties
        public bool IsEnabled => enableVoiceCommands;
        public bool IsListening { get; private set; }
        public bool IsWakeWordActive { get; private set; }
        public ActivationMode CurrentActivationMode => activationMode;

        // Components
        private IVoiceRecognizer voiceRecognizer;
        private VoiceFeedback voiceFeedback;
        private Dictionary<string, VoiceCommand> commands = new Dictionary<string, VoiceCommand>();
        private float wakeWordTimer;

        public enum ActivationMode
        {
            PushToTalk,
            WakeWord,
            AlwaysListening
        }

        private void Awake()
        {
            InitializeRecognizer();
            RegisterDefaultCommands();
        }

        private void Start()
        {
            voiceFeedback = GetComponent<VoiceFeedback>();
            if (voiceFeedback == null)
            {
                voiceFeedback = gameObject.AddComponent<VoiceFeedback>();
            }
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
            voiceRecognizer?.Dispose();
        }

        private void InitializeRecognizer()
        {
            // Create platform-specific recognizer
            #if UNITY_IOS && !UNITY_EDITOR
            voiceRecognizer = gameObject.AddComponent<IOSVoiceRecognizer>();
            #elif UNITY_ANDROID && !UNITY_EDITOR
            voiceRecognizer = gameObject.AddComponent<AndroidVoiceRecognizer>();
            #else
            voiceRecognizer = gameObject.AddComponent<EditorVoiceRecognizer>();
            #endif

            if (voiceRecognizer != null)
            {
                voiceRecognizer.OnResult += HandleRecognitionResult;
                voiceRecognizer.OnError += HandleRecognitionError;
                voiceRecognizer.OnPartialResult += HandlePartialResult;
            }
        }

        private void RegisterDefaultCommands()
        {
            // Navigation commands
            RegisterCommand("next step", "done", "complete", "finished", () =>
            {
                if (procedureRunner?.ActiveStep != null)
                {
                    procedureRunner.CompleteStep(procedureRunner.ActiveStep.id);
                    Speak("Step completed");
                }
            }, "Completes the current step");

            RegisterCommand("previous step", "go back", "undo", () =>
            {
                procedureRunner?.PreviousStep();
                Speak("Previous step");
            }, "Goes to the previous step");

            RegisterCommand("skip", "next", () =>
            {
                procedureRunner?.NextStep();
                Speak("Next step");
            }, "Moves to the next available step without completing");

            // Information commands
            RegisterCommand("what is this", "identify", "what part", () =>
            {
                Speak("Tap on a part to identify it");
            }, "Enters part identification mode");

            RegisterCommand("show details", "more info", "expand", () =>
            {
                // Trigger UI expansion
                Speak("Showing details");
            }, "Expands the current step details");

            RegisterCommand("hide details", "collapse", "less", () =>
            {
                // Trigger UI collapse
                Speak("Hiding details");
            }, "Collapses the step details");

            RegisterCommand("what tools", "tools needed", "what do i need", () =>
            {
                ReadCurrentStepTools();
            }, "Reads the tools needed for the current step");

            RegisterCommand("read warnings", "any warnings", "safety", () =>
            {
                ReadCurrentStepWarnings();
            }, "Reads any warnings for the current step");

            RegisterCommand("read step", "repeat", "say again", () =>
            {
                ReadCurrentStep();
            }, "Reads the current step aloud");

            // Control commands
            RegisterCommand("stop listening", "stop", "quiet", () =>
            {
                StopListening();
                Speak("Voice commands disabled");
            }, "Disables voice commands");

            RegisterCommand("help", "commands", "what can i say", () =>
            {
                ListAvailableCommands();
            }, "Lists available voice commands");

            // Alignment commands
            RegisterCommand("lock alignment", "lock model", "lock", () =>
            {
                arAlignment?.LockAlignment();
                Speak("Alignment locked");
            }, "Locks the model alignment");

            RegisterCommand("unlock alignment", "unlock model", "adjust", () =>
            {
                arAlignment?.UnlockAlignment();
                Speak("Alignment unlocked, you can adjust the model");
            }, "Unlocks the model for adjustment");

            RegisterCommand("reset alignment", "reset model", "reset", () =>
            {
                arAlignment?.ResetAlignment();
                Speak("Alignment reset");
            }, "Resets the model to default position");
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

            foreach (string phrase in phrases)
            {
                string normalized = NormalizePhrase(phrase);
                commands[normalized] = command;
            }
        }

        /// <summary>
        /// Registers a command with the first phrase being the primary.
        /// </summary>
        public void RegisterCommand(params object[] args)
        {
            List<string> phrases = new List<string>();
            Action action = null;
            string description = "";

            foreach (var arg in args)
            {
                if (arg is string s)
                {
                    // Check if it's a description (last string before action)
                    if (action != null)
                    {
                        description = s;
                    }
                    else
                    {
                        phrases.Add(s);
                    }
                }
                else if (arg is Action a)
                {
                    action = a;
                }
            }

            if (phrases.Count > 0 && action != null)
            {
                RegisterCommand(description, action, phrases.ToArray());
            }
        }

        /// <summary>
        /// Starts listening for voice commands.
        /// </summary>
        public void StartListening()
        {
            if (!enableVoiceCommands || IsListening) return;

            if (voiceRecognizer == null)
            {
                OnRecognitionError?.Invoke("Voice recognition not available");
                return;
            }

            voiceRecognizer.StartListening();
            IsListening = true;
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

        private void HandleRecognitionResult(string text, float confidence)
        {
            if (string.IsNullOrEmpty(text)) return;

            string normalized = NormalizePhrase(text);
            Debug.Log($"Voice recognized: \"{text}\" (confidence: {confidence:F2})");

            // Check for wake word
            if (activationMode == ActivationMode.WakeWord && !IsWakeWordActive)
            {
                if (normalized.Contains(NormalizePhrase(wakeWord)))
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

            // Try to match command
            VoiceCommand matchedCommand = null;
            string matchedPhrase = "";

            foreach (var kvp in commands)
            {
                if (normalized.Contains(kvp.Key))
                {
                    if (matchedPhrase.Length < kvp.Key.Length)
                    {
                        matchedCommand = kvp.Value;
                        matchedPhrase = kvp.Key;
                    }
                }
            }

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
            else
            {
                Speak("Sorry, I didn't understand that");
            }
        }

        private void HandlePartialResult(string text)
        {
            // Could update UI with partial recognition
            Debug.Log($"Partial: {text}");
        }

        private void HandleRecognitionError(string error)
        {
            Debug.LogWarning($"Voice recognition error: {error}");
            OnRecognitionError?.Invoke(error);
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

        private string NormalizePhrase(string phrase)
        {
            return phrase.ToLower().Trim();
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
            var seen = new HashSet<VoiceCommand>();

            foreach (var kvp in commands)
            {
                if (!seen.Contains(kvp.Value))
                {
                    seen.Add(kvp.Value);
                    string phrases = string.Join(" / ", kvp.Value.Phrases);
                    result.Add((phrases, kvp.Value.Description));
                }
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
        event Action<string, float> OnResult;
        event Action<string> OnPartialResult;
        event Action<string> OnError;

        bool IsAvailable { get; }
        bool IsListening { get; }

        void StartListening();
        void StopListening();
    }
}
