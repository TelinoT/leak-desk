using System.Collections.Generic;
using UnityEngine;

public class WindowManager : MonoBehaviour
{
    public static WindowManager Instance { get; private set; }

    [SerializeField] private Transform windowsRoot;

    private readonly Dictionary<ToolDefinition, DesktopWindow> windows = new();

    void Awake()
    {
        Instance = this;
        foreach (var window in windowsRoot.GetComponentsInChildren<DesktopWindow>(true))
        {
            windows[window.Tool] = window;
        }
    }

    public void Open(ToolDefinition tool)
    {
        if(windows.TryGetValue(tool, out var window)) window.Open();
        else Debug.LogWarning($"Window {tool} not found");
    }
}
