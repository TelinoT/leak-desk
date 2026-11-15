using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FeedOverlay : MonoBehaviour
{
    [SerializeField] private RawImage grain;
    [SerializeField] private RawImage scanlines;
    [SerializeField] private Graphic recDot;
    [SerializeField] private TMP_Text timeText;

    [SerializeField, Range(0f, 1f)] private float grainStrength = 0.08f;
    [SerializeField] private float blinkInterval = 0.6f;

    private static Texture2D noiseTexture;
    private static Texture2D lineTexture;

    private void Start()
    {
        if (noiseTexture == null) noiseTexture = MakeNoise(128);
        if (lineTexture == null) lineTexture = MakeLines();

        grain.texture = noiseTexture;
        grain.color = new Color(1f, 1f, 1f, grainStrength);
        scanlines.texture = lineTexture;
    }

    private void Update()
    {
        // Grain: jump the noise to a random spot every frame
        grain.uvRect = new Rect(Random.value, Random.value, 2f, 2f);

        // Scanlines: repeat the 3-pixel pattern down the whole feed
        float height = scanlines.rectTransform.rect.height;
        scanlines.uvRect = new Rect(0f, 0f, 1f, Mathf.Max(1f, height / 3f));

        // Blinking REC dot
        recDot.enabled = Mathf.Repeat(Time.unscaledTime, blinkInterval * 2f) < blinkInterval;

        if (timeText != null) timeText.text = GameClock.TimeText();
    }

    private static Texture2D MakeNoise(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point
        };
        var pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            byte v = (byte)Random.Range(0, 256);
            pixels[i] = new Color32(v, v, v, 255);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }

    private static Texture2D MakeLines()
    {
        var texture = new Texture2D(1, 3, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Point
        };
        texture.SetPixels32(new[]
        {
            new Color32(0, 0, 0, 70), // one dark line...
            new Color32(0, 0, 0, 0),  // ...then two clear ones
            new Color32(0, 0, 0, 0)
        });
        texture.Apply();
        return texture;
    }
}