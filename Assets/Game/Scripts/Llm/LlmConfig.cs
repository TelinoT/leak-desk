using UnityEngine;

/// <summary>
/// Where the game sends LLM requests. The real asset lives at Assets/Game/Resources/LlmConfig.asset
/// and is gitignored (it holds the game token). Create it via Assets > Create > Leak Desk > LLM Config.
/// The Anthropic API key never lives here - only in the Cloudflare Worker.
/// </summary>
[CreateAssetMenu(menuName = "Leak Desk/LLM Config", fileName = "LlmConfig")]
public class LlmConfig : ScriptableObject
{
    public const string ResourcePath = "LlmConfig";

    [Tooltip("Cloudflare Worker URL, e.g. https://leakdesk-proxy.<you>.workers.dev")]
    public string proxyUrl = "";

    [Tooltip("Shared token the Worker checks (x-game-token header). Not a real secret: it ships in the build.")]
    public string gameToken = "";

    [Tooltip("Model for live dialogue. The Worker only accepts models on its allowlist.")]
    public string model = "claude-haiku-5-5";

    [Tooltip("Model for the board verdict (one call per round, so a smarter model is affordable).")]
    public string judgeModel = "claude-haiku-5-5";

    [Range(64, 1024)] public int maxTokens = 400;

    [Tooltip("Seconds before a request is abandoned.")]
    public int timeoutSeconds = 30;

    static LlmConfig cached;

    public static LlmConfig Load()
    {
        if (cached != null) return cached;
        cached = Resources.Load<LlmConfig>(ResourcePath);
        if (cached == null)
            Debug.LogError("[LlmConfig] Missing Assets/Game/Resources/LlmConfig.asset. Create it via Assets > Create > Leak Desk > LLM Config.");
        return cached;
    }
}
