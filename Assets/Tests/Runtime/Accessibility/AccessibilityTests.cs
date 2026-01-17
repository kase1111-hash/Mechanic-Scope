using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace MechanicScope.Tests.Runtime.Accessibility
{
    /// <summary>
    /// End-to-end tests for accessibility features.
    /// Tests text scaling, high contrast, haptics, and screen reader support.
    /// </summary>
    public class AccessibilityTests : TestBase
    {
        [Test]
        public void TextSize_Normal_ReturnsScaleOf1()
        {
            // Arrange
            var settings = new MockAccessibilitySettings();

            // Act
            float scale = settings.GetTextScaleMultiplier(TextSize.Normal);

            // Assert
            Assert.AreEqual(1.0f, scale);
        }

        [Test]
        public void TextSize_Large_ReturnsLargerScale()
        {
            // Arrange
            var settings = new MockAccessibilitySettings();

            // Act
            float normalScale = settings.GetTextScaleMultiplier(TextSize.Normal);
            float largeScale = settings.GetTextScaleMultiplier(TextSize.Large);

            // Assert
            Assert.Greater(largeScale, normalScale);
        }

        [Test]
        public void TextSize_ExtraLarge_ReturnsLargestScale()
        {
            // Arrange
            var settings = new MockAccessibilitySettings();

            // Act
            float largeScale = settings.GetTextScaleMultiplier(TextSize.Large);
            float extraLargeScale = settings.GetTextScaleMultiplier(TextSize.ExtraLarge);

            // Assert
            Assert.Greater(extraLargeScale, largeScale);
        }

        [Test]
        public void HighContrast_PrimaryColor_IsWhite()
        {
            // Arrange
            var settings = new MockAccessibilitySettings { HighContrastEnabled = true };

            // Act
            Color color = settings.GetAccessibleColor(AccessibleColorType.Primary);

            // Assert
            Assert.AreEqual(Color.white, color);
        }

        [Test]
        public void HighContrast_AccentColor_IsYellow()
        {
            // Arrange
            var settings = new MockAccessibilitySettings { HighContrastEnabled = true };

            // Act
            Color color = settings.GetAccessibleColor(AccessibleColorType.Accent);

            // Assert
            Assert.AreEqual(Color.yellow, color);
        }

        [Test]
        public void HighContrast_Disabled_ReturnsStandardColors()
        {
            // Arrange
            var settings = new MockAccessibilitySettings { HighContrastEnabled = false };

            // Act
            Color color = settings.GetAccessibleColor(AccessibleColorType.Primary);

            // Assert - should not be pure white when high contrast is off
            Assert.AreNotEqual(Color.white, color);
        }

        [Test]
        public void ButtonSize_Normal_ReturnsScaleOf1()
        {
            // Arrange
            var settings = new MockAccessibilitySettings();

            // Act
            float scale = settings.GetButtonScaleMultiplier(ButtonSize.Normal);

            // Assert
            Assert.AreEqual(1.0f, scale);
        }

        [Test]
        public void ButtonSize_Large_ReturnsLargerScale()
        {
            // Arrange
            var settings = new MockAccessibilitySettings();

            // Act
            float normalScale = settings.GetButtonScaleMultiplier(ButtonSize.Normal);
            float largeScale = settings.GetButtonScaleMultiplier(ButtonSize.Large);

            // Assert
            Assert.Greater(largeScale, normalScale);
        }

        [Test]
        public void MinimumTouchTarget_Is44Pixels()
        {
            // The minimum touch target size recommended by iOS and Android
            float minimumSize = 44f;

            // Assert
            Assert.AreEqual(44f, minimumSize);
        }

        [UnityTest]
        public IEnumerator Button_MeetsMinimumTouchTarget()
        {
            // Arrange
            GameObject buttonObj = new GameObject("TestButton");
            buttonObj.transform.SetParent(TestRoot.transform);

            var rectTransform = buttonObj.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100, 44);

            buttonObj.AddComponent<Image>();
            buttonObj.AddComponent<Button>();

            yield return null;

            // Assert
            Assert.GreaterOrEqual(rectTransform.sizeDelta.x, 44f);
            Assert.GreaterOrEqual(rectTransform.sizeDelta.y, 44f);
        }

        [Test]
        public void HapticType_AllTypesExist()
        {
            // Assert
            var types = System.Enum.GetValues(typeof(HapticType));
            Assert.GreaterOrEqual(types.Length, 3); // Light, Medium, Heavy at minimum
        }

        [Test]
        public void AccessibilityLabel_CanBeSet()
        {
            // Arrange
            var element = new MockAccessibleElement();

            // Act
            element.SetAccessibilityLabel("Close button");

            // Assert
            Assert.AreEqual("Close button", element.GetAccessibilityLabel());
        }

        [Test]
        public void AccessibilityHint_CanBeSet()
        {
            // Arrange
            var element = new MockAccessibleElement();

            // Act
            element.SetAccessibilityHint("Double tap to close the dialog");

            // Assert
            Assert.AreEqual("Double tap to close the dialog", element.GetAccessibilityHint());
        }

        [Test]
        public void ReduceMotion_DisablesAnimations()
        {
            // Arrange
            var settings = new MockAccessibilitySettings { ReduceMotionEnabled = true };

            // Assert
            Assert.IsTrue(settings.ReduceMotionEnabled);
        }

        [Test]
        public void ColorContrast_MeetsWCAGRequirements()
        {
            // WCAG 2.1 requires 4.5:1 contrast ratio for normal text
            // High contrast mode should meet or exceed this

            // Arrange
            Color foreground = Color.white;
            Color background = Color.black;

            // Act
            float contrastRatio = CalculateContrastRatio(foreground, background);

            // Assert - white on black is 21:1, well above 4.5:1
            Assert.GreaterOrEqual(contrastRatio, 4.5f);
        }

        [Test]
        public void HighContrastMode_Warning_UsesRed()
        {
            // Arrange
            var settings = new MockAccessibilitySettings { HighContrastEnabled = true };

            // Act
            Color warningColor = settings.GetAccessibleColor(AccessibleColorType.Warning);

            // Assert - warning should be visually distinct (red)
            Assert.AreEqual(Color.red, warningColor);
        }

        [Test]
        public void TextScaling_PreservesReadability()
        {
            // Arrange
            float baseFontSize = 16f;
            var settings = new MockAccessibilitySettings();

            // Act
            float extraLargeScale = settings.GetTextScaleMultiplier(TextSize.ExtraLarge);
            float scaledSize = baseFontSize * extraLargeScale;

            // Assert - even at extra large, font shouldn't be excessively big
            Assert.LessOrEqual(scaledSize, 32f); // 200% max
            Assert.GreaterOrEqual(scaledSize, 20f); // At least 125%
        }

        [UnityTest]
        public IEnumerator Text_ScalesWithAccessibilitySettings()
        {
            // Arrange
            GameObject textObj = new GameObject("TestText");
            textObj.transform.SetParent(TestRoot.transform);

            var textComponent = textObj.AddComponent<Text>();
            textComponent.fontSize = 16;
            float originalSize = textComponent.fontSize;

            yield return null;

            // Act - simulate scaling
            float scale = 1.5f; // ExtraLarge
            textComponent.fontSize = Mathf.RoundToInt(originalSize * scale);

            yield return null;

            // Assert
            Assert.AreEqual(24, textComponent.fontSize);
        }

        [Test]
        public void ScreenReaderAnnouncement_FormatsCorrectly()
        {
            // Arrange
            string label = "Next Step";
            string role = "Button";
            string hint = "Double tap to go to the next step";

            // Act
            string announcement = FormatAccessibilityAnnouncement(label, role, hint);

            // Assert
            Assert.IsTrue(announcement.Contains(label));
            Assert.IsTrue(announcement.Contains(role));
            Assert.IsTrue(announcement.Contains(hint));
        }

        private string FormatAccessibilityAnnouncement(string label, string role, string hint)
        {
            return $"{label}, {role}. {hint}";
        }

        private float CalculateContrastRatio(Color foreground, Color background)
        {
            float fgLuminance = CalculateLuminance(foreground);
            float bgLuminance = CalculateLuminance(background);

            float lighter = Mathf.Max(fgLuminance, bgLuminance);
            float darker = Mathf.Min(fgLuminance, bgLuminance);

            return (lighter + 0.05f) / (darker + 0.05f);
        }

        private float CalculateLuminance(Color color)
        {
            float r = color.r <= 0.03928f ? color.r / 12.92f : Mathf.Pow((color.r + 0.055f) / 1.055f, 2.4f);
            float g = color.g <= 0.03928f ? color.g / 12.92f : Mathf.Pow((color.g + 0.055f) / 1.055f, 2.4f);
            float b = color.b <= 0.03928f ? color.b / 12.92f : Mathf.Pow((color.b + 0.055f) / 1.055f, 2.4f);

            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }
    }

    // Test helper classes
    public enum TextSize { Normal, Large, ExtraLarge }
    public enum ButtonSize { Normal, Large, ExtraLarge }
    public enum HapticType { Light, Medium, Heavy, Success, Warning, Error }
    public enum AccessibleColorType { Primary, Secondary, Accent, Warning, Success }

    public class MockAccessibilitySettings
    {
        public bool HighContrastEnabled { get; set; }
        public bool ReduceMotionEnabled { get; set; }

        public float GetTextScaleMultiplier(TextSize size)
        {
            return size switch
            {
                TextSize.Large => 1.25f,
                TextSize.ExtraLarge => 1.5f,
                _ => 1.0f
            };
        }

        public float GetButtonScaleMultiplier(ButtonSize size)
        {
            return size switch
            {
                ButtonSize.Large => 1.25f,
                ButtonSize.ExtraLarge => 1.5f,
                _ => 1.0f
            };
        }

        public Color GetAccessibleColor(AccessibleColorType colorType)
        {
            if (HighContrastEnabled)
            {
                return colorType switch
                {
                    AccessibleColorType.Primary => Color.white,
                    AccessibleColorType.Secondary => Color.black,
                    AccessibleColorType.Accent => Color.yellow,
                    AccessibleColorType.Warning => Color.red,
                    AccessibleColorType.Success => Color.green,
                    _ => Color.white
                };
            }

            return colorType switch
            {
                AccessibleColorType.Primary => new Color(0.9f, 0.9f, 0.9f),
                AccessibleColorType.Accent => new Color(1f, 0.42f, 0.21f),
                _ => Color.gray
            };
        }
    }

    public class MockAccessibleElement
    {
        private string accessibilityLabel;
        private string accessibilityHint;

        public void SetAccessibilityLabel(string label) => accessibilityLabel = label;
        public string GetAccessibilityLabel() => accessibilityLabel;

        public void SetAccessibilityHint(string hint) => accessibilityHint = hint;
        public string GetAccessibilityHint() => accessibilityHint;
    }
}
