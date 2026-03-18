using UnityEngine;
using System.Collections;

public class ARHeadingAlign : MonoBehaviour
{
    public GameObject arSessionOrigin; // AR Session Origin 오브젝트 연결
    private bool isAligned = false;

    private WaitForSeconds _waitTime = new WaitForSeconds(0.2f);

    void Start()
    {
        // 나침반 활성화
        Input.compass.enabled = true;
        Input.location.Start(); // 나침반은 위치 서비스가 켜져야 더 정확합니다.

        PathFindingUI.instance.ShowAlignErrorIndicator(true);
        // 정렬 시작 (잠시 대기 후 실행하는 것이 안정적입니다)
        StartCoroutine(AlignHeadingRoutine());
    }

    IEnumerator AlignHeadingRoutine()
    {
        // 나침반 값이 안정화될 때까지 잠시 대기
        yield return _waitTime;

        if (Input.compass.enabled)
        {
            // 1. 실제 북쪽과 스마트폰이 가리키는 방향의 차이(각도)를 가져옴
            float rotationAngle = Input.compass.trueHeading;

            // 2. AR Session Origin을 Y축 기준으로 회전시켜 북쪽을 맞춤
            // 주의: 유니티는 왼손 좌표계를 쓰므로 각도 계산에 유의해야 합니다.
            arSessionOrigin.transform.rotation = Quaternion.Euler(0, -rotationAngle, 0);

            isAligned = true;
            Debug.Log($"[방향 정렬 완료] 현재 북쪽 각도: {rotationAngle}");
            PathFindingUI.instance.ShowAlignErrorIndicator(false);
        }
    }
}
