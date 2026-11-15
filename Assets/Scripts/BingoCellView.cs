using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BingoCellView : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Button button;

    public Button Button => button;

    public void Show(string text, Color backgroundColor, Color textColor)
    {
        label.text = text;
        background.color = backgroundColor;
        label.color = textColor;
    }
}