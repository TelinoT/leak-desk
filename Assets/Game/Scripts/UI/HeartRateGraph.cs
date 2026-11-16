using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Scrolling ECG-style line drawn with UI vertices. Stress drives the beats per minute, spike height and color,
/// so the suspect's panic is visible at a glance.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class HeartRateGraph : MaskableGraphic
{
    public float stress;          // 0..100, set by the UI
    public float thickness = 4f;
    public Color calmColor = new(0.55f, 0.92f, 0.25f);
    public Color panicColor = new(1f, 0.33f, 0.40f);

    const int Samples = 140;
    readonly float[] values = new float[Samples];
    float phase, shown;
    int head;

    void Update()
    {
        shown = Mathf.Lerp(shown, stress, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
        float bpm = 62f + shown * 1.0f;
        // Advance a few samples per frame; the waveform is one PQRST beat per cycle.
        int steps = Mathf.Max(1, Mathf.RoundToInt(Time.unscaledDeltaTime * 90f));
        for (int i = 0; i < steps; i++)
        {
            phase += bpm / 60f / 90f;
            if (phase > 1f) phase -= 1f;
            values[head] = Beat(phase) * (0.55f + shown / 100f * 0.45f) + Random.Range(-0.02f, 0.02f) * (shown / 100f);
            head = (head + 1) % Samples;
        }
        color = Color.Lerp(calmColor, panicColor, Mathf.InverseLerp(30f, 85f, shown));
        SetVerticesDirty();
    }

    static float Beat(float p)
    {
        // Tiny P wave, sharp QRS spike, rounded T wave.
        if (p < 0.10f) return Mathf.Sin(p / 0.10f * Mathf.PI) * 0.10f;
        if (p < 0.16f) return 0f;
        if (p < 0.19f) return -0.15f * ((p - 0.16f) / 0.03f);
        if (p < 0.23f) return Mathf.Lerp(-0.15f, 1f, (p - 0.19f) / 0.04f);
        if (p < 0.27f) return Mathf.Lerp(1f, -0.3f, (p - 0.23f) / 0.04f);
        if (p < 0.30f) return Mathf.Lerp(-0.3f, 0f, (p - 0.27f) / 0.03f);
        if (p < 0.45f) return 0f;
        if (p < 0.60f) return Mathf.Sin((p - 0.45f) / 0.15f * Mathf.PI) * 0.22f;
        return 0f;
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var r = rectTransform.rect;
        float mid = r.center.y;
        float amp = r.height * 0.45f;
        Vector2 prev = Vector2.zero;
        for (int i = 0; i < Samples; i++)
        {
            float v = values[(head + i) % Samples];
            var p = new Vector2(Mathf.Lerp(r.xMin, r.xMax, i / (float)(Samples - 1)), mid + v * amp);
            if (i > 0) AddSegment(vh, prev, p, i / (float)Samples);
            prev = p;
        }
    }

    void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float fade)
    {
        Vector2 n = Vector2.Perpendicular((b - a).normalized) * thickness * 0.5f;
        var c = color;
        c.a *= Mathf.Clamp01(fade * 1.6f); // older samples fade out to the left
        int i = vh.currentVertCount;
        vh.AddVert(a - n, c, Vector2.zero);
        vh.AddVert(a + n, c, Vector2.zero);
        vh.AddVert(b + n, c, Vector2.zero);
        vh.AddVert(b - n, c, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2);
        vh.AddTriangle(i, i + 2, i + 3);
    }
}
