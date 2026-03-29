using UnityEngine;

public class Vector3Converter
{
    private const float METERS_PER_LAT_DEGREE = 111320f;

    // 현재 카메라의 위치를 기반으로 위도/경도를 반환하는 함수
    public Vector2D GetCurrentCameraGPS(double startLat, double startLon, double cameraX, double cameraZ)
    {
        Debug.Log($"camera position x : {cameraX} camera position y : {cameraZ}");
        // 2. Z축 이동량(m)을 위도(Latitude)로 역변환
        double newLat = startLat + (cameraZ / METERS_PER_LAT_DEGREE);

        // 3. X축 이동량(m)을 경도(Longitude)로 역변환 (위도에 따른 보정치 적용)
        double cosLat = Mathf.Cos((float)startLat * Mathf.Deg2Rad);
        double metersPerLonDegree = METERS_PER_LAT_DEGREE * cosLat;
        double newLon = startLon + (cameraX / metersPerLonDegree);

        Debug.Log($"[역변환 완료] 현재 AR 카메라 위치를 GPS로 변환 -> 위도: {newLat}, 경도: {newLon}");

        // 결과를 담아서 반환 (Vector2는 float형이라 정밀도가 떨어지므로 커스텀 구조체 사용 권장)
        return new Vector2D(newLat, newLon);
    }
}

// 위도 / 경도 구조체
public struct Vector2D
{
    public double latitude;
    public double longitude;

    public Vector2D(double lat, double lon)
    {
        latitude = lat;
        longitude = lon;
    }
}
