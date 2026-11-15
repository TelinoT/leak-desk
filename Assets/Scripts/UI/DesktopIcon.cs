using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
public class DesktopIcon : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text label;

    private ToolDefinition tool;

    public void Setup(ToolDefinition toolDefinition)
    {
        tool = toolDefinition;
        label.text = tool.displayName;
        if (tool.icon != null) iconImage.sprite = tool.icon;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.clickCount >= 2)
        {
            WindowManager.Instance.Open(tool);
        }
    }
}
