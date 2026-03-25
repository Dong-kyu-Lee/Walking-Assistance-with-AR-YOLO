using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AppLoadingUI : MonoBehaviour
{
    [SerializeField]
    Graphic[] uiElements;
    [SerializeField]
    Text debugText;

    public ARYOLOInput model;

    void Start()
    {
        if(uiElements == null || uiElements.Length == 0) 
            Debug.LogError("UI 요소들이 할당되지 않았습니다. 인스펙터에서 UI 요소들을 할당해주세요.");
        if (model == null) model = FindFirstObjectByType<ARYOLOInput>();
    }

    // 앱 실행 로딩 UI On/Off 제어
    public void SetLoadingUI(bool isActive)
    {
        foreach (var element in uiElements)
        {
            if (element != null) element.gameObject.SetActive(isActive);
        }
    }
}
