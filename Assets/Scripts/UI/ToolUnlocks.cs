using System;
using System.Collections.Generic;
using UnityEngine;

public class ToolUnlocks : MonoBehaviour
{
    public static ToolUnlocks Instance { get; private set; }

    public event Action<ToolDefinition> onToolUnlocked;
    [SerializeField] private List<ToolDefinition> allTools = new();
    private readonly HashSet<ToolDefinition> unlocked = new();
    
    public IReadOnlyList<ToolDefinition> AllTools => this.allTools;

    private void Awake()
    {

        Instance = this;
        
        foreach (var tool in this.allTools)
        {
            if(tool.unlockedAtStart) unlocked.Add(tool);
        }
    }
    
    public bool isUnlocked(ToolDefinition tool) => this.unlocked.Contains(tool);

    public void Unlock(ToolDefinition tool)
    {
        if (unlocked.Add(tool))
        {
            onToolUnlocked?.Invoke(tool);
        }
    }

    [ContextMenu("Unlocks All Tools")]
    private void UnlockAll()
    {
        foreach (var tool in this.allTools)
        {
            Unlock(tool);
        }
    }

}
