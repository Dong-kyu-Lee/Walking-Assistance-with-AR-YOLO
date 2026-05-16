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
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private AndroidTTS androidTTS;

    [Tooltip("거리 정보 갱신 주기 (초)")]
    [SerializeField] private float updateInterval = 3f;

    [Tooltip("YOLO 모델 입력 이미지 너비 (픽셀) — 방향 삼등분에 사용")]
    [SerializeField] private int modelImageWidth = 640;

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

    // cx: 모델 이미지 좌표(0~modelImageWidth). 화면을 삼등분해 방향 반환
    private string HorizontalDirection(float cx)
    {
        float ratio = cx / modelImageWidth;
        if (ratio < 1f / 3f) return "왼쪽";
        if (ratio < 2f / 3f) return "앞쪽";
        return "오른쪽";
    }

    // 마지막 글자의 종성 유무로 '이'/'가' 결정
    private static string SubjectParticle(string word)
    {
        if (string.IsNullOrEmpty(word)) return "이";
        char last = word[^1];
        if (last >= 0xAC00 && last <= 0xD7A3)
            return (last - 0xAC00) % 28 == 0 ? "가" : "이";
        return "이";
    }

    private string tmpName = "person";
    private float tmpDistance = 1.23f;
    private void Start()
    {
        distanceText.text = $"{tmpName}<color=green>이 </color>{tmpDistance:F1}<color=green>m 앞에 있습니다.</color>";
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

        if (results.Count == 0)
        {
            distanceText.text = string.Empty;
            return;
        }

        distanceText.text = string.Empty;
        bool first = true;
        GroundPlaneDistanceEstimator.DetectionResult nearest = default;
        float nearestDist = float.MaxValue;

        foreach (var r in results)
        {
            if (!first) distanceText.text += "\n";
            first = false;

            distanceText.text += $"Label : {r.label}\nDistance : {r.distanceMeters:F1} m\n";
            Debug.Log($"Label : {r.label} , Distance : {r.distanceMeters:F1} m");

            if (r.isMeasured && r.distanceMeters > 0f && r.distanceMeters < nearestDist)
            {
                nearestDist = r.distanceMeters;
                nearest = r;
            }
        }

        if (nearestDist < float.MaxValue && androidTTS != null)
        {
            string korean = LabelKorean.TryGetValue(nearest.label, out string k) ? k : nearest.label;
            string particle = SubjectParticle(korean);
            string direction = HorizontalDirection(nearest.box.cx);
            androidTTS.Speak($"{korean}{particle} {nearestDist:F1}미터 {direction}에 있습니다.");
        }

        groundPlaneDistanceEstimator.ClearResults();
    }
}
