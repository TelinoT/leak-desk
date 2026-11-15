using System;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Loads Assets/Game/Resources/Cases/&lt;caseId&gt;/truth.json (Resources works synchronously in WebGL) and
/// prompt templates from Assets/Game/Resources/Prompts/.
/// </summary>
public static class CaseLoader
{
    public const string DefaultCaseId = "leak_01";

    public static CaseFile Load(string caseId = DefaultCaseId)
    {
        var asset = Resources.Load<TextAsset>($"Cases/{caseId}/truth");
        if (asset == null)
        {
            Debug.LogError($"[CaseLoader] Missing Resources/Cases/{caseId}/truth.json");
            return null;
        }

        try
        {
            var file = JsonConvert.DeserializeObject<CaseFile>(asset.text);
            Debug.Log($"[CaseLoader] Loaded '{file.Title}': {file.Suspects.Count} suspects, {file.Evidence.Count} evidence, {file.Documents.Count} documents.");
            return file;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CaseLoader] Could not parse {caseId}/truth.json: {e.Message}");
            return null;
        }
    }

    public static string Prompt(string name)
    {
        var asset = Resources.Load<TextAsset>($"Prompts/{name}");
        if (asset == null) Debug.LogError($"[CaseLoader] Missing prompt Resources/Prompts/{name}.txt");
        return asset != null ? asset.text : "";
    }

    /// <summary>Replaces {{key}} placeholders.</summary>
    public static string Fill(string template, params (string key, string value)[] values)
    {
        foreach (var (key, value) in values) template = template.Replace("{{" + key + "}}", value ?? "");
        return template;
    }
}
