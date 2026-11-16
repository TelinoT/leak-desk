using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// All sound is synthesized at startup (no audio files): UI clicks, notification chime, evidence slam,
/// "cracked" sting, flag swipe, and per-character speech babble (Animal Crossing style) at a voice pitch.
/// </summary>
public class Sfx : MonoBehaviour
{
    static Sfx instance;
    const int Rate = 44100;

    readonly Dictionary<string, AudioClip> clips = new();
    AudioSource oneShots, babble, ambience;
    float nextBlip;

    public static float Volume = 0.8f;

    /// <summary>Global mute (the top-bar speaker button).</summary>
    public static bool Muted
    {
        get => AudioListener.volume < 0.01f;
        set => AudioListener.volume = value ? 0f : 1f;
    }

    public static Sfx Instance
    {
        get
        {
            if (instance == null)
            {
                var go = new GameObject("[Sfx]");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<Sfx>();
            }
            return instance;
        }
    }

    void Awake()
    {
        instance = this;
        oneShots = gameObject.AddComponent<AudioSource>();
        babble = gameObject.AddComponent<AudioSource>();
        ambience = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = babble.playOnAwake = ambience.playOnAwake = false;

        clips["click"] = Make(0.05f, t => Sine(t, 1300f - t * 9000f) * Env(t, 0.002f, 0.05f) * 0.5f);
        clips["hover"] = Make(0.03f, t => Sine(t, 1800f) * Env(t, 0.002f, 0.03f) * 0.15f);
        clips["chime"] = Make(0.6f, t => (Sine(t, 880f) * Env(t, 0.005f, 0.35f) + Sine(Mathf.Max(0, t - 0.12f), 1320f) * Env(t - 0.12f, 0.005f, 0.45f)) * 0.35f);
        clips["flag"] = Make(0.22f, t => (Noise() * 0.6f + Sine(t, 300f + t * 2400f) * 0.4f) * Env(t, 0.01f, 0.2f) * 0.35f);
        clips["slam"] = Make(0.45f, t => (Sine(t, 70f - t * 60f) * 1.2f + Noise() * Mathf.Exp(-t * 40f)) * Env(t, 0.001f, 0.42f) * 0.8f);
        clips["stamp"] = Make(0.35f, t => (Sine(t, 110f - t * 120f) + Noise() * Mathf.Exp(-t * 30f) * 0.8f) * Env(t, 0.001f, 0.3f) * 0.7f);
        clips["crack"] = Make(1.1f, t => (Square(t, 520f - t * 380f) * 0.25f + Noise() * Mathf.Exp(-t * 6f) * 0.4f + Sine(t, 55f) * Mathf.Exp(-t * 3f)) * Env(t, 0.002f, 1.05f) * 0.6f);
        clips["unlock"] = Make(0.5f, t => (Sine(t, 660f) * Env(t, 0.004f, 0.18f) + Sine(Mathf.Max(0, t - 0.09f), 990f) * Env(t - 0.09f, 0.004f, 0.18f) + Sine(Mathf.Max(0, t - 0.18f), 1320f) * Env(t - 0.18f, 0.004f, 0.3f)) * 0.3f);
        clips["tick"] = Make(0.04f, t => Square(t, 2000f) * Env(t, 0.001f, 0.035f) * 0.12f);
        clips["win"] = Make(1.2f, t => (Sine(t, 523f) * Env(t, 0.01f, 0.4f) + Sine(Mathf.Max(0, t - 0.15f), 659f) * Env(t - 0.15f, 0.01f, 0.4f) + Sine(Mathf.Max(0, t - 0.3f), 784f) * Env(t - 0.3f, 0.01f, 0.4f) + Sine(Mathf.Max(0, t - 0.45f), 1046f) * Env(t - 0.45f, 0.01f, 0.7f)) * 0.3f);
        clips["lose"] = Make(1.0f, t => (Square(t, 300f - t * 160f) * 0.25f) * Env(t, 0.01f, 0.95f) * 0.5f);
        clips["whoosh"] = Make(0.32f, t => Noise() * Mathf.Sin(Mathf.Clamp01(t / 0.32f) * Mathf.PI) * 0.22f * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI * (200f + t * 1800f))));
        clips["blip"] = Make(0.06f, t => Square(t, 440f) * Env(t, 0.003f, 0.055f) * 0.18f);
        clips["ambience"] = MakeAmbience();

        ambience.clip = clips["ambience"];
        ambience.loop = true;
        ambience.volume = 0.18f * Volume;
        ambience.Play();
    }

    // ---------- public API ----------

    public static void Play(string name, float pitch = 1f, float volume = 1f)
    {
        var s = Instance;
        if (!s.clips.TryGetValue(name, out var clip)) return;
        s.oneShots.pitch = pitch;
        s.oneShots.PlayOneShot(clip, volume * Volume);
    }

    /// <summary>One speech blip for a typed character. Voice pitch ~0.7 (deep) to ~1.6 (high).</summary>
    public static void Babble(char c, float voicePitch)
    {
        var s = Instance;
        if (char.IsWhiteSpace(c) || char.IsPunctuation(c) || Time.unscaledTime < s.nextBlip) return;
        s.nextBlip = Time.unscaledTime + 0.055f;
        s.babble.pitch = voicePitch * (0.9f + (c % 7) * 0.035f);
        s.babble.PlayOneShot(s.clips["blip"], 0.9f * Volume);
    }

    // ---------- synthesis ----------

    static AudioClip Make(float seconds, System.Func<float, float> f)
    {
        int n = Mathf.CeilToInt(seconds * Rate);
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)Rate), -1f, 1f);
        var clip = AudioClip.Create("sfx", n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip MakeAmbience()
    {
        // Low office hum: filtered noise + a faint 50 Hz mains tone, 4 s loop.
        int n = Rate * 4;
        var data = new float[n];
        float lp = 0;
        for (int i = 0; i < n; i++)
        {
            lp += (Noise() - lp) * 0.02f;
            float t = i / (float)Rate;
            data[i] = lp * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 50f) * 0.03f;
        }
        // Crossfade the ends for a seamless loop.
        int fade = Rate / 4;
        for (int i = 0; i < fade; i++)
        {
            float k = i / (float)fade;
            data[i] = data[i] * k + data[n - fade + i] * (1 - k);
        }
        var clip = AudioClip.Create("ambience", n - fade, 1, Rate, false);
        var trimmed = new float[n - fade];
        System.Array.Copy(data, trimmed, n - fade);
        clip.SetData(trimmed, 0);
        return clip;
    }

    static float Sine(float t, float f) => Mathf.Sin(t * 2f * Mathf.PI * f);
    static float Square(float t, float f) => Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * f));
    static float Noise() => Random.value * 2f - 1f;

    static float Env(float t, float attack, float length)
    {
        if (t < 0) return 0;
        if (t < attack) return t / attack;
        return Mathf.Clamp01(1f - (t - attack) / Mathf.Max(0.0001f, length - attack));
    }
}
