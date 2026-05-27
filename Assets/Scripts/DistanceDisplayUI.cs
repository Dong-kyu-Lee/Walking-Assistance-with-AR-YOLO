using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DistanceEstimator의 결과를 주기적으로 읽어 UI 텍스트에 표시하는 컴포넌트.
/// 거리 계산 로직은 DistanceEstimator가, 표시 주기와 포맷은 이 클래스가 담당한다.
/// </summary>
public class DistanceDisplayUI : MonoBehaviour
{
    [SerializeField] private GroundPlaneDistanceEstimator groundPlaneDistanceEstimator;
    // [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private AndroidTTS androidTTS;

    [Tooltip("거리 정보 갱신 주기 (초)")]
    [SerializeField] private float updateInterval = 3f;

    [Tooltip("YOLO 모델 입력 이미지 너비 (픽셀) — 방향 삼등분에 사용")]
    [SerializeField] private int modelImageWidth = 640;

    [Tooltip("음성 안내를 시작할 거리 임계값 (미터)")]
    [SerializeField] private float announcementDistanceThreshold = 6.0f;

    private float elapsed;
    private bool isCoolingDown;

    private static readonly Dictionary<string, string> LabelKorean = new()
    {
        { "sidewalk_normal",  "일반 인도" },
        { "sidewalk_damaged", "파손된 인도" },
        { "roadway",          "차도" },
        { "bike_lane",        "자전거 도로" },
        { "alley",            "골목길" },
        { "crosswalk",        "횡단보도" },
        { "speed_bump",       "과속방지턱" },
        { "stairs",           "계단" },
        { "manhole",          "맨홀" },
        { "tree_zone",        "나무" },
        { "grating",          "격자 덮개" },
        { "repair_zone",      "공사 구역" },
        { "braille_blocks",   "점자블록" },
        { "curb",             "연석" },
        { "ramp",             "경사로" },
        { "person",           "사람" },
        { "car",              "자동차" },
        { "bus",              "버스" },
        { "truck",            "트럭" },
        { "motorcycle",       "오토바이" },
        { "bicycle",          "자전거" },
        { "wheelchair",       "휠체어" },
        { "stroller",         "유모차" },
        { "bollard",          "볼라드" },
        { "pole",             "전신주" },
        { "traffic_light",    "신호등" },
        { "traffic_sign",     "교통 표지판" },
        { "movable_signage",  "입간판" },
        { "kiosk",            "키오스크" },
        { "fire_hydrant",     "소화전" },
        { "bench",            "벤치" },
        { "chair",            "의자" },
        { "table",            "테이블" },
        { "trash_can",        "쓰레기통" },
        { "dog",              "강아지" },
        { "carrier",          "캐리어" },
    };

    private static readonly HashSet<string> DangerousVehicleLabels = new()
    {
        "car", "motorcycle", "bus", "truck", "bicycle"
    };

    // cx: 모델 이미지 좌표(0~modelImageWidth). 화면을 삼등분해 방향 반환
    private string HorizontalDirection(float cx)
    {
        float ratio = cx / modelImageWidth;
        if (ratio < 1f / 3f) return "전방 좌측";
        if (ratio < 2f / 3f) return "전방 중앙";
        return "전방 우측";
    }

    private void Update()
    {
        if (isCoolingDown)
        {
            elapsed += Time.deltaTime;
            if (elapsed < updateInterval) return;

            // 쿨다운 만료: Idle로 전환
            elapsed = 0f;
            isCoolingDown = false;
        }
        else
        {
            // Idle: 탐지 발생 시 즉시 출력 후 쿨다운 진입
            if (groundPlaneDistanceEstimator.GetLastResults().Count == 0) return;
            RefreshText();
            isCoolingDown = true;
            elapsed = 0f;
        }
    }

    private void RefreshText()
    {
        IReadOnlyList<GroundPlaneDistanceEstimator.DetectionResult> results = groundPlaneDistanceEstimator.GetLastResults();

        if (results.Count == 0) return;

        GroundPlaneDistanceEstimator.DetectionResult nearest = default;
        float nearestDist = float.MaxValue;
        GroundPlaneDistanceEstimator.DetectionResult nearestVehicle = default;
        float nearestVehicleDist = float.MaxValue;
    
        foreach (var r in results)
        {
            Debug.Log($"Label : {r.label} , Distance : {r.distanceMeters:F1} m");

            if (!r.isMeasured || r.distanceMeters <= 0f) continue;

            if (r.distanceMeters < nearestDist)
            {
                nearestDist = r.distanceMeters;
                nearest = r;
            }

            if (DangerousVehicleLabels.Contains(r.label) && r.distanceMeters < nearestVehicleDist)
            {
                nearestVehicleDist = r.distanceMeters;
                nearestVehicle = r;
            }
        }

        // 위험 차량이 임계 거리 내에 있으면 우선 안내, 없으면 가장 가까운 객체 안내
        bool vehicleInRange = nearestVehicleDist < announcementDistanceThreshold;
        bool anyInRange = nearestDist < announcementDistanceThreshold;

        if (androidTTS != null && (vehicleInRange || anyInRange))
        {
            var target = vehicleInRange ? nearestVehicle : nearest;
            float distance = vehicleInRange ? nearestVehicleDist : nearestDist;
            string korean = LabelKorean.TryGetValue(target.label, out string k) ? k : target.label;
            string direction = HorizontalDirection(target.box.cx);
            androidTTS.Speak($"{direction} {Mathf.Round(distance * 10f) / 10f}미터 {korean}");
        }

        groundPlaneDistanceEstimator.ClearResults();
    }
}
