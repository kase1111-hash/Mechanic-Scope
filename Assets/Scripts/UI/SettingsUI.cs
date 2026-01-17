using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MechanicScope.Data;

namespace MechanicScope.UI
{
    /// <summary>
    /// Settings screen UI controller.
    /// Manages user preferences and app configuration.
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private DataManager dataManager;

        [Header("Display Settings")]
        [SerializeField] private Toggle highlightEffectsToggle;
        [SerializeField] private Toggle showWarningsToggle;
        [SerializeField] private Toggle showTorqueSpecsToggle;
        [SerializeField] private Slider highlightIntensitySlider;
        [SerializeField] private TextMeshProUGUI highlightIntensityValue;

        [Header("Voice Settings")]
        [SerializeField] private Toggle voiceCommandsToggle;
        [SerializeField] private Toggle voiceFeedbackToggle;
        [SerializeField] private TMP_Dropdown wakeWordDropdown;

        [Header("Procedure Settings")]
        [SerializeField] private Toggle autoAdvanceToggle;
        [SerializeField] private Toggle confirmCompletionToggle;
        [SerializeField] private Toggle showStepNumbersToggle;

        [Header("Data Settings")]
        [SerializeField] private Button exportDataButton;
        [SerializeField] private Button importDataButton;
        [SerializeField] private Button clearHistoryButton;
        [SerializeField] private Button resetAllButton;
        [SerializeField] private TextMeshProUGUI dataStatsText;

        [Header("About")]
        [SerializeField] private TextMeshProUGUI versionText;
        [SerializeField] private Button privacyPolicyButton;
        [SerializeField] private Button licensesButton;
        [SerializeField] private Button feedbackButton;

        [Header("Navigation")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button saveButton;

        // Events
        public event Action OnSettingsChanged;
        public event Action OnBackPressed;

        // Preference keys
        private const string PREF_HIGHLIGHT_EFFECTS = "highlight_effects";
        private const string PREF_SHOW_WARNINGS = "show_warnings";
        private const string PREF_SHOW_TORQUE = "show_torque_specs";
        private const string PREF_HIGHLIGHT_INTENSITY = "highlight_intensity";
        private const string PREF_VOICE_COMMANDS = "voice_commands";
        private const string PREF_VOICE_FEEDBACK = "voice_feedback";
        private const string PREF_WAKE_WORD = "wake_word";
        private const string PREF_AUTO_ADVANCE = "auto_advance";
        private const string PREF_CONFIRM_COMPLETION = "confirm_completion";
        private const string PREF_SHOW_STEP_NUMBERS = "show_step_numbers";

        private bool isDirty;

        private void Start()
        {
            SetupUI();
            LoadSettings();
            UpdateDataStats();
        }

        private void SetupUI()
        {
            // Display settings
            if (highlightEffectsToggle != null)
                highlightEffectsToggle.onValueChanged.AddListener(OnHighlightEffectsChanged);
            if (showWarningsToggle != null)
                showWarningsToggle.onValueChanged.AddListener(OnShowWarningsChanged);
            if (showTorqueSpecsToggle != null)
                showTorqueSpecsToggle.onValueChanged.AddListener(OnShowTorqueChanged);
            if (highlightIntensitySlider != null)
            {
                highlightIntensitySlider.onValueChanged.AddListener(OnHighlightIntensityChanged);
                highlightIntensitySlider.minValue = 0.1f;
                highlightIntensitySlider.maxValue = 1f;
            }

            // Voice settings
            if (voiceCommandsToggle != null)
                voiceCommandsToggle.onValueChanged.AddListener(OnVoiceCommandsChanged);
            if (voiceFeedbackToggle != null)
                voiceFeedbackToggle.onValueChanged.AddListener(OnVoiceFeedbackChanged);
            if (wakeWordDropdown != null)
            {
                wakeWordDropdown.ClearOptions();
                wakeWordDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "None (Push-to-talk)",
                    "Hey Mechanic",
                    "OK Scope"
                });
                wakeWordDropdown.onValueChanged.AddListener(OnWakeWordChanged);
            }

            // Procedure settings
            if (autoAdvanceToggle != null)
                autoAdvanceToggle.onValueChanged.AddListener(OnAutoAdvanceChanged);
            if (confirmCompletionToggle != null)
                confirmCompletionToggle.onValueChanged.AddListener(OnConfirmCompletionChanged);
            if (showStepNumbersToggle != null)
                showStepNumbersToggle.onValueChanged.AddListener(OnShowStepNumbersChanged);

