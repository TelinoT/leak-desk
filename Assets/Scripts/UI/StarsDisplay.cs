using TMPro;
using UnityEngine;

public class StarsDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text text;

    private void Start()
    {
        RunState.Instance.onChanged += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (RunState.Instance != null) RunState.Instance.onChanged -= Refresh;
    }

    private void Refresh()
    {
        var run = RunState.Instance;
        text.text = $"{run.Stars:0.0} Stars     Protected ${run.MoneyProtected}     Budget ${run.Budget}";
    }
}