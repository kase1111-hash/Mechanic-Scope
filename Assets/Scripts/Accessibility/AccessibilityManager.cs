using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MechanicScope.Accessibility
{
    /// <summary>
    /// Manages accessibility settings and provides utilities for accessible UI.
    /// Handles high contrast mode, text scaling, screen reader support, and haptics.
    /// </summary>
    public class AccessibilityManager : MonoBehaviour
    {
        public static AccessibilityManager Instance { get; private set; }

        [Header("Text Settings")]
        [SerializeField] private float defaultTextScale = 1.0f;
        [SerializeField] private float largeTextScale = 1.25f;
        [SerializeField] private float extraLargeTextScale = 1.5f;

        [Header("Color Settings")]
        [SerializeField] private Color highContrastPrimary = Color.white;
        [SerializeField] private Color highContrastSecondary = Color.black;
        [SerializeField] private Color highContrastAccent = Color.yellow;
        [SerializeField] private Color highContrastWarning = Color.red;

        [Header("Haptic Settings")]
        [SerializeField] private bool hapticsEnabled = true;
        [SerializeField] private float hapticIntensity = 1.0f;

        // Preference keys
        private const string PREF_TEXT_SIZE = "accessibility_text_size";
        private const string PREF_HIGH_CONTRAST = "accessibility_high_contrast";
        private const string PREF_REDUCE_MOTION = "accessibility_reduce_motion";
        private const string PREF_HAPTICS_ENABLED = "accessibility_haptics";
        private const string PREF_SCREEN_READER = "accessibility_screen_reader";
        private const string PREF_BUTTON_SIZE = "accessibility_button_size";

        // Events
        public event Action OnSettingsChanged;
        public event Action<TextSize> OnTextSizeChanged;
        public event Action<bool> OnHighContrastChanged;
        public event Action<bool> OnReduceMotionChanged;

        // Current settings
        public TextSize CurrentTextSize { get; private set; } = TextSize.Normal;
        public bool HighContrastEnabled { get; private set; }
        public bool ReduceMotionEnabled { get; private set; }
        public bool HapticsEnabled => hapticsEnabled;
        public bool ScreenReaderEnabled { get; private set; }
        public ButtonSize CurrentButtonSize { get; private set; } = ButtonSize.Normal;

        public enum TextSize
        {
            Normal,
            Large,
            ExtraLarge
        }

        public enum ButtonSize
        {
            Normal,
            Large,
            ExtraLarge
        }

        private List<AccessibleText> registeredTexts = new List<AccessibleText>();
        private List<AccessibleButton> registeredButtons = new List<AccessibleButton>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadSettings();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void LoadSettings()
        {
            CurrentTextSize = (TextSize)PlayerPrefs.GetInt(PREF_TEXT_SIZE, 0);
            HighContrastEnabled = PlayerPrefs.GetInt(PREF_HIGH_CONTRAST, 0) == 1;
            ReduceMotionEnabled = PlayerPrefs.GetInt(PREF_REDUCE_MOTION, 0) == 1;
            hapticsEnabled = PlayerPrefs.GetInt(PREF_HAPTICS_ENABLED, 1) == 1;
            ScreenReaderEnabled = PlayerPrefs.GetInt(PREF_SCREEN_READER, 0) == 1;
            CurrentButtonSize = (ButtonSize)PlayerPrefs.GetInt(PREF_BUTTON_SIZE, 0);

            // Check system accessibility settings
            DetectSystemAccessibilitySettings();
        }

        private void DetectSystemAccessibilitySettings()
        {
            // Check if system has accessibility features enabled
            #if UNITY_IOS && !UNITY_EDITOR
            // iOS accessibility detection would go here
            // VoiceOver, Bold Text, Larger Text, Reduce Motion
            #elif UNITY_ANDROID && !UNITY_EDITOR
            // Android accessibility detection
            // TalkBack, Font Size, etc.
            #endif
        }

        /// <summary>
        /// Sets the text size for all registered accessible text elements.
        /// </summary>
        public void SetTextSize(TextSize size)
        {
            CurrentTextSize = size;
            PlayerPrefs.SetInt(PREF_TEXT_SIZE, (int)size);
            PlayerPrefs.Save();

            ApplyTextSize();
            OnTextSizeChanged?.Invoke(size);
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Enables or disables high contrast mode.
        /// </summary>
        public void SetHighContrast(bool enabled)
        {
            HighContrastEnabled = enabled;
            PlayerPrefs.SetInt(PREF_HIGH_CONTRAST, enabled ? 1 : 0);
            PlayerPrefs.Save();

            ApplyHighContrast();
            OnHighContrastChanged?.Invoke(enabled);
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Enables or disables reduced motion mode.
        /// </summary>
        public void SetReduceMotion(bool enabled)
        {
            ReduceMotionEnabled = enabled;
            PlayerPrefs.SetInt(PREF_REDUCE_MOTION, enabled ? 1 : 0);
            PlayerPrefs.Save();

            OnReduceMotionChanged?.Invoke(enabled);
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Enables or disables haptic feedback.
        /// </summary>
        public void SetHapticsEnabled(bool enabled)
        {
            hapticsEnabled = enabled;
            PlayerPrefs.SetInt(PREF_HAPTICS_ENABLED, enabled ? 1 : 0);
            PlayerPrefs.Save();

            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Sets the button size for all registered accessible buttons.
        /// </summary>
        public void SetButtonSize(ButtonSize size)
        {
            CurrentButtonSize = size;
            PlayerPrefs.SetInt(PREF_BUTTON_SIZE, (int)size);
            PlayerPrefs.Save();

            ApplyButtonSize();
            OnSettingsChanged?.Invoke();
        }

        /// <summary>
        /// Gets the text scale multiplier for the current text size setting.
        /// </summary>
        public float GetTextScaleMultiplier()
        {
            return CurrentTextSize switch
            {
                TextSize.Large => largeTextScale,
                TextSize.ExtraLarge => extraLargeTextScale,
                _ => defaultTextScale
            };
        }

        /// <summary>
        /// Gets the button scale multiplier for the current button size setting.
        /// </summary>
        public float GetButtonScaleMultiplier()
        {
            return CurrentButtonSize switch
            {
                ButtonSize.Large => 1.25f,
                ButtonSize.ExtraLarge => 1.5f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Gets the appropriate color for high contrast mode.
        /// </summary>
        public Color GetAccessibleColor(AccessibleColorType colorType)
        {
            if (!HighContrastEnabled)
            {
                return colorType switch
                {
                    AccessibleColorType.Primary => Color.white,
                    AccessibleColorType.Secondary => new Color(0.7f, 0.7f, 0.7f),
                    AccessibleColorType.Accent => new Color(1f, 0.42f, 0.21f),
                    AccessibleColorType.Warning => new Color(0.96f, 0.26f, 0.21f),
                    AccessibleColorType.Success => new Color(0.3f, 0.69f, 0.31f),
                    _ => Color.white
                };
            }

            return colorType switch
            {
                AccessibleColorType.Primary => highContrastPrimary,
                AccessibleColorType.Secondary => highContrastSecondary,
                AccessibleColorType.Accent => highContrastAccent,
                AccessibleColorType.Warning => highContrastWarning,
                AccessibleColorType.Success => Color.green,
                _ => highContrastPrimary
            };
        }

        /// <summary>
        /// Registers a text element for accessibility management.
        /// </summary>
        public void RegisterText(AccessibleText text)
        {
            if (!registeredTexts.Contains(text))
            {
                registeredTexts.Add(text);
                text.ApplySettings(this);
            }
        }

        /// <summary>
        /// Unregisters a text element.
        /// </summary>
        public void UnregisterText(AccessibleText text)
        {
            registeredTexts.Remove(text);
        }

        /// <summary>
        /// Registers a button for accessibility management.
        /// </summary>
        public void RegisterButton(AccessibleButton button)
        {
            if (!registeredButtons.Contains(button))
            {
                registeredButtons.Add(button);
                button.ApplySettings(this);
            }
        }

        /// <summary>
        /// Unregisters a button.
        /// </summary>
        public void UnregisterButton(AccessibleButton button)
        {
            registeredButtons.Remove(button);
        }

        private void ApplyTextSize()
        {
            foreach (var text in registeredTexts)
            {
                if (text != null)
                {
                    text.ApplySettings(this);
                }
            }

            // Clean up null references
            registeredTexts.RemoveAll(t => t == null);
        }

        private void ApplyHighContrast()
        {
            foreach (var text in registeredTexts)
            {
                if (text != null)
                {
                    text.ApplySettings(this);
                }
            }

            foreach (var button in registeredButtons)
            {
                if (button != null)
                {
                    button.ApplySettings(this);
                }
            }
        }

        private void ApplyButtonSize()
        {
            foreach (var button in registeredButtons)
            {
                if (button != null)
                {
                    button.ApplySettings(this);
                }
            }

            registeredButtons.RemoveAll(b => b == null);
        }

        /// <summary>
        /// Triggers haptic feedback if enabled.
        /// </summary>
        public void TriggerHaptic(HapticType type)
        {
            if (!hapticsEnabled) return;

            #if UNITY_IOS && !UNITY_EDITOR
            TriggerIOSHaptic(type);
            #elif UNITY_ANDROID && !UNITY_EDITOR
            TriggerAndroidHaptic(type);
            #endif
        }

        #if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _TriggerHaptic(int type);

        private void TriggerIOSHaptic(HapticType type)
        {
            _TriggerHaptic((int)type);
        }
        #endif

        #if UNITY_ANDROID && !UNITY_EDITOR
        private void TriggerAndroidHaptic(HapticType type)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                {
                    long duration = type switch
                    {
                        HapticType.Light => 10,
                        HapticType.Medium => 25,
                        HapticType.Heavy => 50,
                        HapticType.Success => 30,
                        HapticType.Warning => 40,
                        HapticType.Error => 100,
                        _ => 20
                    };

                    vibrator.Call("vibrate", duration);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Android haptic failed: {e.Message}");
            }
        }
        #endif

        /// <summary>
        /// Announces text for screen readers.
        /// </summary>
        public void AnnounceForScreenReader(string text, bool interrupt = false)
        {
            if (!ScreenReaderEnabled) return;

            #if UNITY_IOS && !UNITY_EDITOR
            AnnounceForVoiceOver(text, interrupt);
            #elif UNITY_ANDROID && !UNITY_EDITOR
            AnnounceForTalkBack(text, interrupt);
            #else
            Debug.Log($"[ScreenReader] {text}");
            #endif
        }

        #if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void _AnnounceForVoiceOver(string text, bool interrupt);

        private void AnnounceForVoiceOver(string text, bool interrupt)
        {
            _AnnounceForVoiceOver(text, interrupt);
        }
        #endif

        #if UNITY_ANDROID && !UNITY_EDITOR
        private void AnnounceForTalkBack(string text, bool interrupt)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var view = activity.Call<AndroidJavaObject>("getWindow").Call<AndroidJavaObject>("getDecorView"))
                {
                    int type = interrupt ? 1 : 0; // TYPE_ANNOUNCEMENT
                    view.Call("announceForAccessibility", text);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"TalkBack announcement failed: {e.Message}");
            }
        }
        #endif
    }

    public enum AccessibleColorType
    {
        Primary,
        Secondary,
        Accent,
        Warning,
        Success
    }

    public enum HapticType
    {
        Light,
        Medium,
        Heavy,
        Success,
        Warning,
        Error
    }
}
