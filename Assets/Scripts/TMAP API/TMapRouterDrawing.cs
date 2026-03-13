using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

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

    public List<Vector3> routePathPoints = new List<Vector3>();

    private void Start()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = 0;
        _lineRenderer.startWidth = 0.1f;
        _lineRenderer.endWidth = 0.1f;
    }

    public void ParseRouteData(string jsonResponse, double startLon, double startLat)
    {
        // 1. JSON 텍스트를 C# 객체로 역직렬화
        RouteResponse response = JsonConvert.DeserializeObject<RouteResponse>(jsonResponse);

        // 2. 좌표 변환기 초기화 (출발지를 기준점 0,0,0으로 설정)
        CoordinateConverter converter = new CoordinateConverter(startLon, startLat);

        routePathPoints.Clear();

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
                    routePathPoints.Add(worldPos);
                }
            }
        }

        Debug.Log($"총 {routePathPoints.Count}개의 Vector3 경로 포인트가 추출되었습니다.");
        foreach(Vector3 point in routePathPoints.Take<Vector3>(10))
        {
            Debug.Log(point);
            _lineRenderer.positionCount++;
            _lineRenderer.SetPosition(_lineRenderer.positionCount - 1, point);
        }
    }
}
