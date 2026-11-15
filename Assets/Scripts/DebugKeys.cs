using UnityEngine;
using UnityEngine.InputSystem;

public class DebugKeys : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f1Key.wasPressedThisFrame) DayManager.Instance.EndShift();
        if (kb.f2Key.wasPressedThisFrame) DayManager.Instance.NextDay();
        if (kb.f3Key.wasPressedThisFrame) RunState.Instance.ChangeStars(-1f);
        if (kb.f4Key.wasPressedThisFrame) RunState.Instance.ChangeBudget(500);
    }
#endif
}