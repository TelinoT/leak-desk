using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 3D suspect rendering. A tall "stage" RenderTexture shows the selected suspect waist-up with procedural acting
/// (SuspectRig), and every suspect gets a headshot snapshot for list/accusation avatars.
/// Optional: without character prefabs the UI keeps its colored initials.
/// </summary>
public class SuspectPortraits : MonoBehaviour
{
    public static SuspectPortraits Instance { get; private set; }

    [Serializable]
    public class Entry
    {
        public string suspectId;
        public GameObject characterPrefab;
    }

    [SerializeField] List<Entry> characters = new();
    [SerializeField] Vector3 stageOffset = new(0, -500, 0);
    [SerializeField] Vector2Int stageSize = new(640, 1024);
    [SerializeField] int headshotSize = 256;

    const int PortraitLayer = 31; // only the portrait cameras see this layer

    // Stage framing: waist-up, slightly from the side. Headshot framing: face and shoulders.
    static readonly Vector3 StageCam = new(0.25f, 1.30f, 3.2f);
    static readonly Vector3 StageLook = new(0, 1.12f, 0);
    static readonly Vector3 HeadCam = new(0.15f, 1.62f, 1.35f);
    static readonly Vector3 HeadLook = new(0, 1.55f, 0);

    readonly Dictionary<string, GameObject> spawned = new();
    readonly Dictionary<string, SuspectRig> rigs = new();
    readonly Dictionary<string, RenderTexture> headshots = new();
    Camera cam;
    string current;
    string reaction = "calm";
    float reactionTime, recoil;
    int stress;

    public RenderTexture StageTexture { get; private set; }
    public bool IsReady { get; private set; }

    /// <summary>Raised once the stage and headshots are rendered.</summary>
    public event Action Ready;

    public Texture Headshot(string suspectId) => headshots.TryGetValue(suspectId, out var t) ? t : null;

    void Awake() => Instance = this;

    IEnumerator Start()
    {
        if (characters.Count == 0) { enabled = false; yield break; }

        StageTexture = new RenderTexture(stageSize.x, stageSize.y, 24) { name = "SuspectStageRT", antiAliasing = 4 };

        var camGo = new GameObject("PortraitCamera");
        camGo.transform.SetParent(transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.cullingMask = 1 << PortraitLayer;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 10f;

        // Soft three-point lighting so the low-poly faces read well.
        AddLight("Key", new Vector3(-1.0f, 2.4f, 1.6f), new Color(1f, 0.93f, 0.86f), 3.2f);
        AddLight("Fill", new Vector3(1.3f, 1.4f, 1.6f), new Color(0.72f, 0.82f, 1f), 1.6f);
        AddLight("Rim", new Vector3(0.4f, 2.3f, -1.2f), Color.white, 2.6f);

        foreach (var e in characters) Spawn(e);

        yield return null; // let the rigs settle for one frame
        RenderHeadshots();

        SetFraming(StageCam, StageLook, 30f);
        cam.targetTexture = StageTexture;
        IsReady = true;
        Ready?.Invoke();
        if (current != null) Show(current);
    }

    void Spawn(Entry e)
    {
        if (e.characterPrefab == null) return;
        var go = Instantiate(e.characterPrefab, stageOffset, Quaternion.identity, transform);
        go.name = "Suspect_" + e.suspectId;
        foreach (var a in go.GetComponentsInChildren<Animator>()) a.enabled = false;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PortraitLayer;
        spawned[e.suspectId] = go;
        rigs[e.suspectId] = new SuspectRig(go);
        go.SetActive(false);
    }

    void RenderHeadshots()
    {
        SetFraming(HeadCam, HeadLook, 26f);
        foreach (var kv in spawned)
        {
            foreach (var other in spawned.Values) other.SetActive(other == kv.Value);
            rigs[kv.Key].Tick(0, "calm", 0, 0, 0);
            var rt = new RenderTexture(headshotSize, headshotSize, 24) { name = "Headshot_" + kv.Key, antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            headshots[kv.Key] = rt;
        }
        foreach (var go in spawned.Values) go.SetActive(false);
    }

    void SetFraming(Vector3 camOffset, Vector3 look, float fov)
    {
        cam.transform.position = stageOffset + camOffset;
        cam.transform.LookAt(stageOffset + look);
        cam.fieldOfView = fov;
    }

    void AddLight(string name, Vector3 offset, Color color, float intensity)
    {
        var go = new GameObject("Portrait" + name + "Light");
        go.transform.SetParent(transform, false);
        go.transform.position = stageOffset + offset;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 6f;
        l.color = color;
        l.intensity = intensity;
        l.cullingMask = 1 << PortraitLayer;
    }

    public void Show(SuspectData data) => Show(data.Id);

    void Show(string id)
    {
        current = id;
        if (!IsReady) return;
        foreach (var kv in spawned) kv.Value.SetActive(kv.Key == id);
        reaction = "calm";
        reactionTime = 0;
    }

    public void React(SuspectData data, string emotion, int stressLevel)
    {
        if (data.Id != current) return;
        if (emotion != reaction) reactionTime = 0;
        reaction = emotion;
        stress = stressLevel;
    }

    /// <summary>Short flinch, used when evidence is slammed on the table.</summary>
    public void Recoil() => recoil = 1f;

    void LateUpdate()
    {
        if (!IsReady || current == null || !rigs.TryGetValue(current, out var rig)) return;
        reactionTime += Time.deltaTime;
        recoil = Mathf.MoveTowards(recoil, 0, Time.deltaTime * 2.5f);
        rig.Tick(Time.time, reaction, Mathf.Clamp01(reactionTime * 2.5f), stress, recoil);
    }

    void OnDestroy()
    {
        if (StageTexture != null) StageTexture.Release();
        foreach (var rt in headshots.Values) rt.Release();
    }
}
