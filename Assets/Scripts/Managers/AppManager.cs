using System.Collections;
using UnityEngine;

public class AppManager : MonoBehaviour
{
    public ARYOLOInput ARYOLOInput;
    public AppLoadingUI AppLoadingUI;

    void Start()
    {
        // 초기화
        if(ARYOLOInput == null) ARYOLOInput = FindFirstObjectByType<ARYOLOInput>();
        if(AppLoadingUI == null) AppLoadingUI = FindFirstObjectByType<AppLoadingUI>();

        // 앱 실행 로직
        StartCoroutine(AppLoadCoroutine());
    }

    IEnumerator AppLoadCoroutine()
    {
        // 모델이 로드될 때까지 대기
        while (!ARYOLOInput.yoloProcessor.IsModelLoaded)
        {
            yield return null; // 다음 프레임까지 대기
        }
        // 앱 실행 로딩 UI 활성화
        AppLoadingUI.SetLoadingUI(true);
        // 5초 대기 후 앱 실행 로딩 UI 비활성화
        yield return new WaitForSeconds(5f);
        AppLoadingUI.SetLoadingUI(false);
        // 앱 실행 완료 후 추가 로직
        ARYOLOInput.ConnectModelInference();
    }
}
