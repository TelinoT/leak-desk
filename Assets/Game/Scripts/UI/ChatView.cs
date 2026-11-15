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
    public Image AddBubble(string speaker, string text, Side side, Color bubbleColor, Color? textColor = null)
    {
        var row = UiKit.Rect("Row", content);
        row.HLayout(0, 0, side == Side.Right ? TextAnchor.UpperRight : TextAnchor.UpperLeft);

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
            var bubble = UiKit.Card(row, new Color(1, 1, 1, 0.85f), 12, 0, "TypingBubble");
            typingText = UiKit.Text(bubble.transform, "", UiKit.Theme.bodySize, UiKit.Theme.cardText, style: FontStyles.Italic);
            typingText.textWrappingMode = TextWrappingModes.NoWrap;
            bubble.Size(prefW: 320);
            UiKit.Spacer(row, 1);
            typingRow = row.gameObject;
            StartCoroutine(AnimateTyping());
        }
        typingRow.SetActive(true);
        KeepTypingLast();
        ScrollToBottom();
    }

    public void HideTyping()
    {
        if (typingRow != null) typingRow.SetActive(false);
    }

    void KeepTypingLast()
    {
        if (typingRow != null) typingRow.transform.SetAsLastSibling();
    }

    IEnumerator AnimateTyping()
    {
        int dots = 0;
        while (typingRow != null)
        {
            if (typingRow.activeSelf)
            {
                dots = (dots + 1) % 4;
                typingText.text = $"{typingName} is thinking{new string('.', dots)}";
            }
            yield return new WaitForSecondsRealtime(0.35f);
        }
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
