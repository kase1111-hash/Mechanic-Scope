using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MechanicScope.Voice;

namespace MechanicScope.UI
{
    /// <summary>
    /// Header microphone button. Tapping it toggles listening; in push-to-talk mode (the default)
    /// one tap hears one command. Hidden when the device cannot do speech recognition.
    /// </summary>
    public class VoiceButtonUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private VoiceCommandManager voiceManager;

        [Header("UI Elements")]
        [SerializeField] private Button button;
        [SerializeField] private TextMeshProUGUI label;
        [Tooltip("Optional: shows what the recognizer is hearing")]
        [SerializeField] private TextMeshProUGUI transcriptText;

        [Header("Appearance")]
        [SerializeField] private string idleLabel = "MIC";
        [SerializeField] private string listeningLabel = "● MIC";
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color listeningColor = new Color(1f, 0.35f, 0.3f);

        private void Start()
        {
            if (voiceManager == null)
            {
                voiceManager = FindFirstObjectByType<VoiceCommandManager>();
            }

            if (voiceManager == null || !voiceManager.IsEnabled || !voiceManager.IsAvailable)
            {
                gameObject.SetActive(false);
                return;
            }

            if (button != null)
            {
                button.onClick.AddListener(voiceManager.ToggleListening);
            }

            voiceManager.OnListeningStarted += Refresh;
            voiceManager.OnListeningStopped += Refresh;
            voiceManager.OnTranscript += ShowTranscript;
            Refresh();
        }

        private void OnDestroy()
        {
            if (voiceManager == null) return;

            if (button != null)
            {
                button.onClick.RemoveListener(voiceManager.ToggleListening);
            }
            voiceManager.OnListeningStarted -= Refresh;
            voiceManager.OnListeningStopped -= Refresh;
            voiceManager.OnTranscript -= ShowTranscript;
        }

        private void Refresh()
        {
            bool listening = voiceManager != null && voiceManager.IsListening;

            if (label != null)
            {
                label.text = listening ? listeningLabel : idleLabel;
                label.color = listening ? listeningColor : idleColor;
            }

            if (!listening && transcriptText != null)
            {
                transcriptText.text = string.Empty;
            }
        }

        private void ShowTranscript(string text)
        {
            if (transcriptText != null)
            {
                transcriptText.text = text;
            }
        }
    }
}
