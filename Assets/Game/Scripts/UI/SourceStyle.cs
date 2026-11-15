using UnityEngine;

/// <summary>Label + color tag for each evidence/document source.</summary>
public static class SourceStyle
{
    public static string Label(string source) => source switch
    {
        "email" => "EMAIL",
        "slack" => "SLACK",
        "badge" => "BADGE",
        "transfer" => "FILES",
        "vpn" => "VPN",
        "audit" => "AI AUDIT",
        "ticket" => "IT TICKET",
        "calendar" => "CALENDAR",
        "news" => "NEWS",
        "facilities" => "FACILITIES",
        "testimony" => "STATEMENT",
        _ => "NOTE",
    };

    public static Color Color(string source) => source switch
    {
        "email" => new Color(0.29f, 0.62f, 1f),
        "slack" => new Color(0.69f, 0.45f, 1f),
        "badge" => new Color(1f, 0.60f, 0.25f),
        "transfer" => new Color(1f, 0.38f, 0.42f),
        "vpn" => new Color(0.20f, 0.80f, 0.78f),
        "audit" => new Color(1f, 0.42f, 0.75f),
        "ticket" => new Color(1f, 0.80f, 0.20f),
        "calendar" => new Color(0.46f, 0.85f, 0.25f),
        "news" => new Color(0.95f, 0.95f, 0.95f),
        "facilities" => new Color(0.55f, 0.75f, 0.95f),
        "testimony" => new Color(1f, 0.86f, 0.45f),
        _ => new Color(0.75f, 0.78f, 0.85f),
    };

    public static Color EmotionColor(string emotion) => emotion switch
    {
        "nervous" => new Color(1f, 0.80f, 0.25f),
        "angry" => new Color(1f, 0.36f, 0.40f),
        "evasive" => new Color(0.69f, 0.45f, 1f),
        _ => new Color(0.46f, 0.85f, 0.25f),
    };

    public static string EmotionLabel(string emotion) => emotion switch
    {
        "nervous" => "NERVOUS",
        "angry" => "ANGRY",
        "evasive" => "EVASIVE",
        _ => "CALM",
    };
}
