using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Drop target for EvidenceDrag (the interrogation stage). Plays the "slam" and calls back.</summary>
public class PresentDropZone : MonoBehaviour
{
    static PresentDropZone active;

    public Action<EvidenceItem> OnPresent;
    public Func<bool> CanAccept = () => true;
    public Image Highlight;
    public RectTransform ShakeTarget;

    void OnEnable() => active = this;
    void OnDisable() { if (active == this) active = null; }

    public static void SetHighlight(bool on)
    {
        if (active == null || active.Highlight == null) return;
        active.Highlight.gameObject.SetActive(on && active.CanAccept());
    }

    public void Accept(EvidenceItem item, RectTransform ghost)
    {
        if (!CanAccept())
        {
            Destroy(ghost.gameObject);
            return;
        }
        StartCoroutine(Slam(item, ghost));
    }

    IEnumerator Slam(EvidenceItem item, RectTransform ghost)
    {
        // Fly to the zone's center, overshoot, then thud down.
        var target = (RectTransform)transform;
        Vector3 start = ghost.position, end = target.TransformPoint(target.rect.center);
        Quaternion r0 = ghost.rotation, r1 = Quaternion.Euler(0, 0, 4f);
        for (float t = 0; t < 0.22f; t += Time.unscaledDeltaTime)
        {
            float k = t / 0.22f;
            ghost.position = Vector3.Lerp(start, end, 1 - Mathf.Pow(1 - k, 3));
            ghost.rotation = Quaternion.Slerp(r0, r1, k);
            ghost.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.35f, Mathf.Sin(k * Mathf.PI));
            yield return null;
        }
        ghost.localScale = Vector3.one;
        Sfx.Play("slam");
        UiFx.Shake(ShakeTarget != null ? ShakeTarget : target, 14f, 0.3f);
        OnPresent?.Invoke(item);
        yield return new WaitForSecondsRealtime(0.35f);
        var cg = ghost.GetComponent<CanvasGroup>();
        for (float t = 0; t < 0.25f && ghost != null; t += Time.unscaledDeltaTime)
        {
            if (cg) cg.alpha = 1 - t / 0.25f;
            ghost.localScale = Vector3.one * (1 - t / 0.25f * 0.3f);
            yield return null;
        }
        if (ghost != null) Destroy(ghost.gameObject);
    }
}
