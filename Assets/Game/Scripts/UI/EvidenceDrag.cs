using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Makes an evidence card draggable. While dragging, a tilted ghost copy follows the pointer;
/// dropping it on a PresentDropZone presents the evidence to the current suspect.
/// </summary>
public class EvidenceDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public EvidenceItem Item;

    static RectTransform ghost;
    Canvas canvas;

    public static bool Dragging => ghost != null;

    public void OnBeginDrag(PointerEventData e)
    {
        canvas = GetComponentInParent<Canvas>().rootCanvas;
        var src = (RectTransform)transform;
        var copy = Instantiate(gameObject, canvas.transform);
        Destroy(copy.GetComponent<EvidenceDrag>());
        foreach (var g in copy.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
        ghost = (RectTransform)copy.transform;
        ghost.sizeDelta = src.rect.size;
        ghost.localRotation = Quaternion.Euler(0, 0, -6f);
        ghost.localScale = Vector3.one * 1.05f;
        var cg = copy.AddComponent<CanvasGroup>();
        cg.alpha = 0.92f;
        cg.blocksRaycasts = false;
        Move(e);
        PresentDropZone.SetHighlight(true);
        Sfx.Play("click", 0.8f);
    }

    public void OnDrag(PointerEventData e) => Move(e);

    public void OnEndDrag(PointerEventData e)
    {
        PresentDropZone.SetHighlight(false);
        var zone = e.pointerCurrentRaycast.gameObject != null
            ? e.pointerCurrentRaycast.gameObject.GetComponentInParent<PresentDropZone>()
            : null;
        if (zone != null && ghost != null)
        {
            zone.Accept(Item, ghost);
            ghost = null; // the zone animates and destroys it
            return;
        }
        if (ghost != null) Destroy(ghost.gameObject);
        ghost = null;
    }

    void Move(PointerEventData e)
    {
        if (ghost == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, e.position,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
        ghost.anchorMin = ghost.anchorMax = new Vector2(0.5f, 0.5f);
        ghost.pivot = new Vector2(0.5f, 0.5f);
        ghost.anchoredPosition = local;
    }
}
