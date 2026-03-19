using UnityEngine;
using System.Collections;

public class ARHeadingAlign : MonoBehaviour
{
    public GameObject xrOrigin; // AR Session Origin (XR Origin)
    private bool isAligned = false;

    void Start()
    {
        PathFindingUI.instance.ShowAlignErrorIndicator(true);
        StartCoroutine(AlignHeadingRoutine());
    }

    IEnumerator AlignHeadingRoutine()
    {
        // 1. 위치 권한 확인 및 위치 서비스 시작
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogError("[에러] 스마트폰의 위치 설정(GPS)이 꺼져있습니다.");
            PathFindingUI.instance.ShowText("[에러] 스마트폰의 위치 설정(GPS)이 꺼져있습니다.", IndicatorType.aline);
            yield break; // 여기서 멈춤
        }

        Input.location.Start();
        Input.compass.enabled = true;

        // 2. 0.2초가 아니라, GPS가 완전히 켜질 때까지 최대 15초간 대기합니다.
        int maxWait = 15;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1); // 1초씩 기다림
            maxWait--;
        }

        // 15초가 지났는데도 실패했다면
        if (maxWait < 1 || Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogError("[에러] GPS 초기화 실패. 실내인지 확인해주세요.");
            PathFindingUI.instance.ShowText("[에러] GPS 초기화 실패. 실내인지 확인해주세요.", IndicatorType.aline);
            yield break;
        }

        // 3. 나침반 값이 안정화될 때까지 프레임 조금 더 대기 (유니티 버그 방지)
        yield return new WaitForSeconds(0.5f);

        // 4. 안전하게 각도 가져오기
        if (Input.compass.enabled)
        {
            // 실내에서는 trueHeading이 안 나올 수 있으므로, 값이 0.1보다 작으면 magneticHeading(자북)을 대체재로 사용
            float rotationAngle = Input.compass.trueHeading;
            if (rotationAngle < 0.1f)
            {
                rotationAngle = Input.compass.magneticHeading;
                Debug.LogWarning("진북을 찾지 못해 자북(Magnetic)을 사용합니다.");
                PathFindingUI.instance.ShowText("진북을 찾지 못해 자북(Magnetic)을 사용합니다.", IndicatorType.aline);
            }

            // 5. XR Origin 회전 (이때 Y축을 -rotationAngle로 돌리는 것이 맞습니다!)
            xrOrigin.transform.rotation = Quaternion.Euler(0, -rotationAngle, 0);

            isAligned = true;
            Debug.Log($"[방향 정렬 완료] 현재 적용된 각도: {rotationAngle}");
            PathFindingUI.instance.ShowText("방향 정렬 완료.", IndicatorType.aline);
            PathFindingUI.instance.ShowAlignErrorIndicator(false);
        }
    }
}
