using System;
using UnityEngine;

public class GuestSelection : MonoBehaviour
{
    public static GuestSelection Instance { get; private set; }
    public event Action OnSelectionChanged;

    public GuestAgent Selected { get; private set; }
    private bool hasSelection;

    private void Awake() => Instance = this;

    private void Update()
    {
        // The selected guest left and was destroyed
        if (hasSelection && Selected == null)
        {
            hasSelection = false;
            OnSelectionChanged?.Invoke();
        }
    }

    public void Select(GuestAgent guest)
    {
        if (Selected != null) Selected.SetSelected(false);

        Selected = guest;
        hasSelection = guest != null;
        if (guest != null) guest.SetSelected(true);

        OnSelectionChanged?.Invoke();
    }
}