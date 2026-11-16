using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Jensenbot's animated screen-face: blinking pill eyes, a mouth that wobbles while "talking",
/// a thinking pose while waiting for the LLM, and an RGB-split glitch used when its data feed was tampered with.
/// </summary>
public class JensenbotFace : MonoBehaviour
{
    public enum Mood { Idle, Thinking, Talking, Glitch }

    RectTransform eyeL, eyeR, mouth, faceRoot;
    Image screen, glitchA, glitchB;
    Mood mood = Mood.Idle;
    float nextBlink, blinkT = -1f, moodT;
    static readonly Color Glow = new(0.56f, 0.95f, 0.35f);

    public static JensenbotFace Create(Transform parent, float size)
    {
        var T = UiKit.Theme;
        var shell = UiKit.Panel(parent, new Color(0.56f, 0.93f, 0.27f), T.buttonSprite, "JensenbotFace");
        shell.Size(prefW: size, prefH: size, minW: size, minH: size);
        var face = shell.gameObject.AddComponent<JensenbotFace>();

        face.screen = UiKit.Panel(shell.transform, new Color(0.06f, 0.10f, 0.08f), T.panelSprite, "Screen");
        ((RectTransform)face.screen.transform).Stretch(size * 0.12f, size * 0.12f, size * 0.12f, size * 0.2f);
        face.faceRoot = UiKit.Rect("Face", face.screen.transform).Stretch();

        face.glitchA = UiKit.Panel(face.faceRoot, new Color(1f, 0.2f, 0.7f, 0f), T.panelSprite, "GlitchA");
        ((RectTransform)face.glitchA.transform).Stretch();
        face.glitchB = UiKit.Panel(face.faceRoot, new Color(0.2f, 0.9f, 1f, 0f), T.panelSprite, "GlitchB");
        ((RectTransform)face.glitchB.transform).Stretch();

        face.eyeL = Feature(face.faceRoot, new Vector2(0.33f, 0.62f), new Vector2(size * 0.12f, size * 0.2f));
        face.eyeR = Feature(face.faceRoot, new Vector2(0.67f, 0.62f), new Vector2(size * 0.12f, size * 0.2f));
        face.mouth = Feature(face.faceRoot, new Vector2(0.5f, 0.3f), new Vector2(size * 0.32f, size * 0.07f));
        face.nextBlink = Time.time + Random.Range(1.5f, 4f);
        return face;
    }

    static RectTransform Feature(Transform parent, Vector2 anchor, Vector2 size)
    {
        var img = UiKit.Panel(parent, Glow, UiKit.Theme.pillSprite, "Feature").Pill(Mathf.Min(size.x, size.y));
        img.raycastTarget = false;
        var rt = (RectTransform)img.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.sizeDelta = size;
        return rt;
    }

    /// <summary>After being exposed: the screen turns alarm-red and the eyes become narrow slits.</summary>
    public void SetCompromised()
    {
        compromised = true;
        screen.color = new Color(0.22f, 0.04f, 0.06f);
        foreach (var f in new[] { eyeL, eyeR, mouth }) f.GetComponent<Image>().color = new Color(1f, 0.45f, 0.35f);
        GetComponent<Image>().color = new Color(1f, 0.35f, 0.45f);
        SetMood(Mood.Glitch);
    }

    bool compromised;

    public void SetMood(Mood m)
    {
        mood = m;
        moodT = 0f;
        if (m == Mood.Glitch) StartCoroutine(GlitchBurst());
    }

    void Update()
    {
        moodT += Time.unscaledDeltaTime;
        float t = Time.unscaledTime;

        // Blink.
        if (t > nextBlink && blinkT < 0) blinkT = 0;
        float eyeScaleY = 1f;
        if (blinkT >= 0)
        {
            blinkT += Time.unscaledDeltaTime;
            eyeScaleY = Mathf.Abs(Mathf.Cos(blinkT / 0.16f * Mathf.PI));
            if (blinkT > 0.16f) { blinkT = -1; nextBlink = t + Random.Range(2f, 5f); }
        }

        Vector2 look = Vector2.zero;
        float mouthH = 1f, mouthW = 1f;
        switch (mood)
        {
            case Mood.Thinking:
                look = new Vector2(Mathf.Sin(t * 2.2f) * 6f, 6f);
                mouthW = 0.45f;
                break;
            case Mood.Talking:
                mouthH = 1f + Mathf.Abs(Mathf.Sin(t * 18f)) * 2.2f;
                mouthW = 0.8f;
                if (moodT > 2.5f) mood = Mood.Idle;
                break;
            case Mood.Glitch:
                look = new Vector2(Random.Range(-3f, 3f), Random.Range(-2f, 2f));
                if (moodT > 1.2f) mood = Mood.Idle;
                break;
            default:
                look = new Vector2(Mathf.Sin(t * 0.7f) * 4f, Mathf.Sin(t * 0.5f) * 2f);
                break;
        }

        if (compromised) eyeScaleY *= 0.35f;
        eyeL.localScale = new Vector3(1, eyeScaleY, 1);
        eyeR.localScale = new Vector3(1, eyeScaleY, 1);
        eyeL.anchoredPosition = look;
        eyeR.anchoredPosition = look;
        mouth.localScale = new Vector3(mouthW, mouthH, 1);
        mouth.anchoredPosition = look * 0.5f;
    }

    IEnumerator GlitchBurst()
    {
        for (int i = 0; i < 6; i++)
        {
            faceRoot.anchoredPosition = new Vector2(Random.Range(-5f, 5f), Random.Range(-2f, 2f));
            glitchA.color = new Color(1f, 0.2f, 0.7f, 0.35f);
            glitchB.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            ((RectTransform)glitchA.transform).anchoredPosition = new Vector2(Random.Range(-6f, -2f), 0);
            ((RectTransform)glitchB.transform).anchoredPosition = new Vector2(Random.Range(2f, 6f), 0);
            yield return new WaitForSecondsRealtime(Random.Range(0.03f, 0.08f));
            glitchA.color = glitchB.color = new Color(0, 0, 0, 0);
            faceRoot.anchoredPosition = Vector2.zero;
            yield return new WaitForSecondsRealtime(Random.Range(0.04f, 0.12f));
        }
    }
}
