using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AppLoadingUI : MonoBehaviour
{
    [SerializeField] Image logo;
    [SerializeField] Image leftBackground;
    [SerializeField] Image rightBackground;

    public ARYOLOInput model;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(logo == null) Debug.LogError("로고 이미지가 할당되지 않았습니다.");
        if(leftBackground == null) Debug.LogError("왼쪽 배경 이미지가 할당되지 않았습니다.");
        if(rightBackground == null) Debug.LogError("오른쪽 배경 이미지가 할당되지 않았습니다.");
        if(model == null) model = FindFirstObjectByType<ARYOLOInput>();
    }

    // 앱 실행 로딩 UI On/Off 제어
    public void SetLoadingUI(bool isActive)
    {
        logo.gameObject.SetActive(isActive);
        leftBackground.gameObject.SetActive(isActive);
        rightBackground.gameObject.SetActive(isActive);
    }
}
