using System;
using System.Globalization;
using UnityEngine;

namespace MechanicScope.Voice
{
    /// <summary>
    /// Why a recognizer reported an error. The manager treats these differently: NoSpeech is routine
    /// (the user said nothing, or nothing intelligible), the rest mean voice cannot work right now.
    /// </summary>
    public enum VoiceErrorKind
    {
        NoSpeech,
        PermissionDenied,
        Unavailable,
        Network,
        Other
    }

    /// <summary>
    /// Wire format of the messages the native plugins send with UnitySendMessage. UnitySendMessage
    /// carries a single string, so structured payloads are "field|rest":
    ///   result: "&lt;confidence&gt;|&lt;text&gt;"  confidence in 0..1, or negative when the platform gave none
    ///   error:  "&lt;code&gt;|&lt;message&gt;"     code is no_speech, permission, unavailable, network or other
    /// Kept in C# (and tested) so both plugins can stay as thin as possible.
    /// </summary>
    public static class NativeSpeechMessage
    {
        /// <summary>Confidence reported when the platform does not provide one.</summary>
        public const float UnknownConfidence = 1f;

        public static bool TryParseResult(string message, out string text, out float confidence)
        {
            text = null;
            confidence = UnknownConfidence;
            if (string.IsNullOrEmpty(message)) return false;

            int bar = message.IndexOf('|');
            if (bar < 0)
            {
                text = message;
            }
            else
            {
                if (float.TryParse(message.Substring(0, bar), NumberStyles.Float, CultureInfo.InvariantCulture,
                        out float parsed) && parsed >= 0f)
                {
                    confidence = Mathf.Clamp01(parsed);
                }
                text = message.Substring(bar + 1);
            }

            text = text.Trim();
            return text.Length > 0;
        }

        public static void ParseError(string message, out VoiceErrorKind kind, out string detail)
        {
            kind = VoiceErrorKind.Other;
            detail = message ?? string.Empty;
            if (string.IsNullOrEmpty(message)) return;

            int bar = message.IndexOf('|');
            if (bar < 0) return;

            detail = message.Substring(bar + 1);
            switch (message.Substring(0, bar))
            {
                case "no_speech": kind = VoiceErrorKind.NoSpeech; break;
                case "permission": kind = VoiceErrorKind.PermissionDenied; break;
                case "unavailable": kind = VoiceErrorKind.Unavailable; break;
                case "network": kind = VoiceErrorKind.Network; break;
            }
        }
    }

    /// <summary>
    /// Shared behaviour for the iOS and Android recognizers. The native plugins call the
    /// OnNative* methods by name through UnitySendMessage, so this component's GameObject name
    /// is passed to the plugin when listening starts and must be unique in the scene.
    /// </summary>
    public abstract class NativeVoiceRecognizer : MonoBehaviour, IVoiceRecognizer
    {
        public event Action<string, float> OnResult;
        public event Action<string> OnPartialResult;
        public event Action<VoiceErrorKind, string> OnError;
        public event Action OnListeningEnded;

        public bool IsAvailable { get; protected set; }
        public bool IsListening { get; private set; }

        protected string LanguageTag { get; private set; } = "en-US";
        protected bool AllowCloudRecognition { get; private set; }

        public void Configure(string languageTag, bool allowCloudRecognition)
        {
            LanguageTag = string.IsNullOrEmpty(languageTag) ? "en-US" : languageTag;
            AllowCloudRecognition = allowCloudRecognition;
            RefreshAvailability();
        }

        public void StartListening(bool continuous)
        {
            if (IsListening) return;

            if (!IsAvailable)
            {
                OnError?.Invoke(VoiceErrorKind.Unavailable, "Speech recognition is not available on this device");
                return;
            }

            IsListening = true;
            BeginNative(continuous);
        }

        public void StopListening()
        {
            if (!IsListening) return;

            IsListening = false;
            EndNative();
        }

        /// <summary>Re-checks whether recognition can run with the current language and settings.</summary>
        protected abstract void RefreshAvailability();

        protected abstract void BeginNative(bool continuous);

        protected abstract void EndNative();

        // === Called by the native plugins via UnitySendMessage ===

        public void OnNativeSpeechResult(string message)
        {
            if (NativeSpeechMessage.TryParseResult(message, out string text, out float confidence))
            {
                OnResult?.Invoke(text, confidence);
            }
        }

        public void OnNativePartialResult(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                OnPartialResult?.Invoke(text);
            }
        }

        public void OnNativeSpeechError(string message)
        {
            NativeSpeechMessage.ParseError(message, out VoiceErrorKind kind, out string detail);
            OnError?.Invoke(kind, detail);
        }

        /// <summary>
        /// The plugin stopped on its own: end of a single utterance, or an error it will not retry.
        /// Ignored after StopListening, since the caller already knows.
        /// </summary>
        public void OnNativeListeningEnded(string unused)
        {
            if (!IsListening) return;

            IsListening = false;
            OnListeningEnded?.Invoke();
        }

        protected void RaiseError(VoiceErrorKind kind, string message) => OnError?.Invoke(kind, message);

        public virtual void Dispose()
        {
            StopListening();
        }

