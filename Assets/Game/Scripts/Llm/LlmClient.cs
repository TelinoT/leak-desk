using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Sends chat requests to the Cloudflare Worker proxy. Coroutine-based so it works in WebGL (no threads).
/// Request:  {"system": string, "messages": [{role, content}], "max_tokens": int, "model": string}
/// Response: {"text": string} or {"error": string}
/// </summary>
public class LlmClient : MonoBehaviour
{
    static LlmClient instance;

    public static LlmClient Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("[LlmClient]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<LlmClient>();
            }
            return instance;
        }
    }

    /// <summary>Number of requests currently in flight (handy for UI spinners).</summary>
    public int Pending { get; private set; }

    public void Send(string system, IReadOnlyList<LlmMessage> messages, Action<string> onSuccess, Action<string> onError,
        int maxTokens = 0, string model = null)
    {
        StartCoroutine(SendRoutine(system, messages, onSuccess, onError, maxTokens, model));
    }

    IEnumerator SendRoutine(string system, IReadOnlyList<LlmMessage> messages, Action<string> onSuccess,
        Action<string> onError, int maxTokens, string model)
    {
        var config = LlmConfig.Load();
        if (config == null || string.IsNullOrEmpty(config.proxyUrl))
        {
            onError?.Invoke("LLM is not configured (missing LlmConfig asset or proxy URL).");
            yield break;
        }

        var body = new JObject
        {
            ["system"] = system ?? "",
            ["messages"] = JArray.FromObject(messages),
            ["max_tokens"] = maxTokens > 0 ? maxTokens : config.maxTokens,
            ["model"] = string.IsNullOrEmpty(model) ? config.model : model,
        };
        byte[] payload = Encoding.UTF8.GetBytes(body.ToString(Formatting.None));

        Pending++;
        try
        {
            // One automatic retry for network hiccups and 5xx; 4xx means our request is wrong, so no retry.
            for (int attempt = 0; attempt < 2; attempt++)
            {
                using var req = new UnityWebRequest(config.proxyUrl, UnityWebRequest.kHttpVerbPOST);
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("content-type", "application/json");
                req.SetRequestHeader("x-game-token", config.gameToken);
                req.timeout = config.timeoutSeconds;

                yield return req.SendWebRequest();

                string raw = req.downloadHandler?.text;
                if (req.result == UnityWebRequest.Result.Success)
                {
                    string text = null;
                    try { text = JObject.Parse(raw)["text"]?.ToString(); }
                    catch (Exception e) { Debug.LogWarning($"[LlmClient] Bad proxy JSON: {e.Message}\n{raw}"); }

                    if (text != null) { onSuccess?.Invoke(text); yield break; }
                    onError?.Invoke("Proxy returned no text.");
                    yield break;
                }

                bool retryable = req.responseCode == 0 || req.responseCode >= 500;
                string err = ExtractError(raw) ?? req.error;
                Debug.LogWarning($"[LlmClient] Attempt {attempt + 1} failed ({req.responseCode}): {err}");
                if (!retryable || attempt == 1) { onError?.Invoke(err); yield break; }
                yield return new WaitForSecondsRealtime(0.8f);
            }
        }
        finally
        {
            Pending--;
        }
    }

    static string ExtractError(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return null;
        try { return JObject.Parse(raw)["error"]?.ToString(); }
        catch { return null; }
    }
}
