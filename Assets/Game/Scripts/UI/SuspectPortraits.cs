using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders the selected suspect's 3D character (Synty office characters) into the interrogation header
/// via a RenderTexture. Optional: if no character prefabs are assigned, the colored initials avatar stays.
/// </summary>
public class SuspectPortraits : MonoBehaviour
{
    public static SuspectPortraits Instance { get; private set; }

    [System.Serializable]
    public class Entry
    {
        public string suspectId;
        public GameObject characterPrefab;
    }

    [SerializeField] List<Entry> characters = new();
    [SerializeField] Vector3 stageOffset = new(0, -500, 0);
    [SerializeField] int textureSize = 512;

    readonly Dictionary<string, GameObject> spawned = new();
    Camera cam;
    RenderTexture rt;
    RawImage image;
    GameObject currentGo;
    float bobTime;
    string reaction = "calm";
    float reactionTime;

    void Awake() => Instance = this;

    void Start()
    {
        if (characters.Count == 0) { enabled = false; return; }

        rt = new RenderTexture(textureSize, textureSize, 16) { name = "SuspectPortraitRT" };
        var camGo = new GameObject("PortraitCamera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.position = stageOffset + new Vector3(0, 1.55f, 1.6f);
        camGo.transform.LookAt(stageOffset + new Vector3(0, 1.45f, 0));
        cam = camGo.AddComponent<Camera>();
        cam.targetTexture = rt;
        cam.fieldOfView = 26f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 10f;

        var lightGo = new GameObject("PortraitLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.position = stageOffset + new Vector3(0.8f, 2.4f, 1.6f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 6f;
        light.intensity = 3f;

        var game = LeakDeskGame.Instance;
        var panel = FindAnyObjectByType<DeskUI>()?.Interrogation;
        if (panel != null)
        {
            var slot = panel.PortraitSlot;
            image = UiKit.Rect("Portrait", slot).Stretch(-6, -6, -6, -6).gameObject.AddComponent<RawImage>();
            image.texture = rt;
            image.raycastTarget = false;
            if (panel.Current != null) Show(panel.Current.Data);
        }
        if (game == null) enabled = false;
    }

    public void Show(SuspectData data)
    {
        if (!enabled || cam == null) return;
        if (currentGo != null) currentGo.SetActive(false);
        currentGo = null;

        if (!spawned.TryGetValue(data.Id, out var go))
        {
            var entry = characters.Find(e => e.suspectId == data.Id);
            if (entry?.characterPrefab != null)
            {
                go = Instantiate(entry.characterPrefab, stageOffset, Quaternion.Euler(0, 180, 0), transform);
                foreach (var a in go.GetComponentsInChildren<Animator>()) a.enabled = false;
            }
            spawned[data.Id] = go;
        }

        if (go != null)
        {
            go.SetActive(true);
            currentGo = go;
        }
        if (image != null) image.enabled = currentGo != null;
        reaction = "calm";
    }

    public void React(SuspectData data, string emotion)
    {
        reaction = emotion;
        reactionTime = 0f;
    }

    void Update()
    {
        if (currentGo == null) return;
        bobTime += Time.deltaTime;
        reactionTime += Time.deltaTime;

        // Cheap "acting": idle sway, nervous jitter, angry lean, broken slump.
        float sway = Mathf.Sin(bobTime * 1.3f) * 3f;
        Vector3 pos = stageOffset;
        Quaternion rot = Quaternion.Euler(0, 180 + sway, 0);
        switch (reaction)
        {
            case "nervous":
                pos += new Vector3(Mathf.Sin(bobTime * 31f) * 0.006f, 0, 0);
                break;
            case "angry":
                pos += new Vector3(0, 0, Mathf.Clamp01(reactionTime * 4f) * 0.12f);
                break;
            case "evasive":
                rot = Quaternion.Euler(0, 180 + 25f * Mathf.Clamp01(reactionTime * 3f) + sway, 0);
                break;
            case "broken":
                pos += new Vector3(0, -0.08f * Mathf.Clamp01(reactionTime * 2f), 0);
                rot *= Quaternion.Euler(12f * Mathf.Clamp01(reactionTime * 2f), 0, 0);
                break;
        }
        currentGo.transform.SetPositionAndRotation(pos, rot);
    }

    void OnDestroy()
    {
        if (rt != null) rt.Release();
    }
}
