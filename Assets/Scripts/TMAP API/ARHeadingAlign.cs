using UnityEngine;
using System.Collections;
using UnityEngine.Android;

public class ARHeadingAlign : MonoBehaviour
{
    public GameObject xrOrigin; // AR Session Origin (XR Origin)
    private bool isAligned = false;
    public IEnumerator AlignHeadingRoutine()
    {
        PathFindingUI.instance.ShowAlignErrorIndicator(true);

        // 1. 위치 권한 확인 및 위치 서비스 시작

        yield return new WaitForSeconds(3f);

        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
            while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                yield return new WaitForSeconds(0.5f); // 사용자가 [허용] 누를 때까지 대기
            }
        }


        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("[에러] 스마트폰의 위치 설정(GPS)이 꺼져있습니다.");
            PathFindingUI.instance.ShowText("[에러] 스마트폰의 위치 설정(GPS)이 꺼져있습니다.", IndicatorType.aline);
            yield break; // 여기서 멈춤
        }

        Input.location.Start(10f, 10f);
        Input.gyro.enabled = true;
        Input.compass.enabled = true;

        // 2. 0.2초가 아니라, GPS가 완전히 켜질 때까지 최대 15초간 대기합니다.
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1); // 1초씩 기다림
            maxWait--;
        }

        int compassWait = 50; // 최대 5초(0.1초 * 50) 대기

        // trueHeading과 magneticHeading이 둘 다 0이면 아직 센서가 안 깬 것임
        while (Input.compass.magneticHeading == 0 && compassWait > 0)
        {
            Debug.Log("나침반 센서 깨우는 중...");
            PathFindingUI.instance.ShowText("나침반 센서 깨우는 중...", IndicatorType.aline);
            yield return new WaitForSeconds(0.1f);
            compassWait--;
        }

        // 4. 시간 초과 확인
        if (compassWait <= 0)
        {
            Debug.LogError("[치명적 에러] 나침반 센서가 응답하지 않습니다. 기기 센서 고장이거나 권한 오류입니다.");
            PathFindingUI.instance.ShowText("[치명적 에러] 나침반 센서가 응답하지 않습니다. 기기 센서 고장이거나 권한 오류입니다.", IndicatorType.aline);
            yield break;
        }

        float rotationAngle = 0f;

        if (Input.location.status == LocationServiceStatus.Running)
        {
            // GPS가 켜졌다면 진북(True North) 사용
            rotationAngle = Input.compass.trueHeading;
            Debug.Log("[성공] GPS 기반 진북으로 정렬합니다.");
            PathFindingUI.instance.ShowText("[성공] GPS 기반 진북으로 정렬합니다.", IndicatorType.aline);
        }
        else
        {
            // GPS가 실패했거나 실내라면 자석 센서(Magnetic) 기반 자북 사용
            rotationAngle = Input.compass.magneticHeading;
            Debug.LogWarning("[우회] GPS 실패. 실내용 자석 센서(자북)로 정렬합니다.");
            PathFindingUI.instance.ShowText("진북을 찾지 못해 자북(Magnetic)을 사용합니다.", IndicatorType.aline);
        }

        // 센서 값이 0으로 튀는 버그 방지 (0도일 확률은 극히 희박하므로)
        if (rotationAngle == 0)
        {
            rotationAngle = Input.compass.magneticHeading;
        }

        float currentCameraAngle = Camera.main.transform.localEulerAngles.y;
        // 5. XR Origin 회전 (이때 Y축을 -rotationAngle로 돌리는 것이 맞습니다!)
        xrOrigin.transform.rotation = Quaternion.Euler(0, -rotationAngle + currentCameraAngle, 0);

        isAligned = true;
        Debug.Log($"[방향 정렬 완료] 현재 적용된 각도: {-rotationAngle + currentCameraAngle}");
        PathFindingUI.instance.PrintInputTrueHeading(rotationAngle, -rotationAngle + currentCameraAngle);
        PathFindingUI.instance.ShowText("방향 정렬 완료.", IndicatorType.aline);
        PathFindingUI.instance.ShowAlignErrorIndicator(false);
    }

    private void Update()
    {
        if (Input.compass.enabled)
        {
            PathFindingUI.instance.PrintInputTrueHeading(Input.compass.magneticHeading, -Input.compass.magneticHeading + Camera.main.transform.localEulerAngles.y);
        }
    }
}
