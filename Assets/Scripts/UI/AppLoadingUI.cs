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

        StartCoroutine(AddLoadingUICoroutine());
    }

    IEnumerator AddLoadingUICoroutine()
    {
        // 모델이 로드될 때까지 대기
        while (!model.yoloProcessor.IsModelLoaded)
        {
            yield return null; // 다음 프레임까지 대기
        }

        // 모델이 로드되면 5초 후에 UI 요소들을 제거
        yield return new WaitForSeconds(5f);
        logo.gameObject.SetActive(false);
        leftBackground.gameObject.SetActive(false);
        rightBackground.gameObject.SetActive(false);

        // 모델 추론 시작
    }
}
