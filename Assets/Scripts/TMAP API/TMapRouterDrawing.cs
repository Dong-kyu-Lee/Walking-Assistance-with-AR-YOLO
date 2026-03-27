using System.Collections;
using System.Collections.Generic;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using System;

public class RouteResponse
{
    public List<Feature> features;
}

public class Feature
{
    public Geometry geometry;
}

public class Geometry
{
    public string type; // "Point" 또는 "LineString"

    // LineString 좌표계 파싱을 위해 이중 리스트 사용 [경도, 위도]
    public JArray coordinates;
}

public class TMapRouterDrawing : MonoBehaviour
{
    private LineRenderer _lineRenderer;
    [SerializeField] private AREarthManager _earthManager;
    [SerializeField] private ARAnchorManager _arAnchorManager;



    public List<Vector3> RoutePathPoints = new List<Vector3>();
    public List<ARGeospatialAnchor> PathAnchors = new List<ARGeospatialAnchor>();

    public List<Vector3> densePath = new List<Vector3>();
    private Vector3 _cameraPos;

    private WaitForSeconds _waitTime = new WaitForSeconds(0.1f);

    private void Start()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = 0;
        _lineRenderer.startWidth = 1f;
        _lineRenderer.endWidth = 1f;

        _cameraPos = Camera.main.transform.position;
    }

    private void Update()
    {
        if (_earthManager.EarthState != EarthState.Enabled ||_earthManager.EarthTrackingState != TrackingState.Tracking)
        {
            return;
        }

        if (PathAnchors.Count > 0)
        {
            UpdateAnchors();
        }

    }

    private void UpdateAnchors()
    {
        for (int i = 0; i < PathAnchors.Count; i++)
        {
            _lineRenderer.SetPosition(i, PathAnchors[i].transform.position);

            // 2. [디버그용] 해당 위치에 3m짜리 거대한 기둥 세우기
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.transform.position = PathAnchors[i].transform.position;
            pillar.transform.localScale = new Vector3(1f, 3.0f, 1f); // 얇고 길게 (높이 6m)
            pillar.GetComponent<Renderer>().material.color = new UnityEngine.Color(1, 0, 0, 0.5f);
        }
    }

    public IEnumerator CreateAnchors(string jsonResponse, double startLon, double startLat)
    {
        PathFindingUI.instance.ShowText("구글 VPS 추적 중... 카메라로 주변 건물을 비춰주세요...", IndicatorType.lineRenderer);

        float count = 0;

        while (( _earthManager.EarthState != EarthState.Enabled ||_earthManager.EarthTrackingState != TrackingState.Tracking) 
            && count >= 20)
        {
            // 한 프레임 쉬고 다시 검사 (유니티 메인 스레드 멈춤 방지)
            count++;
            yield return null;
        }

        if (count>= 20)
        {
            PathFindingUI.instance.ShowText("구글 VPS 트래킹에 실패했습니다... 일반 GPS 모드로 전환합니다.", IndicatorType.lineRenderer);

            ParseRouteData(jsonResponse, startLon, startLat);

            yield break;
        }


        PathFindingUI.instance.ShowText("구글 VPS 트래킹 성공", IndicatorType.lineRenderer);
        PathFindingUI.instance.ShowLineRenderedErrorIndicator(true);

        RouteResponse response = JsonConvert.DeserializeObject<RouteResponse>(jsonResponse);

        PathAnchors.Clear();

        GeospatialPose pose = _earthManager.CameraGeospatialPose;
        double lineAltitude = pose.Altitude - 1f;


        // 3. features 리스트를 돌면서 LineString만 추출
        foreach (var feature in response.features)
        {
            if (feature.geometry.type == "LineString")
            {
                // LineString의 각 [경도, 위도] 좌표를 Vector3로 변환하여 리스트에 추가
                foreach (JToken coord in feature.geometry.coordinates)
                {
                    double lon = (double)coord[0];
                    double lat = (double)coord[1];

                    ARGeospatialAnchor anchor = _arAnchorManager.AddAnchor(lat, lon, lineAltitude, Quaternion.identity);
                    if (anchor != null)
                    {
                        PathAnchors.Add(anchor);
                    }
                }
            }
        }

        _lineRenderer.positionCount = PathAnchors.Count;
    }


    public void ParseRouteData(string jsonResponse, double startLon, double startLat)
    {
        PathFindingUI.instance.ShowLineRenderedErrorIndicator(true);
        // 1. JSON 텍스트를 C# 객체로 역직렬화
        RouteResponse response = JsonConvert.DeserializeObject<RouteResponse>(jsonResponse);

        // 2. 좌표 변환기 초기화 (출발지를 기준점 0,0,0으로 설정)
        CoordinateConverter converter = new CoordinateConverter(startLon, startLat);

        RoutePathPoints.Clear();

        // 3. features 리스트를 돌면서 LineString만 추출
        foreach (var feature in response.features)
        {
            if (feature.geometry.type == "LineString")
            {
                // LineString의 각 [경도, 위도] 좌표를 Vector3로 변환하여 리스트에 추가
                foreach (JToken coord in feature.geometry.coordinates)
                {
                    double lon = (double)coord[0];
                    double lat = (double)coord[1];

                    Vector3 worldPos = converter.ConvertGpsToVector3(lon, lat);
                    RoutePathPoints.Add(worldPos);

                    Vector3 displayPoint = new Vector3(worldPos.x, worldPos.y + 1.5f, worldPos.z);

                    // 2. [디버그용] 해당 위치에 3m짜리 거대한 기둥 세우기
                    GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pillar.transform.position = displayPoint;
                    pillar.transform.localScale = new Vector3(0.5f, 3.0f, 0.5f); // 얇고 길게 (높이 6m)
                    pillar.GetComponent<Renderer>().material.color = new UnityEngine.Color(1, 0, 0, 0.5f);

                }
            }
        }

        Debug.Log($"총 {RoutePathPoints.Count}개의 Vector3 경로 포인트가 추출되었습니다.");
        PathFindingUI.instance.ShowText("경로 추출 성공", IndicatorType.lineRenderer);
        /*foreach(Vector3 point in routePathPoints.Take<Vector3>(10))
        {
            Debug.Log(point);
            _lineRenderer.positionCount++;
            _lineRenderer.SetPosition(_lineRenderer.positionCount - 1, point);
        }*/

        /*foreach (Vector3 point in routePathPoints)
        {
            _lineRenderer.positionCount++;
            _lineRenderer.SetPosition(_lineRenderer.positionCount - 1, point);
        }*/


        densePath.Clear();
        densePath = InterpolatePath(RoutePathPoints);
        _lineRenderer.positionCount = densePath.Count;
        _lineRenderer.SetPositions(densePath.ToArray());
        PathFindingUI.instance.ShowText("경로 출력 성공", IndicatorType.lineRenderer);

        StartCoroutine(SnapToPlaneRoutine());

        PathFindingUI.instance.ShowLineRenderedErrorIndicator(false);
    }

    IEnumerator SnapToPlaneRoutine()
    {
        int startIndex = 0;
        while (true)
        {
            //Vector3 cameraPos = Camera.main.transform.position;

            // 쪼개진 전체 경로(densePath)를 돌면서 확인하되,
            for (int i = startIndex; i < densePath.Count; i++)
            {
                Vector3 point = densePath[i];

                // 1. 카메라와 점 사이의 거리가 5m 이내일 때만 연산 수행!
                if (Vector3.Distance(_cameraPos, point) < 5.0f)
                {
                    // 2. 해당 점 위치에서 아래(Vector3.down)로 레이캐스트 발사
                    Ray ray = new Ray(new Vector3(point.x, _cameraPos.y, point.z), Vector3.down);

                    point.y = _cameraPos.y - 1.0f;

                    /*if (Physics.Raycast(ray, out RaycastHit hit, 10.0f))
                    {
                        // 3. 평면에 닿았다면 해당 점의 Y값을 평면 높이로 갱신
                        point.y = hit.point.y + 0.5f; // 바닥에 파묻히지 않게 살짝 띄움
                    }
                    else
                    {
                        point.y = _cameraPos.y - 1.0f;
                    }*/
                    densePath[i] = point;
                    _lineRenderer.SetPosition(i, point);
                    startIndex = i + 1;

                }
            }

            yield return _waitTime; // 0.1초마다 반복
        }
    }

    // 점과 점 사이를 촘촘하게 쪼개주는 함수
    public List<Vector3> InterpolatePath(List<Vector3> originalPath, float maxDistance = 1.0f)
    {
        List<Vector3> densePath = new List<Vector3>();

        // 현재 좌표를 시작 지점으로 설정
        densePath.Add(Vector3.zero);

        // 첫 번째 점은 그대로 추가
        densePath.Add(originalPath[0]);

        for (int i = 1; i < originalPath.Count - 1; i++)
        {
            Vector3 p1 = originalPath[i];
            Vector3 p2 = originalPath[i + 1];

            // 두 점 사이의 실제 거리 측정
            float distance = Vector3.Distance(p1, p2);

            // 거리가 설정한 기준(예: 1m)보다 멀다면? 쪼갭니다!
            if (distance > maxDistance)
            {
                // 몇 조각으로 나눌지 계산 (예: 5.5m면 6조각)
                int segments = Mathf.CeilToInt(distance / maxDistance);

                for (int j = 1; j <= segments; j++)
                {
                    // Lerp를 이용해 p1과 p2 사이의 비율(t)에 해당하는 중간 좌표를 구함
                    float t = (float)j / segments;
                    Vector3 midPoint = Vector3.Lerp(p1, p2, t);
                    densePath.Add(midPoint);
                }
            }
            else
            {
                // 거리가 짧으면 그냥 다음 점 추가
                densePath.Add(p2);
            }
        }

        // 촘촘해진 새로운 경로 리스트 반환
        return densePath;
    }
}