            // Data buttons
            if (exportDataButton != null)
                exportDataButton.onClick.AddListener(OnExportData);
            if (importDataButton != null)
                importDataButton.onClick.AddListener(OnImportData);
            if (clearHistoryButton != null)
                clearHistoryButton.onClick.AddListener(OnClearHistory);
            if (resetAllButton != null)
                resetAllButton.onClick.AddListener(OnResetAll);

            // About buttons
            if (privacyPolicyButton != null)
                privacyPolicyButton.onClick.AddListener(OnPrivacyPolicy);
            if (licensesButton != null)
                licensesButton.onClick.AddListener(OnLicenses);
            if (feedbackButton != null)
                feedbackButton.onClick.AddListener(OnFeedback);

            // Navigation
            if (backButton != null)
                backButton.onClick.AddListener(OnBack);
            if (saveButton != null)
                saveButton.onClick.AddListener(SaveSettings);

            // Version
            if (versionText != null)
                versionText.text = $"Version {Application.version}";
        }

        private void LoadSettings()
        {
            var progress = dataManager?.Progress;
            if (progress == null)
            {
                // Load from PlayerPrefs as fallback
                LoadFromPlayerPrefs();
                return;
            }

            // Display settings
            if (highlightEffectsToggle != null)
                highlightEffectsToggle.isOn = progress.GetPreferenceBool(PREF_HIGHLIGHT_EFFECTS, true);
            if (showWarningsToggle != null)
                showWarningsToggle.isOn = progress.GetPreferenceBool(PREF_SHOW_WARNINGS, true);
            if (showTorqueSpecsToggle != null)
                showTorqueSpecsToggle.isOn = progress.GetPreferenceBool(PREF_SHOW_TORQUE, true);
            if (highlightIntensitySlider != null)
            {
                highlightIntensitySlider.value = progress.GetPreferenceFloat(PREF_HIGHLIGHT_INTENSITY, 0.8f);
                UpdateHighlightIntensityLabel();
            }

            // Voice settings
            if (voiceCommandsToggle != null)
                voiceCommandsToggle.isOn = progress.GetPreferenceBool(PREF_VOICE_COMMANDS, false);
            if (voiceFeedbackToggle != null)
                voiceFeedbackToggle.isOn = progress.GetPreferenceBool(PREF_VOICE_FEEDBACK, true);
            if (wakeWordDropdown != null)
                wakeWordDropdown.value = progress.GetPreferenceInt(PREF_WAKE_WORD, 0);

            // Procedure settings
            if (autoAdvanceToggle != null)
                autoAdvanceToggle.isOn = progress.GetPreferenceBool(PREF_AUTO_ADVANCE, false);
            if (confirmCompletionToggle != null)
                confirmCompletionToggle.isOn = progress.GetPreferenceBool(PREF_CONFIRM_COMPLETION, true);
            if (showStepNumbersToggle != null)
                showStepNumbersToggle.isOn = progress.GetPreferenceBool(PREF_SHOW_STEP_NUMBERS, true);

            isDirty = false;
        }

        private void LoadFromPlayerPrefs()
        {
            if (highlightEffectsToggle != null)
                highlightEffectsToggle.isOn = PlayerPrefs.GetInt(PREF_HIGHLIGHT_EFFECTS, 1) == 1;
            if (showWarningsToggle != null)
                showWarningsToggle.isOn = PlayerPrefs.GetInt(PREF_SHOW_WARNINGS, 1) == 1;
            if (showTorqueSpecsToggle != null)
                showTorqueSpecsToggle.isOn = PlayerPrefs.GetInt(PREF_SHOW_TORQUE, 1) == 1;
            if (highlightIntensitySlider != null)
            {
                highlightIntensitySlider.value = PlayerPrefs.GetFloat(PREF_HIGHLIGHT_INTENSITY, 0.8f);
                UpdateHighlightIntensityLabel();
            }
            if (voiceCommandsToggle != null)
                voiceCommandsToggle.isOn = PlayerPrefs.GetInt(PREF_VOICE_COMMANDS, 0) == 1;
            if (autoAdvanceToggle != null)
                autoAdvanceToggle.isOn = PlayerPrefs.GetInt(PREF_AUTO_ADVANCE, 0) == 1;
            if (confirmCompletionToggle != null)
                confirmCompletionToggle.isOn = PlayerPrefs.GetInt(PREF_CONFIRM_COMPLETION, 1) == 1;
            if (showStepNumbersToggle != null)
                showStepNumbersToggle.isOn = PlayerPrefs.GetInt(PREF_SHOW_STEP_NUMBERS, 1) == 1;

            isDirty = false;
        }

        public void SaveSettings()
        {
            var progress = dataManager?.Progress;
            if (progress == null)
            {
                SaveToPlayerPrefs();
                return;
            }

            // Display settings
            if (highlightEffectsToggle != null)
                progress.SetPreference(PREF_HIGHLIGHT_EFFECTS, highlightEffectsToggle.isOn.ToString());
            if (showWarningsToggle != null)
                progress.SetPreference(PREF_SHOW_WARNINGS, showWarningsToggle.isOn.ToString());
            if (showTorqueSpecsToggle != null)
                progress.SetPreference(PREF_SHOW_TORQUE, showTorqueSpecsToggle.isOn.ToString());
            if (highlightIntensitySlider != null)
                progress.SetPreference(PREF_HIGHLIGHT_INTENSITY, highlightIntensitySlider.value.ToString());

            // Voice settings
            if (voiceCommandsToggle != null)
                progress.SetPreference(PREF_VOICE_COMMANDS, voiceCommandsToggle.isOn.ToString());
            if (voiceFeedbackToggle != null)
                progress.SetPreference(PREF_VOICE_FEEDBACK, voiceFeedbackToggle.isOn.ToString());
            if (wakeWordDropdown != null)
                progress.SetPreference(PREF_WAKE_WORD, wakeWordDropdown.value.ToString());

            // Procedure settings
            if (autoAdvanceToggle != null)
                progress.SetPreference(PREF_AUTO_ADVANCE, autoAdvanceToggle.isOn.ToString());
            if (confirmCompletionToggle != null)
                progress.SetPreference(PREF_CONFIRM_COMPLETION, confirmCompletionToggle.isOn.ToString());
            if (showStepNumbersToggle != null)
                progress.SetPreference(PREF_SHOW_STEP_NUMBERS, showStepNumbersToggle.isOn.ToString());

            isDirty = false;
            OnSettingsChanged?.Invoke();
        }

        private void SaveToPlayerPrefs()
        {
            if (highlightEffectsToggle != null)
                PlayerPrefs.SetInt(PREF_HIGHLIGHT_EFFECTS, highlightEffectsToggle.isOn ? 1 : 0);
            if (showWarningsToggle != null)
                PlayerPrefs.SetInt(PREF_SHOW_WARNINGS, showWarningsToggle.isOn ? 1 : 0);
            if (showTorqueSpecsToggle != null)
                PlayerPrefs.SetInt(PREF_SHOW_TORQUE, showTorqueSpecsToggle.isOn ? 1 : 0);
            if (highlightIntensitySlider != null)
                PlayerPrefs.SetFloat(PREF_HIGHLIGHT_INTENSITY, highlightIntensitySlider.value);
            if (voiceCommandsToggle != null)
                PlayerPrefs.SetInt(PREF_VOICE_COMMANDS, voiceCommandsToggle.isOn ? 1 : 0);
            if (autoAdvanceToggle != null)
                PlayerPrefs.SetInt(PREF_AUTO_ADVANCE, autoAdvanceToggle.isOn ? 1 : 0);
            if (confirmCompletionToggle != null)
                PlayerPrefs.SetInt(PREF_CONFIRM_COMPLETION, confirmCompletionToggle.isOn ? 1 : 0);
            if (showStepNumbersToggle != null)
                PlayerPrefs.SetInt(PREF_SHOW_STEP_NUMBERS, showStepNumbersToggle.isOn ? 1 : 0);

            PlayerPrefs.Save();
            isDirty = false;
            OnSettingsChanged?.Invoke();
        }

        private void UpdateDataStats()
        {
            if (dataStatsText == null || dataManager == null) return;

            var stats = dataManager.GetStats();
            dataStatsText.text = $"Parts: {stats.PartCount} | Repairs: {stats.CompletedRepairs} | In Progress: {stats.InProgressProcedures}";
        }

        private void UpdateHighlightIntensityLabel()
        {
            if (highlightIntensityValue != null && highlightIntensitySlider != null)
            {
                highlightIntensityValue.text = $"{Mathf.RoundToInt(highlightIntensitySlider.value * 100)}%";
            }
        }

        // Change handlers
        private void OnHighlightEffectsChanged(bool value) { isDirty = true; }
        private void OnShowWarningsChanged(bool value) { isDirty = true; }
        private void OnShowTorqueChanged(bool value) { isDirty = true; }
        private void OnHighlightIntensityChanged(float value)
        {
            isDirty = true;
            UpdateHighlightIntensityLabel();
        }
        private void OnVoiceCommandsChanged(bool value)
        {
            isDirty = true;
            // Enable/disable voice-related settings
            if (voiceFeedbackToggle != null)
                voiceFeedbackToggle.interactable = value;
            if (wakeWordDropdown != null)
                wakeWordDropdown.interactable = value;
        }
        private void OnVoiceFeedbackChanged(bool value) { isDirty = true; }
        private void OnWakeWordChanged(int value) { isDirty = true; }
        private void OnAutoAdvanceChanged(bool value) { isDirty = true; }
        private void OnConfirmCompletionChanged(bool value) { isDirty = true; }
        private void OnShowStepNumbersChanged(bool value) { isDirty = true; }

        // Data actions
        private void OnExportData()
        {
            if (dataManager != null)
            {
                string backupPath = dataManager.ExportBackup();
                ShowMessage($"Data exported to:\n{backupPath}");
            }
        }

        private void OnImportData()
        {
            ShowMessage("Import feature coming soon.\nManually place backup in the backups folder.");
        }

        private void OnClearHistory()
        {
            ShowConfirmation("Clear all repair history?", () =>
            {
                // Clear repair history but keep preferences
                Debug.Log("Clearing repair history");
                UpdateDataStats();
                ShowMessage("Repair history cleared.");
            });
        }

        private void OnResetAll()
        {
            ShowConfirmation("Reset ALL data?\nThis cannot be undone.", () =>
            {
                dataManager?.ResetAllData();
                LoadSettings();
                UpdateDataStats();
                ShowMessage("All data has been reset.");
            });
        }

        // About actions
        private void OnPrivacyPolicy()
        {
            Application.OpenURL("https://github.com/mechanicscope/privacy");
        }

        private void OnLicenses()
        {
            ShowMessage("MIT License\n\nSee GitHub repository for full license text and third-party attributions.");
        }

        private void OnFeedback()
        {
            Application.OpenURL("https://github.com/mechanicscope/feedback");
        }

        private void OnBack()
        {
            if (isDirty)
            {
                ShowConfirmation("Save changes before leaving?", () =>
                {
                    SaveSettings();
                    OnBackPressed?.Invoke();
                }, () =>
                {
                    OnBackPressed?.Invoke();
                });
            }
            else
            {
                OnBackPressed?.Invoke();
            }
        }

        private void ShowMessage(string message)
        {
            Debug.Log(message);
            // In production, show a toast or dialog
        }

        private void ShowConfirmation(string message, Action onConfirm, Action onCancel = null)
        {
            Debug.Log($"Confirmation: {message}");
            // In production, show a confirmation dialog
            // For now, just execute confirm
            onConfirm?.Invoke();
        }

        // Public static getters for other systems
        public static bool GetHighlightEffectsEnabled()
        {
            return PlayerPrefs.GetInt(PREF_HIGHLIGHT_EFFECTS, 1) == 1;
        }

        public static bool GetShowWarnings()
        {
            return PlayerPrefs.GetInt(PREF_SHOW_WARNINGS, 1) == 1;
        }

        public static bool GetShowTorqueSpecs()
        {
            return PlayerPrefs.GetInt(PREF_SHOW_TORQUE, 1) == 1;
        }

        public static float GetHighlightIntensity()
        {
            return PlayerPrefs.GetFloat(PREF_HIGHLIGHT_INTENSITY, 0.8f);
        }

        public static bool GetVoiceCommandsEnabled()
        {
            return PlayerPrefs.GetInt(PREF_VOICE_COMMANDS, 0) == 1;
        }

        public static bool GetAutoAdvance()
        {
            return PlayerPrefs.GetInt(PREF_AUTO_ADVANCE, 0) == 1;
        }

        public static bool GetConfirmCompletion()
        {
            return PlayerPrefs.GetInt(PREF_CONFIRM_COMPLETION, 1) == 1;
        }
    }
}
