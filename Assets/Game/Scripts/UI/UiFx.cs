using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>Small tween/juice helpers (shake, punch, fade, slide, typewriter) run on a hidden host object.</summary>
public class UiFx : MonoBehaviour
{
    static UiFx host;

    static UiFx Host
    {
        get
        {
            if (host == null) host = new GameObject("[UiFx]").AddComponent<UiFx>();
            return host;
        }
    }

    public static Coroutine Run(IEnumerator routine) => Host.StartCoroutine(routine);

    public static void Shake(RectTransform t, float strength, float duration) => Run(ShakeRoutine(t, strength, duration));

    // Remember each target's resting position so overlapping shakes never drift it.
    static readonly System.Collections.Generic.Dictionary<RectTransform, (Vector2 pos, int count)> resting = new();

    static IEnumerator ShakeRoutine(RectTransform t, float strength, float duration)
    {
        if (t == null) yield break;
        if (!resting.TryGetValue(t, out var r)) r = (t.anchoredPosition, 0);
        resting[t] = (r.pos, r.count + 1);
        for (float time = 0; time < duration && t != null; time += Time.unscaledDeltaTime)
        {
            float k = 1 - time / duration;
            t.anchoredPosition = r.pos + Random.insideUnitCircle * strength * k * k;
            yield return null;
        }
        if (t == null) { resting.Remove(t); yield break; }
        var now = resting[t];
        if (now.count <= 1) { t.anchoredPosition = now.pos; resting.Remove(t); }
        else resting[t] = (now.pos, now.count - 1);
    }

    public static void Punch(Transform t, float amount = 0.15f, float duration = 0.3f) => Run(PunchRoutine(t, amount, duration));

    static IEnumerator PunchRoutine(Transform t, float amount, float duration)
    {
        for (float time = 0; time < duration && t != null; time += Time.unscaledDeltaTime)
        {
            float k = time / duration;
            float s = 1 + Mathf.Sin(k * Mathf.PI * 3) * amount * (1 - k);
            t.localScale = new Vector3(s, s, 1);
            yield return null;
        }
        if (t != null) t.localScale = Vector3.one;
    }

    public static void FadeIn(CanvasGroup cg, float duration = 0.2f, float slideY = 0f) => Run(FadeRoutine(cg, duration, slideY));

    static IEnumerator FadeRoutine(CanvasGroup cg, float duration, float slideY)
    {
        if (cg == null) yield break;
        var rt = (RectTransform)cg.transform;
        Vector2 basePos = rt.anchoredPosition;
        for (float time = 0; time < duration && cg != null; time += Time.unscaledDeltaTime)
        {
            float k = 1 - Mathf.Pow(1 - time / duration, 3);
            cg.alpha = k;
            rt.anchoredPosition = basePos + new Vector2(0, slideY * (1 - k));
            yield return null;
        }
        if (cg == null) yield break;
        cg.alpha = 1;
        rt.anchoredPosition = basePos;
    }

    /// <summary>Reveals text character by character, with optional speech babble.</summary>
    public static Coroutine Typewrite(TMP_Text text, float charsPerSecond = 55f, float voicePitch = 0f) =>
        Run(TypeRoutine(text, charsPerSecond, voicePitch));

    static IEnumerator TypeRoutine(TMP_Text text, float cps, float voice)
    {
        if (text == null) yield break;
        text.ForceMeshUpdate();
        int total = text.textInfo.characterCount;
        text.maxVisibleCharacters = 0;
        float shown = 0;
        while (text != null && text.maxVisibleCharacters < total)
        {
            shown += cps * Time.unscaledDeltaTime;
            int n = Mathf.Min(total, Mathf.FloorToInt(shown));
            for (int i = text.maxVisibleCharacters; i < n; i++)
                if (voice > 0 && i < text.textInfo.characterInfo.Length) Sfx.Babble(text.textInfo.characterInfo[i].character, voice);
            text.maxVisibleCharacters = n;
            yield return null;
        }
        if (text != null) text.maxVisibleCharacters = 99999;
    }
}
