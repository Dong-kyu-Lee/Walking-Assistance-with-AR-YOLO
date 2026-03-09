using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static System.Math;

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

public class CoordinateConverter
{
    // 지구의 반지름 (미터)
    private const double EarthRadius = 6371000f;

    // 기준점(Unity World의 0,0,0이 될 실제 GPS 좌표)
    private double originLon;
    private double originLat;

    public CoordinateConverter(double originLon, double originLat)
    {
        this.originLon = originLon;
        this.originLat = originLat;
    }

    // GPS [경도, 위도]를 Unity Vector3 (X, 0, Z)로 변환 (간이 공식)
    public Vector3 ConvertGpsToVector3(double targetLon, double targetLat)
    {
        // 위도/경도를 라디안으로 변환
        double lat1 = originLat * (Math.PI / 180);
        double lat2 = targetLat * (Math.PI / 180);
        double deltaLat = (targetLat - originLat) * (Math.PI / 180);
        double deltaLon = (targetLon - originLon) * (Math.PI / 180);

        // X축 거리 (경도 차이)
        double x = deltaLon * EarthRadius * Math.Cos((lat1 + lat2) / 2.0f);
        // Z축 거리 (위도 차이)
        double z = deltaLat * EarthRadius;

        // Y축(높이)은 고도 데이터가 없다면 일단 0 또는 기준 바닥 높이로 설정
        return new Vector3((float)x, 0f, (float)z);
    }
}


public class TMapRouterDrawing : MonoBehaviour
{
    public List<Vector3> routePathPoints = new List<Vector3>();

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
        }
    }
}
