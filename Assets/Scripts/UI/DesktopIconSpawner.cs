using UnityEngine;
using System.Collections.Generic;

public class DesktopIconSpawner : MonoBehaviour
{
    [SerializeField] private DesktopIcon iconPrefab;
    [SerializeField] private Transform iconContainer;

    private readonly Dictionary<ToolDefinition, DesktopIcon> icons = new();
    
    void Start()
    {
        foreach (var tool in ToolUnlocks.Instance.AllTools)
        {
            if (ToolUnlocks.Instance.isUnlocked(tool)) Spawn(tool);
        }

        ToolUnlocks.Instance.onToolUnlocked += Spawn;
    }

    void Spawn(ToolDefinition tool)
    {
        if (icons.ContainsKey(tool)) return;
        
        var icon = Instantiate(iconPrefab, iconContainer);
        icon.Setup(tool);
        icons.Add(tool, icon);
        
        foreach (var t in ToolUnlocks.Instance.AllTools)
            if (icons.TryGetValue(t, out var existing))
                existing.transform.SetAsLastSibling();
    }

    void OnDestroy()
    {
        if (ToolUnlocks.Instance != null)
        {
            ToolUnlocks.Instance.onToolUnlocked -= Spawn;
        }
    }
}
