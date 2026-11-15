using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CamFeedView : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private RawImage feedImage;
    [SerializeField] private TMP_Text labelText;
    [SerializeField] private Button expandButton; // optional

    public SecurityCamera Source { get; private set; }
    public event Action<CamFeedView> OnExpandClicked;

    private void Awake()
    {
        if (expandButton != null)
            expandButton.onClick.AddListener(() => OnExpandClicked?.Invoke(this));
    }

    public void Bind(SecurityCamera securityCamera)
    {
        if (securityCamera == null)
        {
            Debug.LogWarning($"{name}: Bind was called without a camera.", this);
            return;
        }
        if (feedImage == null)
        {
            Debug.LogWarning($"{name}: no Feed Image assigned.", this);
            return;
        }

        Source = securityCamera;
        feedImage.texture = securityCamera.Texture;
        if (labelText != null) labelText.text = securityCamera.Label;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Source == null) return;

        // 1. Where inside the feed image was the click? (0–1 on both axes)
        RectTransform rect = feedImage.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        Rect r = rect.rect;
        var viewport = new Vector2((local.x - r.x) / r.width, (local.y - r.y) / r.height);

        // 2. Shoot a ray from the 3D camera through that same point
        Ray ray = Source.Camera.ViewportPointToRay(viewport);
        GuestAgent guest = null;
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            guest = hit.collider.GetComponentInParent<GuestAgent>();

        // 3. Select whoever was hit (or clear the selection on a miss)
        GuestSelection.Instance.Select(guest);
    }
}