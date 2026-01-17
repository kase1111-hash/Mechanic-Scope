using System;
using UnityEngine;

namespace MechanicScope.Voice
{
    /// <summary>
    /// Editor/fallback voice recognizer for testing.
    /// Uses Unity's built-in microphone for basic functionality.
    /// </summary>
    public class EditorVoiceRecognizer : MonoBehaviour, IVoiceRecognizer
    {
        public event Action<string, float> OnResult;
        public event Action<string> OnPartialResult;
        public event Action<string> OnError;

        public bool IsAvailable => Microphone.devices.Length > 0;
        public bool IsListening { get; private set; }

        private AudioClip recordingClip;
        private string microphoneDevice;

        private void Awake()
        {
            if (Microphone.devices.Length > 0)
            {
                microphoneDevice = Microphone.devices[0];
            }
        }

        public void StartListening()
        {
            if (!IsAvailable)
            {
                OnError?.Invoke("No microphone available");
                return;
            }

            if (IsListening) return;

            IsListening = true;
            recordingClip = Microphone.Start(microphoneDevice, true, 10, 16000);
            Debug.Log("Editor voice recognizer started (simulation only)");
        }

        public void StopListening()
        {
            if (!IsListening) return;

            Microphone.End(microphoneDevice);
            IsListening = false;
            Debug.Log("Editor voice recognizer stopped");

            // In editor, we simulate with keyboard input
            // Real speech recognition would require external service
        }

        /// <summary>
        /// Simulates voice input for testing in editor.
        /// </summary>
        public void SimulateVoiceInput(string text, float confidence = 0.9f)
        {
            OnResult?.Invoke(text, confidence);
        }

        private void Update()
        {
            // Allow keyboard simulation in editor
            #if UNITY_EDITOR
            if (IsListening)
            {
                // Press keys 1-9 to simulate common commands
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    SimulateVoiceInput("next step");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    SimulateVoiceInput("go back");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    SimulateVoiceInput("what tools");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha4))
                {
                    SimulateVoiceInput("read warnings");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha5))
                {
                    SimulateVoiceInput("read step");
                }
                else if (Input.GetKeyDown(KeyCode.Alpha0))
                {
                    SimulateVoiceInput("stop listening");
                }
            }
            #endif
        }

        public void Dispose()
        {
            StopListening();
        }
    }

    /// <summary>
    /// iOS voice recognizer using Speech framework.
    /// Requires iOS 10+ and Speech recognition permission.
    /// </summary>
    public class IOSVoiceRecognizer : MonoBehaviour, IVoiceRecognizer
    {
        public event Action<string, float> OnResult;
        public event Action<string> OnPartialResult;
        public event Action<string> OnError;

        public bool IsAvailable { get; private set; }
        public bool IsListening { get; private set; }

        #if UNITY_IOS && !UNITY_EDITOR
        // Native iOS plugin would be implemented here
        // Using Unity's native plugin interface

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _StartSpeechRecognition();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _StopSpeechRecognition();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern bool _IsSpeechRecognitionAvailable();
        #endif

        private void Awake()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            IsAvailable = _IsSpeechRecognitionAvailable();
            #else
            IsAvailable = false;
            #endif
        }

        public void StartListening()
        {
            if (!IsAvailable)
            {
                OnError?.Invoke("Speech recognition not available on this device");
                return;
            }

            #if UNITY_IOS && !UNITY_EDITOR
            _StartSpeechRecognition();
            IsListening = true;
            #endif
        }

        public void StopListening()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            _StopSpeechRecognition();
            IsListening = false;
            #endif
        }

        // Called from native iOS code
        public void OnSpeechResult(string json)
        {
            // Parse JSON result from native code
            // { "text": "next step", "confidence": 0.95 }
            try
            {
                var result = JsonUtility.FromJson<SpeechResult>(json);
                OnResult?.Invoke(result.text, result.confidence);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse speech result: {e.Message}");
            }
        }

        public void OnSpeechPartialResult(string text)
        {
            OnPartialResult?.Invoke(text);
        }

        public void OnSpeechError(string error)
        {
            OnError?.Invoke(error);
            IsListening = false;
        }

        public void Dispose()
        {
            StopListening();
        }

        [Serializable]
        private class SpeechResult
        {
            public string text;
            public float confidence;
        }
    }

    /// <summary>
    /// Android voice recognizer using SpeechRecognizer API.
    /// Requires RECORD_AUDIO permission.
    /// </summary>
    public class AndroidVoiceRecognizer : MonoBehaviour, IVoiceRecognizer
    {
        public event Action<string, float> OnResult;
        public event Action<string> OnPartialResult;
        public event Action<string> OnError;

        public bool IsAvailable { get; private set; }
        public bool IsListening { get; private set; }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject speechRecognizer;
        private AndroidJavaObject recognitionListener;
        #endif

        private void Awake()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroid();
            #else
            IsAvailable = false;
            #endif
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private void InitializeAndroid()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var speechClass = new AndroidJavaClass("android.speech.SpeechRecognizer"))
                {
                    IsAvailable = speechClass.CallStatic<bool>("isRecognitionAvailable", activity);

                    if (IsAvailable)
                    {
                        speechRecognizer = speechClass.CallStatic<AndroidJavaObject>("createSpeechRecognizer", activity);
                        CreateRecognitionListener();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize Android speech recognizer: {e.Message}");
                IsAvailable = false;
            }
        }

        private void CreateRecognitionListener()
        {
            recognitionListener = new AndroidJavaObject("com.mechanicscope.SpeechListener", gameObject.name);
            speechRecognizer.Call("setRecognitionListener", recognitionListener);
        }
        #endif

        public void StartListening()
        {
            if (!IsAvailable)
            {
                OnError?.Invoke("Speech recognition not available");
                return;
            }

            #if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var intent = new AndroidJavaObject("android.content.Intent",
                    "android.speech.action.RECOGNIZE_SPEECH"))
                {
                    using (var recognizerIntent = new AndroidJavaClass("android.speech.RecognizerIntent"))
                    {
                        intent.Call<AndroidJavaObject>("putExtra",
                            recognizerIntent.GetStatic<string>("EXTRA_LANGUAGE_MODEL"),
                            recognizerIntent.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM"));

                        intent.Call<AndroidJavaObject>("putExtra",
                            recognizerIntent.GetStatic<string>("EXTRA_PARTIAL_RESULTS"),
                            true);

                        intent.Call<AndroidJavaObject>("putExtra",
                            recognizerIntent.GetStatic<string>("EXTRA_MAX_RESULTS"),
                            1);
                    }

                    speechRecognizer.Call("startListening", intent);
                    IsListening = true;
                }
            }
            catch (Exception e)
            {
                OnError?.Invoke($"Failed to start listening: {e.Message}");
            }
            #endif
        }

        public void StopListening()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            speechRecognizer?.Call("stopListening");
            IsListening = false;
            #endif
        }

        // Called from Android native code via SendMessage
        public void OnAndroidSpeechResult(string result)
        {
            OnResult?.Invoke(result, 0.9f);
        }

        public void OnAndroidPartialResult(string result)
        {
            OnPartialResult?.Invoke(result);
        }

        public void OnAndroidSpeechError(string error)
        {
            OnError?.Invoke(error);
            IsListening = false;
        }

        public void Dispose()
        {
            StopListening();

            #if UNITY_ANDROID && !UNITY_EDITOR
            speechRecognizer?.Call("destroy");
            speechRecognizer?.Dispose();
            recognitionListener?.Dispose();
            #endif
        }

        private void OnDestroy()
        {
            Dispose();
        }
    }
}
