using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MechanicScope.Accessibility
{
    /// <summary>
    /// UI panel for accessibility settings.
    /// Allows users to configure text size, high contrast, haptics, etc.
    /// </summary>
    public class AccessibilitySettingsUI : MonoBehaviour
    {
        [Header("Text Size")]
        [SerializeField] private TMP_Dropdown textSizeDropdown;
        [SerializeField] private TextMeshProUGUI textSizePreview;

        [Header("Display")]
        [SerializeField] private Toggle highContrastToggle;
        [SerializeField] private Toggle reduceMotionToggle;
        [SerializeField] private Image highContrastPreview;

        [Header("Button Size")]
        [SerializeField] private TMP_Dropdown buttonSizeDropdown;

        [Header("Feedback")]
        [SerializeField] private Toggle hapticsToggle;
        [SerializeField] private Button testHapticButton;

        [Header("Screen Reader")]
        [SerializeField] private Toggle screenReaderToggle;
        [SerializeField] private TextMeshProUGUI screenReaderStatus;

        [Header("Navigation")]
        [SerializeField] private Button backButton;
        [SerializeField] private Button resetButton;

        public event Action OnBackPressed;

        private void Start()
        {
            SetupUI();
            LoadCurrentSettings();
        }

        private void SetupUI()
        {
            // Text size dropdown
            if (textSizeDropdown != null)
            {
                textSizeDropdown.ClearOptions();
                textSizeDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "Normal",
                    "Large (125%)",
                    "Extra Large (150%)"
                });
                textSizeDropdown.onValueChanged.AddListener(OnTextSizeChanged);
            }

            // Button size dropdown
            if (buttonSizeDropdown != null)
            {
                buttonSizeDropdown.ClearOptions();
                buttonSizeDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "Normal",
                    "Large",
                    "Extra Large"
                });
                buttonSizeDropdown.onValueChanged.AddListener(OnButtonSizeChanged);
            }

            // Toggles
            if (highContrastToggle != null)
                highContrastToggle.onValueChanged.AddListener(OnHighContrastChanged);

            if (reduceMotionToggle != null)
                reduceMotionToggle.onValueChanged.AddListener(OnReduceMotionChanged);

            if (hapticsToggle != null)
                hapticsToggle.onValueChanged.AddListener(OnHapticsChanged);

            if (screenReaderToggle != null)
                screenReaderToggle.onValueChanged.AddListener(OnScreenReaderChanged);

            // Buttons
            if (testHapticButton != null)
                testHapticButton.onClick.AddListener(OnTestHaptic);

            if (backButton != null)
                backButton.onClick.AddListener(OnBack);

            if (resetButton != null)
                resetButton.onClick.AddListener(OnReset);
        }

        private void LoadCurrentSettings()
        {
            var manager = AccessibilityManager.Instance;
            if (manager == null) return;

            if (textSizeDropdown != null)
                textSizeDropdown.value = (int)manager.CurrentTextSize;

            if (buttonSizeDropdown != null)
                buttonSizeDropdown.value = (int)manager.CurrentButtonSize;

            if (highContrastToggle != null)
                highContrastToggle.isOn = manager.HighContrastEnabled;

            if (reduceMotionToggle != null)
                reduceMotionToggle.isOn = manager.ReduceMotionEnabled;

            if (hapticsToggle != null)
                hapticsToggle.isOn = manager.HapticsEnabled;

            if (screenReaderToggle != null)
                screenReaderToggle.isOn = manager.ScreenReaderEnabled;

            UpdatePreviews();
            UpdateScreenReaderStatus();
        }

        private void OnTextSizeChanged(int value)
        {
            var manager = AccessibilityManager.Instance;
            if (manager != null)
            {
                manager.SetTextSize((AccessibilityManager.TextSize)value);
                UpdateTextPreview();

                // Announce change
                string[] sizes = { "Normal", "Large", "Extra Large" };
                manager.AnnounceForScreenReader($"Text size changed to {sizes[value]}");
            }
        }

        private void OnButtonSizeChanged(int value)
        {
            var manager = AccessibilityManager.Instance;
            if (manager != null)
            {
                manager.SetButtonSize((AccessibilityManager.ButtonSize)value);

                string[] sizes = { "Normal", "Large", "Extra Large" };
                manager.AnnounceForScreenReader($"Button size changed to {sizes[value]}");
            }
        }

        private void OnHighContrastChanged(bool value)
        {
            var manager = AccessibilityManager.Instance;
            if (manager != null)
            {
                manager.SetHighContrast(value);
                UpdateHighContrastPreview();

                manager.AnnounceForScreenReader($"High contrast mode {(value ? "enabled" : "disabled")}");
            }
        }

        private void OnReduceMotionChanged(bool value)
        {
            var manager = AccessibilityManager.Instance;
            if (manager != null)
            {
                manager.SetReduceMotion(value);

                manager.AnnounceForScreenReader($"Reduce motion {(value ? "enabled" : "disabled")}");
            }
        }

        private void OnHapticsChanged(bool value)
        {
            var manager = AccessibilityManager.Instance;
            if (manager != null)
            {
                manager.SetHapticsEnabled(value);

                if (value)
                {
                    // Give feedback that haptics are now on
                    manager.TriggerHaptic(HapticType.Success);
                }
            }
        }

        private void OnScreenReaderChanged(bool value)
        {
            // Note: In production, this would integrate with system settings
            UpdateScreenReaderStatus();

            if (value)
            {
                AccessibilityManager.Instance?.AnnounceForScreenReader("Screen reader support enabled");
            }
        }

        private void OnTestHaptic()
        {
            var manager = AccessibilityManager.Instance;
            if (manager != null && manager.HapticsEnabled)
            {
                manager.TriggerHaptic(HapticType.Medium);
            }
        }

        private void OnBack()
        {
            OnBackPressed?.Invoke();
        }

        private void OnReset()
        {
            var manager = AccessibilityManager.Instance;
            if (manager == null) return;

            // Reset to defaults
            manager.SetTextSize(AccessibilityManager.TextSize.Normal);
            manager.SetButtonSize(AccessibilityManager.ButtonSize.Normal);
            manager.SetHighContrast(false);
            manager.SetReduceMotion(false);
            manager.SetHapticsEnabled(true);

            // Reload UI
            LoadCurrentSettings();

            manager.AnnounceForScreenReader("Accessibility settings reset to defaults");
        }

        private void UpdatePreviews()
        {
            UpdateTextPreview();
            UpdateHighContrastPreview();
        }

        private void UpdateTextPreview()
        {
            if (textSizePreview == null || AccessibilityManager.Instance == null) return;

            float scale = AccessibilityManager.Instance.GetTextScaleMultiplier();
            textSizePreview.fontSize = 16 * scale;
            textSizePreview.text = $"Sample text at {Mathf.RoundToInt(scale * 100)}%";
        }

        private void UpdateHighContrastPreview()
        {
            if (highContrastPreview == null || AccessibilityManager.Instance == null) return;

            if (AccessibilityManager.Instance.HighContrastEnabled)
            {
                highContrastPreview.color = AccessibilityManager.Instance.GetAccessibleColor(AccessibleColorType.Accent);
            }
            else
            {
                highContrastPreview.color = new Color(1f, 0.42f, 0.21f); // Default accent
            }
        }

        private void UpdateScreenReaderStatus()
        {
            if (screenReaderStatus == null) return;

            bool systemScreenReaderActive = IsSystemScreenReaderActive();

            if (systemScreenReaderActive)
            {
                screenReaderStatus.text = "System screen reader detected";
                screenReaderStatus.color = Color.green;
            }
            else
            {
                screenReaderStatus.text = "No screen reader detected";
                screenReaderStatus.color = Color.gray;
            }
        }

        private bool IsSystemScreenReaderActive()
        {
            #if UNITY_IOS && !UNITY_EDITOR
            // Check for VoiceOver
            return UnityEngine.iOS.Device.voiceOverActive;
            #elif UNITY_ANDROID && !UNITY_EDITOR
            // Check for TalkBack (simplified)
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var accessibilityManager = activity.Call<AndroidJavaObject>("getSystemService", "accessibility"))
                {
                    return accessibilityManager.Call<bool>("isEnabled");
                }
            }
            catch
            {
                return false;
            }
            #else
            return false;
            #endif
        }
    }
}
