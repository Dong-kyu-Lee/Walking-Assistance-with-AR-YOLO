using UnityEngine;
using System.Collections;
using UnityEngine.Android;

public class GPSManager : MonoBehaviour
{
    public float currentLat; // 현재 위도
    public float currentLon; // 현재 경도
    public bool isGPSStarted = false;

    void Start()
    {
        // 1. 위치 권한 확인 (안드로이드 기준)
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Permission.RequestUserPermission(Permission.FineLocation);
        }

        StartCoroutine(StartLocationService());
    }

    IEnumerator StartLocationService()
    {
        // 2. 위치 서비스 장치가 켜져 있는지 확인
        if (!Input.location.isEnabledByUser)
        {
            Debug.Log("스마트폰 GPS가 꺼져 있습니다.");
            PathFindingUI.instance.ShowGPSErrorIndicator(true);
            yield break;
        }

        // 3. 위치 서비스 시작 (정밀도 5m, 갱신 거리 5m 단위로 설정 가능)
        Input.location.Start(5f, 5f);

        // 4. 초기화될 때까지 대기
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (maxWait < 1 || Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.Log("GPS 초기화 실패");
            PathFindingUI.instance.ShowGPSErrorIndicator(true);
            yield break;
        }

        // 5. 수신 성공! 실시간 갱신 시작
        isGPSStarted = true;
        Debug.Log("GPS 수신 시작");
        PathFindingUI.instance.ShowGPSErrorIndicator(false);
    }

    void Update()
    {
        if (isGPSStarted && Input.location.status == LocationServiceStatus.Running)
        {
            // 실시간으로 변수에 현재 좌표 업데이트
            currentLat = Input.location.lastData.latitude;
            currentLon = Input.location.lastData.longitude;
        }
    }
}
