using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Scrolling chat log with speech bubbles and an animated "typing..." indicator.</summary>
public class ChatView : MonoBehaviour
{
    public enum Side { Left, Right, Center }

    ScrollRect scroll;
    RectTransform content;
    GameObject typingRow;
    TextMeshProUGUI typingText;
    string typingName;

    public static ChatView Create(Transform parent)
    {
        var scroll = UiKit.ScrollList(parent, out var content, 12, 16, "Chat");
        var view = scroll.gameObject.AddComponent<ChatView>();
        view.scroll = scroll;
        view.content = content;
        return view;
    }

    public void Clear()
    {
        HideTyping();
        UiKit.Clear(content);
    }

    /// <summary>Adds a bubble. Returns the bubble so callers can decorate it (e.g. evidence cards).</summary>
    public Image AddBubble(string speaker, string text, Side side, Color bubbleColor, Color? textColor = null, Texture avatar = null)
    {
        var row = UiKit.Rect("Row", content);
        row.HLayout(avatar != null ? 10 : 0, 0, side == Side.Right ? TextAnchor.UpperRight : TextAnchor.UpperLeft);

        if (avatar != null && side == Side.Left)
        {
            var av = UiKit.Panel(row, new Color(1, 1, 1, 0.9f), UiKit.AvatarSprite, "Avatar");
            av.Size(prefW: 54, prefH: 54, minW: 54);
            av.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var raw = UiKit.Rect("Head", av.transform).Stretch().gameObject.AddComponent<RawImage>();
            raw.texture = avatar;
            raw.raycastTarget = false;
        }

        if (side == Side.Right) UiKit.Spacer(row, 1);
        if (side == Side.Center) UiKit.Spacer(row, 0.5f);

        var bubble = UiKit.Card(row, bubbleColor, 14, 4, "Bubble");
        bubble.sprite = UiKit.Theme.bubbleSprite != null ? UiKit.Theme.bubbleSprite : bubble.sprite;
        if (UiKit.Theme.bubbleSprite != null) bubble.type = Image.Type.Sliced;
        bubble.Size(flexW: side == Side.Center ? 4 : 3);

        var tc = textColor ?? UiKit.Theme.cardText;
        if (!string.IsNullOrEmpty(speaker))
            UiKit.Text(bubble.transform, speaker, UiKit.Theme.smallSize, new Color(tc.r, tc.g, tc.b, 0.65f), style: FontStyles.Bold);
        UiKit.Text(bubble.transform, text, UiKit.Theme.bodySize, tc);

        if (side == Side.Left) UiKit.Spacer(row, 1);
        if (side == Side.Center) UiKit.Spacer(row, 0.5f);

        KeepTypingLast();
        ScrollToBottom();
        StartCoroutine(PopIn(bubble.transform));
        return bubble;
    }

    public void ShowTyping(string who)
    {
        typingName = who;
        if (typingRow == null)
        {
            var row = UiKit.Rect("Typing", content);
            row.HLayout(0, 0, TextAnchor.UpperLeft);
            var bubble = UiKit.Panel(row, new Color(1, 1, 1, 0.92f), UiKit.Theme.bubbleSprite, "TypingBubble");
            bubble.HLayout(10, 16, TextAnchor.MiddleLeft);
            typingText = UiKit.Text(bubble.transform, "", UiKit.Theme.smallSize, new Color(0.3f, 0.28f, 0.45f), style: FontStyles.Bold);
            typingText.textWrappingMode = TextWrappingModes.NoWrap;
            dots = new RectTransform[3];
            for (int i = 0; i < 3; i++)
            {
                var d = UiKit.Panel(bubble.transform, new Color(0.45f, 0.40f, 0.70f), UiKit.AvatarSprite, "Dot");
                d.Size(prefW: 12, prefH: 12, minW: 12);
                dots[i] = (RectTransform)d.transform;
            }
            UiKit.Spacer(row, 1);
            typingRow = row.gameObject;
            StartCoroutine(AnimateTyping());
        }
        typingText.text = typingName;
        typingRow.SetActive(true);
        KeepTypingLast();
        ScrollToBottom();
    }

    RectTransform[] dots;

    IEnumerator AnimateTyping()
    {
        // Three dots bouncing in a wave (scale, so the layout group never fights the animation).
        while (typingRow != null)
        {
            if (typingRow.activeSelf && dots != null)
            {
                for (int i = 0; i < dots.Length; i++)
                {
                    if (dots[i] == null) continue;
                    float k = Mathf.Max(0, Mathf.Sin(Time.unscaledTime * 7f - i * 0.9f));
                    float s = 0.7f + k * 0.55f;
                    dots[i].localScale = new Vector3(s, s, 1);
                }
            }
            yield return null;
        }
    }

    public void HideTyping()
    {
        if (typingRow != null) typingRow.SetActive(false);
    }

    void KeepTypingLast()
    {
        if (typingRow != null) typingRow.transform.SetAsLastSibling();
    }


    public void ScrollToBottom()
    {
        if (isActiveAndEnabled) StartCoroutine(ScrollNextFrame());
    }

    IEnumerator ScrollNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        scroll.verticalNormalizedPosition = 0f;
    }

    static IEnumerator PopIn(Transform t)
    {
        float time = 0f;
        while (t != null && time < 0.18f)
        {
            time += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(time / 0.18f);
            float s = 0.85f + 0.15f * (1f - Mathf.Pow(1f - k, 3f)) + Mathf.Sin(k * Mathf.PI) * 0.04f;
            t.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one;
    }
}
