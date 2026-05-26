using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SettingBtn : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    [SerializeField] private Image image;
    [SerializeField] private TextMeshProUGUI text;

    [SerializeField] private Color settingColor = Color.yellow;
    [SerializeField] private Color unsettingColor = Color.white;

    public void IsSetting()
    {
        image.color = settingColor;
    }

    public void IsNotSetting()
    {
        image.color = unsettingColor;
    }

    public void OnSelect(BaseEventData eventData)
    {
        image.color = settingColor;
    }

    public void OnDeselect(BaseEventData eventData)
    {
        image.color = unsettingColor;
    }
}
