using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Spoken lines through the Worker's /tts route (Fish Audio). Coroutine-based, so it works in WebGL.
/// Every caller has a fallback: if there is no voice id, the request fails, or it takes too long,
/// <see cref="Speak"/> hands back null and the UI uses the synthesized babble instead.
/// </summary>
public class Voice : MonoBehaviour
{
    const float Timeout = 5f;
    const string PrefKey = "leakdesk_voices";

    static Voice instance;
    AudioSource source;
    int requestId;
    int failures;

    public static Voice Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("[Voice]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Voice>();
            }
            return instance;
        }
    }

    /// <summary>Player setting (on by default). After a few failed requests in a row the game stops trying.</summary>
    public static bool Enabled
    {
        get => PlayerPrefs.GetInt(PrefKey, 1) == 1 && (instance == null || instance.failures < 3);
        set
        {
            PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            if (instance != null) instance.failures = 0;
            if (!value) Stop();
        }
    }

    readonly System.Collections.Generic.Dictionary<string, AudioClip> cache = new();

    static string Key(string voiceId, string text) => voiceId + "|" + Spoken(text);

    /// <summary>Fixed lines (the tutorial) are fetched one by one in the background and kept for later.</summary>
    public static void Prefetch(string voiceId, System.Collections.Generic.IEnumerable<string> lines, float speed = 1.08f)
    {
        if (!Enabled || string.IsNullOrEmpty(voiceId)) return;
        var v = Instance;
        v.StartCoroutine(v.PrefetchRoutine(voiceId, new System.Collections.Generic.List<string>(lines), speed));
    }

    IEnumerator PrefetchRoutine(string voiceId, System.Collections.Generic.List<string> lines, float speed)
    {
        foreach (var line in lines)
        {
            string key = Key(voiceId, line);
            if (cache.ContainsKey(key) || !Enabled) continue;
            AudioClip got = null;
            yield return Fetch(-1, voiceId, Spoken(line), speed, clip => got = clip);
            if (got != null) cache[key] = got;
        }
    }

    public static AudioClip Cached(string voiceId, string text) =>
        instance != null && !string.IsNullOrEmpty(voiceId) && instance.cache.TryGetValue(Key(voiceId, text), out var c) ? c : null;

    public static AudioClip Current => instance != null && instance.source != null && instance.source.isPlaying ? instance.source.clip : null;

    public static bool Speaking => instance != null && instance.source != null && instance.source.isPlaying;

    void Awake()
    {
        instance = this;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.volume = 1f;
    }

    void Update() => Sfx.Duck(source.isPlaying);

    /// <summary>
    /// Fetches the line and calls <paramref name="ready"/> with the clip once it can play (not yet playing),
    /// or with null on any failure/timeout. Call <see cref="Play"/> with the clip to start it.
    /// </summary>
    public static void Speak(string voiceId, string text, Action<AudioClip> ready, float speed = 1.08f)
    {
        string spoken = Spoken(text);
        if (!Enabled || string.IsNullOrEmpty(voiceId) || spoken.Length < 2) { ready?.Invoke(null); return; }
        var v = Instance;
        v.StartCoroutine(v.Fetch(++v.requestId, voiceId, spoken, speed, ready));
    }

    public static float Play(AudioClip clip)
    {
        if (clip == null) return 0f;
        var v = Instance;
        v.source.Stop();
        v.source.clip = clip;
        v.source.Play();
        return clip.length;
    }

    public static void Stop()
    {
        if (instance == null) return;
        instance.requestId++; // a line still loading is dropped when it arrives
        if (instance.source != null) instance.source.Stop();
    }

    IEnumerator Fetch(int id, string voiceId, string text, float speed, Action<AudioClip> ready)
    {
        var config = LlmConfig.Load();
        if (config == null || string.IsNullOrEmpty(config.proxyUrl)) { ready?.Invoke(null); yield break; }

        var body = new JObject { ["text"] = text, ["voice"] = voiceId, ["speed"] = speed };
        using var req = new UnityWebRequest(config.proxyUrl.TrimEnd('/') + "/tts", UnityWebRequest.kHttpVerbPOST);
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString(Newtonsoft.Json.Formatting.None)));
        var dl = new DownloadHandlerAudioClip(req.uri, AudioType.MPEG)
        {
            // Decode the whole line up front: a streamed clip dies with the request (and we dispose it below).
            streamAudio = false,
            compressed = false,
        };
        req.downloadHandler = dl;
        req.SetRequestHeader("content-type", "application/json");
        req.SetRequestHeader("x-game-token", config.gameToken);

        float start = Time.unscaledTime;
        bool gaveUp = false;
        var op = req.SendWebRequest();
        while (!op.isDone)
        {
            if (!gaveUp && id >= 0 && Time.unscaledTime - start > Timeout)
            {
                gaveUp = true;          // the UI moves on with babble; this request finishes in the background
                ready?.Invoke(null);
                ready = null;
            }
            yield return null;
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            failures++;
            Debug.LogWarning($"[Voice] tts failed ({req.responseCode}): {req.error}");
            ready?.Invoke(null);
            yield break;
        }

        AudioClip clip = null;
        try { clip = dl.audioClip; }
        catch (Exception e) { Debug.LogWarning($"[Voice] could not decode audio: {e.Message}"); }
        // In WebGL the browser decodes the mp3 asynchronously: the clip can start out Unloaded, so wait until it
        // is actually Loaded before touching its length (reading it early counts as a failure and mutes voices).
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        while (clip != null && clip.loadState != AudioDataLoadState.Loaded && clip.loadState != AudioDataLoadState.Failed
               && Time.unscaledTime - start < Timeout + 4f)
            yield return null;

        if (clip == null || clip.loadState != AudioDataLoadState.Loaded || clip.length < 0.1f)
        {
            failures++;
            ready?.Invoke(null);
            yield break;
        }
        failures = 0;
        if (id >= 0 && id != requestId) { ready?.Invoke(null); yield break; } // superseded (switched suspect, ...): show text, no audio
        ready?.Invoke(clip);
    }

    /// <summary>What actually gets read out: no *stage directions*, rich-text tags, bullets or emoticons.</summary>
    public static string Spoken(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        text = Regex.Replace(text, @"<[^>]+>", "");
        text = Regex.Replace(text, @"\*[^*]{1,120}\*", " ");
        text = Regex.Replace(text, @":[a-z][a-z0-9_+-]{1,29}:", "");
        text = text.Replace("^_^", "").Replace("...", "… ").Replace("—", ", ").Replace(" - ", ", ");
        text = Regex.Replace(text, @"^\s*[-•]\s*", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length > 480 ? text.Substring(0, 480) : text;
    }

    /// <summary>Types a bubble out in step with a playing clip (no babble), or with babble when there is no clip.</summary>
    public static void TypeAlong(TMPro.TMP_Text label, AudioClip clip, float babbleCps, float babblePitch)
    {
        if (clip == null) { UiFx.Typewrite(label, babbleCps, babblePitch); return; }
        float seconds = Play(clip);
        label.ForceMeshUpdate();
        float cps = Mathf.Max(20f, label.textInfo.characterCount / Mathf.Max(0.5f, seconds * 0.92f));
        UiFx.Typewrite(label, cps, 0f);
    }
}
