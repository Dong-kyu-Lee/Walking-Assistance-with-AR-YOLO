using UnityEngine;
using Google.XR.Cardboard;

public class CardboardDeviceParamsController : MonoBehaviour
{
/*    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 저장된 Cardboard HMD 파라미터가 없으면 최초 1회 스캔 유도
        if (!Api.HasDeviceParams())
        {
            Api.ScanDeviceParams();
        }
#endif
    }

    private void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // Cardboard 우측 상단 설정 버튼
        if (Api.IsGearButtonPressed)
        {
            Api.ScanDeviceParams();
        }

        // QR 스캔 후 새로운 기기 파라미터가 저장되었는지 확인
        if (Api.HasNewDeviceParams())
        {
            Api.ReloadDeviceParams();
        }

        // 화면 방향/크기 변경 반영
        Api.UpdateScreenParams();
#endif
    }*/
}