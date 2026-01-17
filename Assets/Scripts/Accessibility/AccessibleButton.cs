using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace MechanicScope.Accessibility
{
    /// <summary>
    /// Makes a button accessible by responding to accessibility settings.
    /// Provides larger touch targets, haptic feedback, and screen reader support.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AccessibleButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Settings")]
        [SerializeField] private bool scaleWithSettings = true;
        [SerializeField] private bool useHighContrastColors = true;
        [SerializeField] private bool provideHapticFeedback = true;
        [SerializeField] private HapticType hapticType = HapticType.Light;

        [Header("Touch Target")]
        [SerializeField] private float minimumTouchSize = 44f; // iOS/Android minimum
        [SerializeField] private bool expandTouchTarget = true;

        [Header("Screen Reader")]
        [SerializeField] private string accessibilityLabel;
        [SerializeField] private string accessibilityHint;
        [SerializeField] private AccessibilityRole accessibilityRole = AccessibilityRole.Button;

        [Header("Visual Feedback")]
        [SerializeField] private bool showFocusIndicator = true;
        [SerializeField] private float focusIndicatorWidth = 3f;
        [SerializeField] private Color focusIndicatorColor = Color.yellow;

        private Button button;
        private RectTransform rectTransform;
        private Vector2 originalSize;
        private Image focusIndicator;

        public enum AccessibilityRole
        {
            Button,
            Toggle,
            Link,
            MenuItem,
            Tab
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            rectTransform = GetComponent<RectTransform>();

            if (rectTransform != null)
            {
                originalSize = rectTransform.sizeDelta;
            }

            // Subscribe to button click for haptic feedback
            if (button != null)
            {
                button.onClick.AddListener(OnButtonClicked);
            }
        }

        private void Start()
        {
            if (AccessibilityManager.Instance != null)
            {
                AccessibilityManager.Instance.RegisterButton(this);
            }

            EnsureMinimumTouchTarget();
        }

        private void OnDestroy()
        {
            if (AccessibilityManager.Instance != null)
            {
                AccessibilityManager.Instance.UnregisterButton(this);
            }

            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }
        }

        /// <summary>
        /// Applies current accessibility settings.
        /// </summary>
        public void ApplySettings(AccessibilityManager manager)
        {
            if (scaleWithSettings)
            {
                float scale = manager.GetButtonScaleMultiplier();
                Vector2 newSize = originalSize * scale;

                // Ensure minimum size
                newSize.x = Mathf.Max(newSize.x, minimumTouchSize);
                newSize.y = Mathf.Max(newSize.y, minimumTouchSize);

                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = newSize;
                }
            }

            if (useHighContrastColors && button != null)
            {
                ApplyHighContrastColors(manager);
            }
        }

        private void ApplyHighContrastColors(AccessibilityManager manager)
        {
            if (!manager.HighContrastEnabled) return;

            var colors = button.colors;
            colors.normalColor = manager.GetAccessibleColor(AccessibleColorType.Primary);
            colors.highlightedColor = manager.GetAccessibleColor(AccessibleColorType.Accent);
            colors.pressedColor = manager.GetAccessibleColor(AccessibleColorType.Secondary);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            button.colors = colors;

            // Update button text if present
            var text = GetComponentInChildren<AccessibleText>();
            if (text != null)
            {
                text.SetColorType(AccessibleColorType.Secondary);
            }
        }

        private void EnsureMinimumTouchTarget()
        {
            if (!expandTouchTarget || rectTransform == null) return;

            Vector2 currentSize = rectTransform.sizeDelta;

            if (currentSize.x < minimumTouchSize || currentSize.y < minimumTouchSize)
            {
                // Create invisible hit area expander
                CreateHitAreaExpander();
            }
        }

        private void CreateHitAreaExpander()
        {
            // Check if already exists
            Transform existing = transform.Find("HitAreaExpander");
            if (existing != null) return;

            GameObject expander = new GameObject("HitAreaExpander");
            expander.transform.SetParent(transform, false);

            RectTransform expanderRect = expander.AddComponent<RectTransform>();
            expanderRect.anchorMin = new Vector2(0.5f, 0.5f);
            expanderRect.anchorMax = new Vector2(0.5f, 0.5f);
            expanderRect.pivot = new Vector2(0.5f, 0.5f);
            expanderRect.anchoredPosition = Vector2.zero;
            expanderRect.sizeDelta = new Vector2(minimumTouchSize, minimumTouchSize);

            // Add transparent image for raycast target
            Image expanderImage = expander.AddComponent<Image>();
            expanderImage.color = new Color(0, 0, 0, 0);
            expanderImage.raycastTarget = true;

            // Move to back
            expander.transform.SetAsFirstSibling();
        }

        private void OnButtonClicked()
        {
            if (provideHapticFeedback && AccessibilityManager.Instance != null)
            {
                AccessibilityManager.Instance.TriggerHaptic(hapticType);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (showFocusIndicator && AccessibilityManager.Instance?.HighContrastEnabled == true)
            {
                ShowFocusIndicator();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            HideFocusIndicator();
        }

        private void ShowFocusIndicator()
        {
            if (focusIndicator != null) return;

            GameObject indicator = new GameObject("FocusIndicator");
            indicator.transform.SetParent(transform, false);

            RectTransform indicatorRect = indicator.AddComponent<RectTransform>();
            indicatorRect.anchorMin = Vector2.zero;
            indicatorRect.anchorMax = Vector2.one;
            indicatorRect.sizeDelta = new Vector2(focusIndicatorWidth * 2, focusIndicatorWidth * 2);
            indicatorRect.anchoredPosition = Vector2.zero;

            focusIndicator = indicator.AddComponent<Image>();
            focusIndicator.color = focusIndicatorColor;
            focusIndicator.raycastTarget = false;

            // Make it an outline
            Outline outline = indicator.AddComponent<Outline>();
            outline.effectColor = focusIndicatorColor;
            outline.effectDistance = new Vector2(focusIndicatorWidth, focusIndicatorWidth);

            indicator.transform.SetAsFirstSibling();
        }

        private void HideFocusIndicator()
        {
            if (focusIndicator != null)
            {
                Destroy(focusIndicator.gameObject);
                focusIndicator = null;
            }
        }

        /// <summary>
        /// Gets the full accessibility description for screen readers.
        /// </summary>
        public string GetAccessibilityDescription()
        {
            string label = !string.IsNullOrEmpty(accessibilityLabel)
                ? accessibilityLabel
                : GetButtonText();

            string role = GetRoleDescription();
            string hint = !string.IsNullOrEmpty(accessibilityHint)
                ? $". {accessibilityHint}"
                : "";

            string state = button != null && !button.interactable
                ? ". Disabled"
                : "";

            return $"{label}, {role}{hint}{state}";
        }

        private string GetButtonText()
        {
            var tmpText = GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmpText != null) return tmpText.text;

            var legacyText = GetComponentInChildren<Text>();
            if (legacyText != null) return legacyText.text;

            return gameObject.name;
        }

        private string GetRoleDescription()
        {
            return accessibilityRole switch
            {
                AccessibilityRole.Toggle => "Toggle button",
                AccessibilityRole.Link => "Link",
                AccessibilityRole.MenuItem => "Menu item",
                AccessibilityRole.Tab => "Tab",
                _ => "Button"
            };
        }

        /// <summary>
        /// Sets the accessibility label programmatically.
        /// </summary>
        public void SetAccessibilityLabel(string label)
        {
            accessibilityLabel = label;
        }

        /// <summary>
        /// Sets the accessibility hint programmatically.
        /// </summary>
        public void SetAccessibilityHint(string hint)
        {
            accessibilityHint = hint;
        }

        /// <summary>
        /// Sets the haptic type for button press feedback.
        /// </summary>
        public void SetHapticType(HapticType type)
        {
            hapticType = type;
        }

        /// <summary>
        /// Announces the button for screen readers.
        /// </summary>
        public void AnnounceButton()
        {
            if (AccessibilityManager.Instance != null)
            {
                AccessibilityManager.Instance.AnnounceForScreenReader(GetAccessibilityDescription());
            }
        }
    }
}
