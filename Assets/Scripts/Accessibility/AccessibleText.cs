using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MechanicScope.Accessibility
{
    /// <summary>
    /// Makes a text element accessible by responding to accessibility settings.
    /// Attach to any GameObject with Text or TextMeshProUGUI.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class AccessibleText : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool scaleWithSettings = true;
        [SerializeField] private bool useHighContrastColors = true;
        [SerializeField] private AccessibleColorType colorType = AccessibleColorType.Primary;

        [Header("Screen Reader")]
        [SerializeField] private string accessibilityLabel;
        [SerializeField] private string accessibilityHint;
        [SerializeField] private bool announceOnChange = false;

        private Text legacyText;
        private TextMeshProUGUI tmpText;
        private float originalFontSize;
        private Color originalColor;
        private string lastText;

        private void Awake()
        {
            legacyText = GetComponent<Text>();
            tmpText = GetComponent<TextMeshProUGUI>();

            if (tmpText != null)
            {
                originalFontSize = tmpText.fontSize;
                originalColor = tmpText.color;
            }
            else if (legacyText != null)
            {
                originalFontSize = legacyText.fontSize;
                originalColor = legacyText.color;
            }
        }

        private void Start()
        {
            if (AccessibilityManager.Instance != null)
            {
                AccessibilityManager.Instance.RegisterText(this);
            }
        }

        private void OnDestroy()
        {
            if (AccessibilityManager.Instance != null)
            {
                AccessibilityManager.Instance.UnregisterText(this);
            }
        }

        private void Update()
        {
            if (announceOnChange)
            {
                string currentText = GetCurrentText();
                if (currentText != lastText)
                {
                    lastText = currentText;
                    AnnounceChange();
                }
            }
        }

        /// <summary>
        /// Applies current accessibility settings.
        /// </summary>
        public void ApplySettings(AccessibilityManager manager)
        {
            if (scaleWithSettings)
            {
                float scale = manager.GetTextScaleMultiplier();
                float newSize = originalFontSize * scale;

                if (tmpText != null)
                {
                    tmpText.fontSize = newSize;
                }
                else if (legacyText != null)
                {
                    legacyText.fontSize = Mathf.RoundToInt(newSize);
                }
            }

            if (useHighContrastColors)
            {
                Color color = manager.GetAccessibleColor(colorType);

                if (tmpText != null)
                {
                    tmpText.color = color;
                }
                else if (legacyText != null)
                {
                    legacyText.color = color;
                }
            }
        }

        /// <summary>
        /// Gets the accessibility label for screen readers.
        /// </summary>
        public string GetAccessibilityLabel()
        {
            if (!string.IsNullOrEmpty(accessibilityLabel))
            {
                return accessibilityLabel;
            }

            return GetCurrentText();
        }

        /// <summary>
        /// Gets the accessibility hint for screen readers.
        /// </summary>
        public string GetAccessibilityHint()
        {
            return accessibilityHint;
        }

        /// <summary>
        /// Sets the accessibility label programmatically.
        /// </summary>
        public void SetAccessibilityLabel(string label)
        {
            accessibilityLabel = label;
        }

        private string GetCurrentText()
        {
            if (tmpText != null)
            {
                return tmpText.text;
            }
            else if (legacyText != null)
            {
                return legacyText.text;
            }
            return string.Empty;
        }

        private void AnnounceChange()
        {
            if (AccessibilityManager.Instance != null)
            {
                string announcement = GetAccessibilityLabel();
                if (!string.IsNullOrEmpty(announcement))
                {
                    AccessibilityManager.Instance.AnnounceForScreenReader(announcement);
                }
            }
        }

        /// <summary>
        /// Sets the color type for high contrast mode.
        /// </summary>
        public void SetColorType(AccessibleColorType type)
        {
            colorType = type;
            if (AccessibilityManager.Instance != null)
            {
                ApplySettings(AccessibilityManager.Instance);
            }
        }

        /// <summary>
        /// Resets to original font size and color.
        /// </summary>
        public void ResetToOriginal()
        {
            if (tmpText != null)
            {
                tmpText.fontSize = originalFontSize;
                tmpText.color = originalColor;
            }
            else if (legacyText != null)
            {
                legacyText.fontSize = Mathf.RoundToInt(originalFontSize);
                legacyText.color = originalColor;
            }
        }
    }
}
