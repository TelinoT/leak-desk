using Newtonsoft.Json.Linq;

/// <summary>Defensive helpers for pulling a JSON object out of a model reply.</summary>
public static class LlmJson
{
    /// <summary>
    /// Finds the first balanced {...} block in the text (models sometimes wrap JSON in prose or code fences)
    /// and parses it. Returns null when nothing parseable is found.
    /// </summary>
    public static JObject TryParseObject(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        int start = text.IndexOf('{');
        while (start >= 0)
        {
            int depth = 0;
            bool inString = false, escaped = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') inString = false;
                    continue;
                }
                if (c == '"') inString = true;
                else if (c == '{') depth++;
                else if (c == '}' && --depth == 0)
                {
                    try { return JObject.Parse(text.Substring(start, i - start + 1)); }
                    catch { break; }
                }
            }
            start = text.IndexOf('{', start + 1);
        }
        return null;
    }
}
