// TextMeshPro stand-ins for the headless test harness.
//
// Compile-check support only — no text is measured or rendered. Property names and shapes mirror
// the real TMP API so that a typo or a wrong member in project code still fails the build here.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TMPro
{
    public enum TextAlignmentOptions
    {
        Left, Center, Right, Justified, Flush,
        TopLeft, Top, TopRight, TopJustified,
        MidlineLeft, Midline, MidlineRight,
        BottomLeft, Bottom, BottomRight,
        Baseline, Capline
    }

    public enum TextOverflowModes { Overflow, Ellipsis, Masking, Truncate, ScrollRect, Page, Linked }
    public enum FontStyles { Normal = 0, Bold = 1, Italic = 2, Underline = 4, LowerCase = 8, UpperCase = 16, SmallCaps = 32, Strikethrough = 64 }

    public class TMP_FontAsset : ScriptableObject { }

    public class ScriptableObject : UnityEngine.Object { }

    public class TMP_Text : MaskableGraphic
    {
        public string text { get; set; } = "";
        public float fontSize { get; set; } = 36f;
        public float fontSizeMin { get; set; }
        public float fontSizeMax { get; set; }
        public bool enableAutoSizing { get; set; }
        public bool enableWordWrapping { get; set; } = true;
        public bool richText { get; set; } = true;
        public FontStyles fontStyle { get; set; }
        public TextAlignmentOptions alignment { get; set; }
        public TextOverflowModes overflowMode { get; set; }
        public TMP_FontAsset font { get; set; }
        public float characterSpacing { get; set; }
        public float lineSpacing { get; set; }
        public int maxVisibleCharacters { get; set; } = int.MaxValue;
        public Vector2 margin { get; set; }
        public float preferredWidth => 0f;
        public float preferredHeight => 0f;

        public void ForceMeshUpdate() { }
        public void ForceMeshUpdate(bool ignoreActiveState) { }
        public void SetText(string value) => text = value;
        public void SetText(string format, float arg0) => text = string.Format(format, arg0);
    }

    public class TextMeshProUGUI : TMP_Text { }

    public class TextMeshPro : TMP_Text { }

    public class TMP_InputField : Selectable
    {
        public class OnChangeEvent : UnityEvent<string> { }
        public class SubmitEvent : UnityEvent<string> { }
        public class SelectionEvent : UnityEvent<string> { }

        public enum ContentType { Standard, Autocorrected, IntegerNumber, DecimalNumber, Alphanumeric, Name, EmailAddress, Password, Pin, Custom }

        public string text { get; set; } = "";
        public ContentType contentType { get; set; }
        public int characterLimit { get; set; }
        public TMP_Text textComponent { get; set; }
        public TMP_Text placeholder { get; set; }
        public OnChangeEvent onValueChanged { get; set; } = new OnChangeEvent();
        public SubmitEvent onEndEdit { get; set; } = new SubmitEvent();
        public SelectionEvent onSubmit { get; set; } = new SelectionEvent();

        public void ActivateInputField() { }
        public void DeactivateInputField() { }
    }

    public class TMP_Dropdown : Selectable
    {
        [Serializable]
        public class OptionData
        {
            public string text;
            public Sprite image;
            public OptionData() { }
            public OptionData(string text) { this.text = text; }
            public OptionData(string text, Sprite image) { this.text = text; this.image = image; }
        }

        public class DropdownEvent : UnityEvent<int> { }

        public int value { get; set; }
        public List<OptionData> options { get; set; } = new List<OptionData>();
        public TMP_Text captionText { get; set; }
        public TMP_Text itemText { get; set; }
        public DropdownEvent onValueChanged { get; set; } = new DropdownEvent();

        public void ClearOptions() => options.Clear();
        public void AddOptions(List<string> texts) => options.AddRange(texts.ConvertAll(t => new OptionData(t)));
        public void AddOptions(List<OptionData> newOptions) => options.AddRange(newOptions);
        public void RefreshShownValue() { }
    }
}
