using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
public class DesktopWindow : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private ToolDefinition tool;
    [SerializeField] private TMP_Text titleText;
    
    public ToolDefinition Tool => tool;

    public void Open()
    {
        if(titleText != null) titleText.text = tool.displayName;
        
        gameObject.SetActive(true);
        BringToFront();
    }

    void BringToFront()
    {
        transform.SetAsLastSibling();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        BringToFront();
    }
}
