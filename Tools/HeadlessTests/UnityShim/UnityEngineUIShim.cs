// UnityEngine.UI and UnityEngine.EventSystems stand-ins for the headless test harness.
//
// These exist so the UI and Accessibility scripts can be compile-checked. Nothing here draws or
// lays out anything; the value is catching type and member errors that would otherwise break the
// Unity build. UnityEvent-style hooks are real enough to add listeners and raise them, so logic
// wired through them stays verifiable.

using System;
using System.Collections.Generic;

namespace UnityEngine.Events
{
    public class UnityEventBase
    {
        public void RemoveAllListeners() => Clear();
        protected virtual void Clear() { }
    }

    public class UnityEvent : UnityEventBase
    {
        private readonly List<Action> listeners = new List<Action>();

        public void AddListener(Action call) => listeners.Add(call);
        public void RemoveListener(Action call) => listeners.Remove(call);
        public void Invoke() { foreach (Action l in listeners.ToArray()) l(); }
        protected override void Clear() => listeners.Clear();
    }

    public class UnityEvent<T> : UnityEventBase
    {
        private readonly List<Action<T>> listeners = new List<Action<T>>();

        public void AddListener(Action<T> call) => listeners.Add(call);
        public void RemoveListener(Action<T> call) => listeners.Remove(call);
        public void Invoke(T arg) { foreach (Action<T> l in listeners.ToArray()) l(arg); }
        protected override void Clear() => listeners.Clear();
    }

    public class UnityAction { }
}

namespace UnityEngine.EventSystems
{
    public class EventSystem : Component { }

    public abstract class AbstractEventData
    {
        public bool used { get; private set; }
        public void Use() => used = true;
    }

    public class BaseEventData : AbstractEventData { }

    public class PointerEventData : BaseEventData
    {
        public enum InputButton { Left, Right, Middle }

        public Vector2 position { get; set; }
        public Vector2 delta { get; set; }
        public Vector2 pressPosition { get; set; }
        public int pointerId { get; set; }
        public int clickCount { get; set; }
        public InputButton button { get; set; }
        public GameObject pointerCurrentRaycast { get; set; }
        public bool dragging { get; set; }
    }

    public interface IEventSystemHandler { }
    public interface IPointerDownHandler : IEventSystemHandler { void OnPointerDown(PointerEventData eventData); }
    public interface IPointerUpHandler : IEventSystemHandler { void OnPointerUp(PointerEventData eventData); }
    public interface IPointerClickHandler : IEventSystemHandler { void OnPointerClick(PointerEventData eventData); }
    public interface IPointerEnterHandler : IEventSystemHandler { void OnPointerEnter(PointerEventData eventData); }
    public interface IPointerExitHandler : IEventSystemHandler { void OnPointerExit(PointerEventData eventData); }
    public interface IBeginDragHandler : IEventSystemHandler { void OnBeginDrag(PointerEventData eventData); }
    public interface IDragHandler : IEventSystemHandler { void OnDrag(PointerEventData eventData); }
    public interface IEndDragHandler : IEventSystemHandler { void OnEndDrag(PointerEventData eventData); }

    public class UIBehaviour : MonoBehaviour { }
}

namespace UnityEngine.UI
{
    using UnityEngine.Events;
    using UnityEngine.EventSystems;

    public class Graphic : UIBehaviour
    {
        public Color color { get; set; } = Color.white;
        public bool raycastTarget { get; set; } = true;
        public Material material { get; set; }
        public RectTransform rectTransform => gameObject.GetComponent<RectTransform>();
        public void SetAllDirty() { }
    }

    public class MaskableGraphic : Graphic { }

    public class Image : MaskableGraphic
    {
        public enum Type { Simple, Sliced, Tiled, Filled }
        public enum FillMethod { Horizontal, Vertical, Radial90, Radial180, Radial360 }

        public Sprite sprite { get; set; }
        public Sprite overrideSprite { get; set; }
        public Type type { get; set; }
        public bool preserveAspect { get; set; }
        public float fillAmount { get; set; } = 1f;
        public FillMethod fillMethod { get; set; }
    }

    public class RawImage : MaskableGraphic
    {
        public Texture texture { get; set; }
        public Rect uvRect { get; set; }
    }

    public class Text : MaskableGraphic
    {
        public string text { get; set; } = "";
        public int fontSize { get; set; } = 14;
        public bool supportRichText { get; set; } = true;
    }

    public class Shadow : UIBehaviour
    {
        public Color effectColor { get; set; } = Color.black;
        public Vector2 effectDistance { get; set; } = new Vector2(1f, -1f);
        public bool useGraphicAlpha { get; set; } = true;
    }

    public class Outline : Shadow { }
    public class Mask : UIBehaviour { }
    public class RectMask2D : UIBehaviour { }

