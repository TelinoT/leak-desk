using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>Tiny squash-and-stretch on hover/press so buttons feel bouncy and casual.</summary>
public class ButtonPop : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    float target = 1f;
    float current = 1f;

    public void OnPointerEnter(PointerEventData e)
    {
        target = 1.04f;
        var b = GetComponent<UnityEngine.UI.Selectable>();
        if (b == null || b.interactable) Sfx.Play("hover", Random.Range(0.95f, 1.1f));
    }
    public void OnPointerExit(PointerEventData e) => target = 1f;
    public void OnPointerDown(PointerEventData e) => target = 0.94f;
    public void OnPointerUp(PointerEventData e) => target = 1.04f;

    void OnDisable()
    {
        target = current = 1f;
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        if (Mathf.Approximately(current, target)) return;
        current = Mathf.Lerp(current, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        if (Mathf.Abs(current - target) < 0.001f) current = target;
        transform.localScale = new Vector3(current, current, 1f);
    }
}
