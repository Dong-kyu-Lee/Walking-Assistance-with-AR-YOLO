using System.Collections;
using UnityEngine;

public class AppManager : MonoBehaviour
{
    public ARYOLOInput aryoloInput;
    public AppLoadingUI appLoadingUI;
    [SerializeField] float loadingDelay = 5f; // 앱 실행 로딩 UI가 활성화된 상태로 유지되는 시간

    void Start()
    {
        // 초기화
        if(aryoloInput == null) aryoloInput = FindFirstObjectByType<ARYOLOInput>();
        if(appLoadingUI == null) appLoadingUI = FindFirstObjectByType<AppLoadingUI>();

        // 앱 실행 로직
        StartCoroutine(AppLoadCoroutine());
    }

    IEnumerator AppLoadCoroutine()
    {
        // 모델이 로드될 때까지 대기
        while (!aryoloInput.yoloProcessor.IsModelLoaded)
        {
            yield return null; // 다음 프레임까지 대기
        }
        // 앱 실행 로딩 UI 활성화
        appLoadingUI.SetLoadingUI(true);
        // 5초 대기 후 앱 실행 로딩 UI 비활성화
        yield return new WaitForSeconds(loadingDelay);
        appLoadingUI.SetLoadingUI(false);
        // 앱 실행 완료 후 추가 로직
        aryoloInput.ConnectModelInference();
    }
}
