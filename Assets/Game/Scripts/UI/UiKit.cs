using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Small helper library that builds uGUI hierarchies from code. Everything in Leak Desk's UI is created at
/// runtime through this, so screens are plain C# (easy to diff and to change quickly during the jam).
/// </summary>
public static class UiKit
{
    static UiTheme theme;

    public static UiTheme Theme
    {
        get
        {
            if (theme == null) theme = Resources.Load<UiTheme>("UiTheme");
            if (theme == null) theme = ScriptableObject.CreateInstance<UiTheme>();
            return theme;
        }
    }

    // ---------- roots ----------

    public static Canvas CreateCanvas(string name, int sortOrder = 0)
    {
        EnsureEventSystem();
        var go = new GameObject(name, typeof(RectTransform));
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // never crop the 16:9 layout on other aspect ratios
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem));
        es.AddComponent<InputSystemUIInputModule>();
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    // ---------- layout helpers ----------

    public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(left, bottom);
        rt.offsetMax = new Vector2(-right, -top);
        return rt;
    }

    public static RectTransform Anchor(this RectTransform rt, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    public static VerticalLayoutGroup VLayout(this Component c, float spacing = 8, int pad = 0, TextAnchor align = TextAnchor.UpperLeft,
        bool controlHeight = true, bool expandHeight = false)
    {
        var l = c.gameObject.GetComponent<VerticalLayoutGroup>() ?? c.gameObject.AddComponent<VerticalLayoutGroup>();
        l.spacing = spacing;
        l.padding = new RectOffset(pad, pad, pad, pad);
        l.childAlignment = align;
        l.childControlWidth = true;
        l.childControlHeight = controlHeight;
        l.childForceExpandWidth = true;
        l.childForceExpandHeight = expandHeight;
        return l;
    }

    public static HorizontalLayoutGroup HLayout(this Component c, float spacing = 8, int pad = 0, TextAnchor align = TextAnchor.MiddleLeft,
        bool expandWidth = false)
    {
        var l = c.gameObject.GetComponent<HorizontalLayoutGroup>() ?? c.gameObject.AddComponent<HorizontalLayoutGroup>();
        l.spacing = spacing;
        l.padding = new RectOffset(pad, pad, pad, pad);
        l.childAlignment = align;
        l.childControlWidth = true;
        l.childControlHeight = true;
        l.childForceExpandWidth = expandWidth;
        l.childForceExpandHeight = false;
        return l;
    }

    public static LayoutElement Size(this Component c, float prefW = -1, float prefH = -1, float flexW = -1, float flexH = -1,
        float minW = -1, float minH = -1)
    {
        var le = c.gameObject.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
        // A fixed preferred size means "don't grow": layout groups otherwise report themselves as flexible
        // and steal leftover space from the columns that should stretch.
        if (prefW >= 0 && flexW < 0) flexW = 0;
        if (prefH >= 0 && flexH < 0) flexH = 0;
        // Fixed-width columns also refuse to be squeezed by greedy neighbours (long unwrapped text).
        if (prefW >= 0 && flexW == 0 && minW < 0) minW = prefW;
        le.preferredWidth = prefW;
        le.preferredHeight = prefH;
        le.flexibleWidth = flexW;
        le.flexibleHeight = flexH;
        le.minWidth = minW;
        le.minHeight = minH;
        return le;
    }

    public static ContentSizeFitter FitHeight(this Component c)
    {
        var f = c.gameObject.GetComponent<ContentSizeFitter>() ?? c.gameObject.AddComponent<ContentSizeFitter>();
        f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return f;
    }

    // ---------- widgets ----------

    public static Image Panel(Transform parent, Color color, Sprite sprite = null, string name = "Panel")
    {
        var rt = Rect(name, parent);
        var img = rt.gameObject.AddComponent<Image>();
        ApplySprite(img, sprite);
        img.color = color;
        return img;
    }

    public static void ApplySprite(Image img, Sprite sprite)
    {
        if (sprite == null) return;
        img.sprite = sprite;
        img.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        img.pixelsPerUnitMultiplier = Theme.pixelsPerUnitMultiplier;
    }

    public static TextMeshProUGUI Text(Transform parent, string text, float size = -1, Color? color = null,
        TextAlignmentOptions align = TextAlignmentOptions.TopLeft, FontStyles style = FontStyles.Normal, string name = "Text")
    {
        var rt = Rect(name, parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (Theme.font != null) t.font = Theme.font;
        t.text = text;
        t.fontSize = size > 0 ? size : Theme.bodySize;
        t.color = color ?? Theme.text;
        t.alignment = align;
        t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.raycastTarget = false;
        t.richText = true;
        return t;
    }

    public static TextMeshProUGUI Heading(Transform parent, string text, float size = -1, Color? color = null)
    {
        var t = Text(parent, text, size > 0 ? size : Theme.headingSize, color, TextAlignmentOptions.Left, FontStyles.Bold, "Heading");
        if (Theme.headingFont != null) t.font = Theme.headingFont;
        return t;
    }

    public static Button Button(Transform parent, string label, Action onClick, Color? bg = null, Color? fg = null,
        float height = 52, float size = -1)
    {
        var img = Panel(parent, bg ?? Theme.accent, Theme.buttonSprite, "Button: " + label);
        var btn = img.gameObject.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.55f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        btn.Size(prefH: height, minH: height);

        var t = Text(img.transform, label, size > 0 ? size : Theme.bodySize, fg ?? Theme.accentText, TextAlignmentOptions.Center,
            FontStyles.Bold, "Label");
        ((RectTransform)t.transform).Stretch(14, 4, 14, 4);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Ellipsis;
        btn.gameObject.AddComponent<ButtonPop>();
        return btn;
    }

    public static void SetLabel(this Button b, string label)
    {
        var t = b.GetComponentInChildren<TMP_Text>();
        if (t != null) t.text = label;
    }

    public static TMP_InputField Input(Transform parent, string placeholder, bool multiline = false, float height = 56)
    {
        var img = Panel(parent, Color.white, Theme.inputSprite, "Input");
        var go = img.gameObject;
        go.SetActive(false); // configure fully before TMP_InputField.OnEnable runs

        var area = Rect("Text Area", go.transform).Stretch(16, 8, 16, 8);
        area.gameObject.AddComponent<RectMask2D>();

        var ph = Text(area, placeholder, Theme.bodySize, new Color(0.45f, 0.47f, 0.55f), TextAlignmentOptions.MidlineLeft, FontStyles.Italic, "Placeholder");
        ((RectTransform)ph.transform).Stretch();
        var txt = Text(area, "", Theme.bodySize, Theme.cardText, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, "Text");
        ((RectTransform)txt.transform).Stretch();
        if (!multiline)
        {
            ph.textWrappingMode = TextWrappingModes.NoWrap;
            txt.textWrappingMode = TextWrappingModes.NoWrap;
        }

        var input = go.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = txt;
        input.placeholder = ph;
        input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        input.caretColor = Theme.cardText;
        input.customCaretColor = true;
        input.selectionColor = new Color(Theme.secondary.r, Theme.secondary.g, Theme.secondary.b, 0.35f);
        input.richText = false;
        if (Theme.font != null) input.fontAsset = Theme.font;
        input.Size(prefH: height, minH: height, flexW: 1);
        go.SetActive(true);
        return input;
    }

    /// <summary>Vertical scrolling list. Add children to the returned content transform.</summary>
    public static ScrollRect ScrollList(Transform parent, out RectTransform content, float spacing = 10, int pad = 12, string name = "Scroll")
    {
        var root = Rect(name, parent);
        var scroll = root.gameObject.AddComponent<ScrollRect>();
        var hit = root.gameObject.AddComponent<Image>(); // invisible, catches scroll wheel over empty space
        hit.color = new Color(0, 0, 0, 0);

        var viewport = Rect("Viewport", root).Stretch();
        viewport.gameObject.AddComponent<RectMask2D>();

        content = Rect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        content.VLayout(spacing, pad);
        content.FitHeight();

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35;
        scroll.inertia = true;
        return scroll;
    }

    /// <summary>A rounded card with vertical layout; returns the card image.</summary>
    public static Image Card(Transform parent, Color? color = null, int pad = 14, float spacing = 6, string name = "Card")
    {
        var img = Panel(parent, color ?? Theme.card, Theme.cardSprite, name);
        img.VLayout(spacing, pad);
        return img;
    }

    /// <summary>
    /// Makes a 9-sliced round sprite a true capsule at this height: scales the slice borders to half the height,
    /// so small bars and chips don't get squashed into ovals.
    /// </summary>
    public static Image Pill(this Image img, float height)
    {
        if (img.sprite == null || img.sprite.border == Vector4.zero) return img;
        float border = Mathf.Max(img.sprite.border.y, img.sprite.border.w) * 100f / img.sprite.pixelsPerUnit;
        img.pixelsPerUnitMultiplier = Mathf.Max(0.01f, border / (height * 0.5f));
        return img;
    }

    /// <summary>A capsule-shaped chip (emotion tags, source tags).</summary>
    public static Image Chip(Transform parent, Color color, float width, float height, string name = "Chip")
    {
        var img = Panel(parent, color, Theme.pillSprite, name).Pill(height);
        img.HLayout(0, 4, TextAnchor.MiddleCenter);
        img.Size(prefW: width, prefH: height, minW: width);
        return img;
    }

    /// <summary>Horizontal progress bar; returns the fill image (use SetBar).</summary>
    public static Image Bar(Transform parent, Color fill, float height = 12)
    {
        var bg = Panel(parent, new Color(0, 0, 0, 0.35f), Theme.pillSprite, "Bar").Pill(height);
        bg.Size(prefH: height, minH: height, flexW: 1);
        var f = Panel(bg.transform, fill, Theme.barFillSprite ?? Theme.pillSprite, "Fill").Pill(height);
        var rt = (RectTransform)f.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(0, 1);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return f;
    }

    public static void SetBar(Image fill, float t01)
    {
        var rt = (RectTransform)fill.transform;
        rt.anchorMax = new Vector2(Mathf.Clamp01(t01), 1);
    }

    public static RectTransform Spacer(Transform parent, float flexW = 1, float flexH = 0)
    {
        var rt = Rect("Spacer", parent);
        rt.Size(flexW: flexW, flexH: flexH);
        return rt;
    }

    /// <summary>A tinted icon from the theme's icon set (empty Image if the pack isn't present).</summary>
    public static Image Icon(Transform parent, string icon, float size, Color color)
    {
        var img = Panel(parent, color, null, "Icon: " + icon);
        img.sprite = Theme.Icon(icon);
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.enabled = img.sprite != null;
        img.Size(prefW: size, prefH: size, minW: size, minH: size);
        return img;
    }

    static Sprite gradient;

    /// <summary>Vertical white-to-transparent gradient (top opaque), generated once. Tint via Image.color.</summary>
    public static Sprite GradientSprite
    {
        get
        {
            if (gradient != null) return gradient;
            var tex = new Texture2D(4, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UiGradient" };
            for (int y = 0; y < 128; y++)
            {
                float a = Mathf.SmoothStep(0f, 1f, y / 127f);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            tex.Apply();
            gradient = Sprite.Create(tex, new Rect(0, 0, 4, 128), new Vector2(0.5f, 0.5f), 100f);
            return gradient;
        }
    }

    /// <summary>Stretched gradient overlay (no raycasts). flip = darken from the bottom instead of light from the top.</summary>
    public static Image GradientOverlay(Transform parent, Color color, bool flip = false)
    {
        var img = Panel(parent, color, null, "Gradient");
        img.sprite = GradientSprite;
        img.type = Image.Type.Simple;
        img.raycastTarget = false;
        var rt = (RectTransform)img.transform;
        rt.Stretch();
        if (flip) rt.localScale = new Vector3(1, -1, 1);
        return img;
    }

    /// <summary>Round avatar sprite (falls back to the pill).</summary>
    public static Sprite AvatarSprite => Theme.avatarSprite != null ? Theme.avatarSprite : Theme.pillSprite;

    public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);

    /// <summary>Removes one child now (detached first so the layout updates immediately).</summary>
    public static void Remove(Transform child)
    {
        child.gameObject.SetActive(false);
        child.SetParent(null, false);
        UnityEngine.Object.Destroy(child.gameObject);
    }

    /// <summary>Removes all children now (detached first, so childCount/GetChild are correct immediately).</summary>
    public static void Clear(Transform t)
    {
        for (int i = t.childCount - 1; i >= 0; i--)
        {
            var child = t.GetChild(i);
            child.gameObject.SetActive(false);
            child.SetParent(null, false);
            UnityEngine.Object.Destroy(child.gameObject);
        }
    }
}
