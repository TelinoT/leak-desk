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

    const int PortraitLayer = 31; // keep the office out of the portrait camera and the portrait out of the main camera
    readonly Dictionary<string, GameObject> spawned = new();
    Camera cam;
    RenderTexture rt;
    RawImage image;
    GameObject currentGo;
    float bobTime;
    string reaction = "calm";
    float reactionTime;

    void Awake() => Instance = this;

    System.Collections.IEnumerator Start()
    {
        if (characters.Count == 0) { enabled = false; yield break; }
        yield return null; // let LeakDeskGame build the UI first

        rt = new RenderTexture(textureSize, textureSize, 16) { name = "SuspectPortraitRT" };
        var camGo = new GameObject("PortraitCamera");
        camGo.transform.SetParent(transform, false);
        camGo.transform.position = stageOffset + CamOffset;
        camGo.transform.LookAt(stageOffset + LookOffset);
        cam = camGo.AddComponent<Camera>();
        cam.targetTexture = rt;
        cam.fieldOfView = 26f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0, 0, 0, 0);
        cam.cullingMask = 1 << PortraitLayer;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 10f;

        var lightGo = new GameObject("PortraitLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.position = stageOffset + new Vector3(0.8f, 2.4f, 1.6f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 6f;
        light.intensity = 3f;
        light.cullingMask = 1 << PortraitLayer;

        var game = LeakDeskGame.Instance;
        var panel = FindAnyObjectByType<DeskUI>()?.Interrogation;
        if (panel != null)
        {
            var slot = panel.PortraitSlot;
            // Clip the render to the round avatar.
            if (slot.GetComponent<Mask>() == null) slot.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            image = UiKit.Rect("Portrait", slot).Stretch().gameObject.AddComponent<RawImage>();
            image.texture = rt;
            image.raycastTarget = false;
            if (panel.Current != null) Show(panel.Current.Data);
        }
        if (game == null) enabled = false;
    }

    // Portrait framing: head and shoulders.
    static readonly Vector3 CamOffset = new(0, 1.62f, 1.25f);
    static readonly Vector3 LookOffset = new(0, 1.52f, 0);

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
                go = Instantiate(entry.characterPrefab, stageOffset, Quaternion.identity, transform);
                foreach (var a in go.GetComponentsInChildren<Animator>()) a.enabled = false;
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PortraitLayer;
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
        Quaternion rot = Quaternion.Euler(0, sway, 0);
        switch (reaction)
        {
            case "nervous":
                pos += new Vector3(Mathf.Sin(bobTime * 31f) * 0.006f, 0, 0);
                break;
            case "angry":
                pos += new Vector3(0, 0, Mathf.Clamp01(reactionTime * 4f) * 0.12f);
                break;
            case "evasive":
                rot = Quaternion.Euler(0, 25f * Mathf.Clamp01(reactionTime * 3f) + sway, 0);
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
