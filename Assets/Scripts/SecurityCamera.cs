using UnityEngine;

[RequireComponent(typeof(Camera))]
public class SecurityCamera : MonoBehaviour
{
    [SerializeField] private string label = "CAM 1";
    [SerializeField] private Vector2Int resolution = new(640, 360);

    public string Label => label;
    public Camera Camera { get; private set; }
    public RenderTexture Texture { get; private set; }

    private void Awake()
    {
        Camera = GetComponent<Camera>();
        Texture = new RenderTexture(resolution.x, resolution.y, 16) { name = $"Feed_{label}" };
        Camera.targetTexture = Texture;
        Camera.enabled = false; 
    }

    public void SetLive(bool live) => Camera.enabled = live;

    private void OnDestroy()
    {
        if (Texture == null) return;
        Camera.targetTexture = null;
        Texture.Release();
        Destroy(Texture);
    }
}