using UnityEngine;

[CreateAssetMenu(menuName = "Too Lucky/Tool Definition", fileName = "Tool_")]
public class ToolDefinition : ScriptableObject
{
    public string displayName = "Tool";
    public Sprite icon;
    [TextArea]public string description;
    public bool unlockedAtStart = false;
}
