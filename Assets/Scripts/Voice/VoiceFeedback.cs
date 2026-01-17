using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MechanicScope.Voice
{
    /// <summary>
    /// Provides text-to-speech feedback for voice commands and step instructions.
    /// Uses platform-native TTS capabilities.
    /// </summary>
    public class VoiceFeedback : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool enabled = true;
        [SerializeField] private float speechRate = 1.0f;
        [SerializeField] private float pitch = 1.0f;
        [SerializeField] private float volume = 1.0f;
        [SerializeField] private string language = "en-US";

        [Header("Queue Settings")]
        [SerializeField] private bool queueMessages = true;
        [SerializeField] private float delayBetweenMessages = 0.3f;

        // Events
        public event Action<string> OnSpeakStarted;
        public event Action<string> OnSpeakCompleted;
        public event Action<string> OnSpeakError;

        // Properties
        public bool IsEnabled => enabled;
        public bool IsSpeaking { get; private set; }

        private Queue<string> messageQueue = new Queue<string>();
        private bool isProcessingQueue;

        #if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _Speak(string text, float rate, float pitch, float volume, string language);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _StopSpeaking();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern bool _IsSpeaking();
        #endif

        #if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject tts;
        private bool ttsInitialized;
        #endif

        private void Awake()
        {
            InitializeTTS();
        }

        private void OnDestroy()
        {
            StopSpeaking();
            DisposeTTS();
        }

        private void InitializeTTS()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            InitializeAndroidTTS();
            #endif
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private void InitializeAndroidTTS()
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", activity, new TTSInitListener(this));
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize Android TTS: {e.Message}");
            }
        }

        private class TTSInitListener : AndroidJavaProxy
        {
            private VoiceFeedback feedback;

            public TTSInitListener(VoiceFeedback feedback) : base("android.speech.tts.TextToSpeech$OnInitListener")
            {
                this.feedback = feedback;
            }

            public void onInit(int status)
            {
                feedback.ttsInitialized = status == 0; // TextToSpeech.SUCCESS
                if (feedback.ttsInitialized)
                {
                    Debug.Log("Android TTS initialized successfully");
                }
            }
        }
        #endif

        /// <summary>
        /// Speaks the given text.
        /// </summary>
        public void Speak(string text)
        {
            if (!enabled || string.IsNullOrEmpty(text)) return;

            if (queueMessages)
            {
                messageQueue.Enqueue(text);
                if (!isProcessingQueue)
                {
                    StartCoroutine(ProcessQueue());
                }
            }
            else
            {
                SpeakImmediate(text);
            }
        }

        /// <summary>
        /// Speaks immediately, interrupting any current speech.
        /// </summary>
        public void SpeakImmediate(string text)
        {
            if (!enabled || string.IsNullOrEmpty(text)) return;

            StopSpeaking();
            StartCoroutine(SpeakCoroutine(text));
        }

        /// <summary>
        /// Stops any current speech.
        /// </summary>
        public void StopSpeaking()
        {
            messageQueue.Clear();
            isProcessingQueue = false;

            #if UNITY_IOS && !UNITY_EDITOR
            _StopSpeaking();
            #elif UNITY_ANDROID && !UNITY_EDITOR
            if (tts != null && ttsInitialized)
            {
                tts.Call<int>("stop");
            }
            #endif

            IsSpeaking = false;
        }

        /// <summary>
        /// Clears the message queue without stopping current speech.
        /// </summary>
        public void ClearQueue()
        {
            messageQueue.Clear();
        }

        /// <summary>
        /// Sets the speech rate (0.5 = slow, 1.0 = normal, 2.0 = fast).
        /// </summary>
        public void SetSpeechRate(float rate)
        {
            speechRate = Mathf.Clamp(rate, 0.5f, 2.0f);

            #if UNITY_ANDROID && !UNITY_EDITOR
            if (tts != null && ttsInitialized)
            {
                tts.Call<int>("setSpeechRate", speechRate);
            }
            #endif
        }

        /// <summary>
        /// Sets the speech pitch (0.5 = low, 1.0 = normal, 2.0 = high).
        /// </summary>
        public void SetPitch(float newPitch)
        {
            pitch = Mathf.Clamp(newPitch, 0.5f, 2.0f);

            #if UNITY_ANDROID && !UNITY_EDITOR
            if (tts != null && ttsInitialized)
            {
                tts.Call<int>("setPitch", pitch);
            }
            #endif
        }

        /// <summary>
        /// Enables or disables voice feedback.
        /// </summary>
        public void SetEnabled(bool value)
        {
            enabled = value;
            if (!enabled)
            {
                StopSpeaking();
            }
        }

        private IEnumerator ProcessQueue()
        {
            isProcessingQueue = true;

            while (messageQueue.Count > 0)
            {
                string message = messageQueue.Dequeue();
                yield return StartCoroutine(SpeakCoroutine(message));

                if (messageQueue.Count > 0)
                {
                    yield return new WaitForSeconds(delayBetweenMessages);
                }
            }

            isProcessingQueue = false;
        }

        private IEnumerator SpeakCoroutine(string text)
        {
            IsSpeaking = true;
            OnSpeakStarted?.Invoke(text);

            bool success = false;

            #if UNITY_IOS && !UNITY_EDITOR
            _Speak(text, speechRate, pitch, volume, language);
            success = true;

            // Wait for speech to complete
            while (_IsSpeaking())
            {
                yield return null;
            }
            #elif UNITY_ANDROID && !UNITY_EDITOR
            if (tts != null && ttsInitialized)
            {
                // Set up utterance listener for completion callback
                string utteranceId = Guid.NewGuid().ToString();

                var parameters = new AndroidJavaObject("android.os.Bundle");
                parameters.Call("putString", "utteranceId", utteranceId);
                parameters.Call("putFloat", "volume", volume);

                int result = tts.Call<int>("speak", text, 0, parameters, utteranceId); // QUEUE_FLUSH = 0
                success = result == 0; // TextToSpeech.SUCCESS

                if (success)
                {
                    // Estimate speech duration (rough approximation)
                    float estimatedDuration = text.Length * 0.06f / speechRate;
                    yield return new WaitForSeconds(estimatedDuration);
                }

                parameters.Dispose();
            }
            #else
            // Editor fallback - just log
            Debug.Log($"[TTS] {text}");
            success = true;

            // Simulate speech duration
            float estimatedDuration = text.Length * 0.05f;
            yield return new WaitForSeconds(estimatedDuration);
            #endif

            IsSpeaking = false;

            if (success)
            {
                OnSpeakCompleted?.Invoke(text);
            }
            else
            {
                OnSpeakError?.Invoke("Failed to speak");
            }
        }

        private void DisposeTTS()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (tts != null)
            {
                tts.Call("shutdown");
                tts.Dispose();
                tts = null;
            }
            #endif
        }

        /// <summary>
        /// Checks if TTS is available on this platform.
        /// </summary>
        public bool IsAvailable()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            return true; // AVSpeechSynthesizer is always available on iOS
            #elif UNITY_ANDROID && !UNITY_EDITOR
            return ttsInitialized;
            #else
            return true; // Editor always "available" (logs to console)
            #endif
        }
    }
}
