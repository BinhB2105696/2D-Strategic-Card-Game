using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Metadata remains stable even when the visual hierarchy is rearranged.
public sealed class AppFilterOption : MonoBehaviour
{
    public int group;
    public string value;
    public Button button;
    public TMP_Text label;
    public void Refresh(bool selected)
    {
        label.text = (selected ? "[x] " : "[ ] ") + value;
        button.GetComponent<Image>().color = selected
            ? new Color(.045f, .22f, .43f, 1f)
            : new Color(.035f, .095f, .18f, .98f);
    }
}