    [Serializable]
    public class ColorBlock
    {
        public Color normalColor = Color.white;
        public Color highlightedColor = Color.white;
        public Color pressedColor = Color.white;
        public Color selectedColor = Color.white;
        public Color disabledColor = Color.gray;
        public float colorMultiplier = 1f;
        public float fadeDuration = 0.1f;
    }

    public class Selectable : UIBehaviour
    {
        public bool interactable { get; set; } = true;
        public ColorBlock colors { get; set; } = new ColorBlock();
        public Graphic targetGraphic { get; set; }
        public Image image { get; set; }
        public void Select() { }
    }

    public class Button : Selectable
    {
        public class ButtonClickedEvent : UnityEvent { }

        public ButtonClickedEvent onClick { get; set; } = new ButtonClickedEvent();
    }

    public class Toggle : Selectable
    {
        public class ToggleEvent : UnityEvent<bool> { }

        public bool isOn { get; set; }
        public ToggleEvent onValueChanged { get; set; } = new ToggleEvent();
        public Graphic graphic { get; set; }
    }

    public class Slider : Selectable
    {
        public class SliderEvent : UnityEvent<float> { }

        public float value { get; set; }
        public float minValue { get; set; }
        public float maxValue { get; set; } = 1f;
        public bool wholeNumbers { get; set; }
        public SliderEvent onValueChanged { get; set; } = new SliderEvent();
    }

    public class Scrollbar : Selectable
    {
        public class ScrollEvent : UnityEvent<float> { }

        public float value { get; set; }
        public float size { get; set; }
        public ScrollEvent onValueChanged { get; set; } = new ScrollEvent();
    }

    public class ScrollRect : UIBehaviour
    {
        public RectTransform content { get; set; }
        public RectTransform viewport { get; set; }
        public bool horizontal { get; set; } = true;
        public bool vertical { get; set; } = true;
        public Vector2 normalizedPosition { get; set; }
        public float verticalNormalizedPosition { get; set; }
        public float horizontalNormalizedPosition { get; set; }
        public Scrollbar verticalScrollbar { get; set; }
        public Scrollbar horizontalScrollbar { get; set; }
    }

    public class InputField : Selectable
    {
        public class OnChangeEvent : UnityEvent<string> { }
        public class SubmitEvent : UnityEvent<string> { }

        public string text { get; set; } = "";
        public OnChangeEvent onValueChanged { get; set; } = new OnChangeEvent();
        public SubmitEvent onEndEdit { get; set; } = new SubmitEvent();
    }

    public class Dropdown : Selectable
    {
        [Serializable]
        public class OptionData
        {
            public string text;
            public Sprite image;
            public OptionData() { }
            public OptionData(string text) { this.text = text; }
        }

        public class DropdownEvent : UnityEvent<int> { }

        public int value { get; set; }
        public List<OptionData> options { get; set; } = new List<OptionData>();
        public DropdownEvent onValueChanged { get; set; } = new DropdownEvent();

        public void ClearOptions() => options.Clear();
        public void AddOptions(List<string> texts) => options.AddRange(texts.ConvertAll(t => new OptionData(t)));
        public void AddOptions(List<OptionData> newOptions) => options.AddRange(newOptions);
        public void RefreshShownValue() { }
    }

    public class LayoutGroup : UIBehaviour
    {
        public float spacing { get; set; }
        public bool childForceExpandWidth { get; set; }
        public bool childForceExpandHeight { get; set; }
    }

    public class HorizontalOrVerticalLayoutGroup : LayoutGroup { }
    public class HorizontalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class VerticalLayoutGroup : HorizontalOrVerticalLayoutGroup { }
    public class GridLayoutGroup : LayoutGroup
    {
        public Vector2 cellSize { get; set; }
    }

    public class LayoutElement : UIBehaviour
    {
        public float minWidth { get; set; }
        public float minHeight { get; set; }
        public float preferredWidth { get; set; }
        public float preferredHeight { get; set; }
    }

    public class ContentSizeFitter : UIBehaviour
    {
        public enum FitMode { Unconstrained, MinSize, PreferredSize }

        public FitMode horizontalFit { get; set; }
        public FitMode verticalFit { get; set; }
    }

    public class AspectRatioFitter : UIBehaviour
    {
        public enum AspectMode { None, WidthControlsHeight, HeightControlsWidth, FitInParent, EnvelopeParent }

        public AspectMode aspectMode { get; set; }
        public float aspectRatio { get; set; } = 1f;
    }

    public class CanvasScaler : UIBehaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }

        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public float scaleFactor { get; set; } = 1f;
    }

    public class GraphicRaycaster : UIBehaviour { }

    public static class LayoutRebuilder
    {
        public static void ForceRebuildLayoutImmediate(RectTransform layoutRoot) { }
        public static void MarkLayoutForRebuild(RectTransform rect) { }
    }
}