        protected virtual void OnDestroy()
        {
            Dispose();
        }
    }

    /// <summary>
    /// Editor recognizer. There is no speech recognition in the Editor, so while listening the
    /// number keys simulate commands (1 next step, 2 go back, 3 what tools, 4 read warnings,
    /// 5 read step, 0 stop listening). SimulateVoiceInput does the same from code.
    /// </summary>
    public class EditorVoiceRecognizer : MonoBehaviour, IVoiceRecognizer
    {
        public event Action<string, float> OnResult;
        public event Action<string> OnPartialResult;
        public event Action<VoiceErrorKind, string> OnError;
        public event Action OnListeningEnded;

        public bool IsAvailable => Application.isEditor;
        public bool IsListening { get; private set; }

        private bool continuous;

        public void Configure(string languageTag, bool allowCloudRecognition) { }

        public void StartListening(bool keepListening)
        {
            if (!IsAvailable)
            {
                OnError?.Invoke(VoiceErrorKind.Unavailable, "Speech recognition is not available on this platform");
                return;
            }

            if (IsListening) return;

            IsListening = true;
            continuous = keepListening;
            Debug.Log("[Voice] Editor recognizer listening - press 1-5 or 0 to simulate a command");
        }

        public void StopListening()
        {
            IsListening = false;
        }

        /// <summary>Delivers text as if it had been recognized. Ends listening unless continuous.</summary>
        public void SimulateVoiceInput(string text, float confidence = 0.9f)
        {
            if (!IsListening) return;

            OnPartialResult?.Invoke(text);
            OnResult?.Invoke(text, confidence);

            if (!continuous && IsListening)
            {
                IsListening = false;
                OnListeningEnded?.Invoke();
            }
        }

        #if UNITY_EDITOR
        private void Update()
        {
            if (!IsListening) return;

            if (Input.GetKeyDown(KeyCode.Alpha1)) SimulateVoiceInput("next step");
            else if (Input.GetKeyDown(KeyCode.Alpha2)) SimulateVoiceInput("go back");
            else if (Input.GetKeyDown(KeyCode.Alpha3)) SimulateVoiceInput("what tools");
            else if (Input.GetKeyDown(KeyCode.Alpha4)) SimulateVoiceInput("read warnings");
            else if (Input.GetKeyDown(KeyCode.Alpha5)) SimulateVoiceInput("read step");
            else if (Input.GetKeyDown(KeyCode.Alpha0)) SimulateVoiceInput("stop listening");
        }
        #endif

        public void Dispose()
        {
            StopListening();
        }
    }

    /// <summary>
    /// iOS recognizer backed by the Speech framework (Assets/Plugins/iOS/MechanicScopeSpeech.mm).
    /// Unless cloud recognition is allowed, it requires on-device recognition (iOS 13+) so audio
    /// never leaves the phone; devices or languages without it report Unavailable.
    /// </summary>
    public class IOSVoiceRecognizer : NativeVoiceRecognizer
    {
        #if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern int _MSSpeechIsAvailable(string languageTag, int allowCloud);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _MSSpeechStart(string gameObjectName, string languageTag, int allowCloud, int continuous);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _MSSpeechStop();
        #endif

        private void Awake()
        {
            RefreshAvailability();
        }

        protected override void RefreshAvailability()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            IsAvailable = _MSSpeechIsAvailable(LanguageTag, AllowCloudRecognition ? 1 : 0) != 0;
            #else
            IsAvailable = false;
            #endif
        }

        protected override void BeginNative(bool continuous)
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _MSSpeechStart(gameObject.name, LanguageTag, AllowCloudRecognition ? 1 : 0, continuous ? 1 : 0);
            #endif
        }

        protected override void EndNative()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _MSSpeechStop();
            #endif
        }
    }

    /// <summary>
    /// Android recognizer backed by android.speech.SpeechRecognizer, driven through
    /// Assets/Plugins/Android/MechanicScopeSpeech.java (SpeechRecognizer must be used on the UI
    /// thread, which a plain JNI call from Unity's thread is not). Asks for the microphone
    /// permission the first time listening starts.
    /// </summary>
    public class AndroidVoiceRecognizer : NativeVoiceRecognizer
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        private const string BridgeClass = "com.mechanicscope.MechanicScopeSpeech";

        private AndroidJavaClass bridge;
        private AndroidJavaObject activity;
        #endif

        private void Awake()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                bridge = new AndroidJavaClass(BridgeClass);
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Voice] Android speech bridge unavailable: {e.Message}");
                bridge = null;
            }
            #endif

            RefreshAvailability();
        }

        protected override void RefreshAvailability()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            IsAvailable = bridge != null && bridge.CallStatic<bool>("isAvailable", activity);
            #else
            IsAvailable = false;
            #endif
        }

        protected override void BeginNative(bool continuous)
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                StartBridge(continuous);
                return;
            }

            var callbacks = new UnityEngine.Android.PermissionCallbacks();
            callbacks.PermissionGranted += _ =>
            {
                if (IsListening) StartBridge(continuous);
            };
            callbacks.PermissionDenied += _ => DenyPermission();
            callbacks.PermissionDeniedAndDontAskAgain += _ => DenyPermission();
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone, callbacks);
            #endif
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private void StartBridge(bool continuous)
        {
            bridge.CallStatic("start", activity, gameObject.name, LanguageTag, AllowCloudRecognition, continuous);
        }

        private void DenyPermission()
        {
            RaiseError(VoiceErrorKind.PermissionDenied, "Microphone permission was denied");
            OnNativeListeningEnded(null);
        }
        #endif

        protected override void EndNative()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            bridge?.CallStatic("stop", activity);
            #endif
        }

        public override void Dispose()
        {
            base.Dispose();

            #if UNITY_ANDROID && !UNITY_EDITOR
            if (bridge != null)
            {
                bridge.CallStatic("destroy", activity);
                bridge.Dispose();
                bridge = null;
            }
            activity?.Dispose();
            activity = null;
            #endif
        }
    }
}
