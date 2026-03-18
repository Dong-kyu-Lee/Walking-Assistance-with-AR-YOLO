using System;
using UnityEngine;

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

