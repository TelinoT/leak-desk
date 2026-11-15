using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class WindowDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [SerializeField] private RectTransform window;

    private Canvas canvas;
    private RectTransform area;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        area = window.parent as RectTransform;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        window.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        window.anchoredPosition += eventData.delta / canvas.scaleFactor;

        Vector2 limit = (area.rect.size - window.rect.size) * 0.5f;
        Vector2 pos = window.anchoredPosition;
        pos.x = Mathf.Clamp(pos.x, -limit.x, limit.x);
        pos.y = Mathf.Clamp(pos.y, -limit.y, limit.y);
        window.anchoredPosition = pos;
    }
}
