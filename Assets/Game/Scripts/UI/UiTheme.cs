using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Colors, sprites and font for the runtime-built UI. Lives at Assets/Game/Resources/UiTheme.asset.
/// Any sprite left empty falls back to a flat rounded-less Image, so the game works without the GUI pack.
/// </summary>
[CreateAssetMenu(menuName = "Leak Desk/UI Theme", fileName = "UiTheme")]
public class UiTheme : ScriptableObject
{
    [Header("Font")]
    public TMP_FontAsset font;
    public TMP_FontAsset headingFont;

    [Header("Colors")]
    public Color background = new(0.09f, 0.10f, 0.16f, 0.55f);
    public Color panel = new(0.14f, 0.16f, 0.25f, 0.96f);
    public Color panelAlt = new(0.20f, 0.22f, 0.34f, 1f);
    public Color card = new(0.97f, 0.96f, 0.92f, 1f);
    public Color cardText = new(0.16f, 0.17f, 0.24f, 1f);
    public Color text = new(0.96f, 0.97f, 1f, 1f);
    public Color muted = new(0.66f, 0.70f, 0.82f, 1f);
    public Color accent = new(0.46f, 0.85f, 0.25f, 1f);      // Envydia lime
    public Color accentText = new(0.08f, 0.14f, 0.05f, 1f);
    public Color secondary = new(0.29f, 0.62f, 1f, 1f);      // friendly blue
    public Color warning = new(1f, 0.74f, 0.20f, 1f);
    public Color danger = new(1f, 0.36f, 0.40f, 1f);
    public Color bubbleSuspect = new(1f, 1f, 1f, 1f);
    public Color bubblePlayer = new(0.46f, 0.85f, 0.25f, 1f);
    public Color bubbleSystem = new(1f, 0.86f, 0.45f, 1f);

    [Header("Sprites (9-sliced)")]
    public Sprite panelSprite;
    public Sprite cardSprite;
    public Sprite buttonSprite;
    public Sprite inputSprite;
    public Sprite bubbleSprite;
    public Sprite pillSprite;
    public Sprite avatarSprite;
    public Sprite barFillSprite;

    [Header("Icons (looked up by sprite name without the 'Pictoicon_' prefix)")]
    public List<Sprite> icons = new();

    [Header("Sizes")]
    public float pixelsPerUnitMultiplier = 1f;
    public float bodySize = 22f;
    public float smallSize = 17f;
    public float headingSize = 30f;

    Dictionary<string, Sprite> iconLookup;

    public Sprite Icon(string name)
    {
        if (iconLookup == null)
        {
            iconLookup = new Dictionary<string, Sprite>();
            foreach (var s in icons)
                if (s != null) iconLookup[s.name.Replace("Pictoicon_", "")] = s;
        }
        return name != null && iconLookup.TryGetValue(name, out var sprite) ? sprite : null;
    }
}
