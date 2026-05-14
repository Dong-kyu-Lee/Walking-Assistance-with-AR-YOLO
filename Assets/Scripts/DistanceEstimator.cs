using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// YOLO 탐지 결과에 카메라-객체 간 거리 정보를 추가하는 컴포넌트.
/// </summary>
public class DistanceEstimator : MonoBehaviour
{
    // 탐지 결과에 거리 정보를 결합한 구조체
    public struct DetectionResult
    {
        public BoxData box;
        public string label;

        // 추정 거리(미터). 추정 불가 시 -1
        public float distanceMeters;

        // 이번 프레임에 실제로 거리를 측정했는지 여부
        public bool isMeasured;
    }

    // 너비 기준 클래스 → 실제 너비(m)
    // 자세/방향에 따른 높이 변화가 크거나 소형 물체에 사용
    // TODO: 실제 사용 클래스에 맞게 수정 필요
    private static readonly Dictionary<string, float> RealWidthTable = new()
    {
        { "person",     0.50f },
        { "dog",        0.40f },
        { "cat",        0.30f },
        { "chair",      0.50f },
        { "bottle",     0.08f },
    };

    // 높이 기준 클래스 → 실제 높이(m)
    // 정면/측면 방향에 따른 너비 편차가 큰 차량류에 사용
    // TODO: 실제 사용 클래스에 맞게 수정 필요
    private static readonly Dictionary<string, float> RealHeightTable = new()
    {
        { "car",        1.50f },
        { "truck",      3.00f },
        { "bus",        3.00f },
        { "bicycle",    0.60f },
        { "motorcycle", 0.80f },
        { "bench",      0.80f },
    };

    // Camera.main으로 화각을 얻지 못할 때 사용하는 fallback 값 (도 단위)
    [SerializeField] private float fallbackHorizontalFovDegrees = 60f;
    [SerializeField] private float fallbackVerticalFovDegrees   = 45f;

    // 실제 초점거리(픽셀) = ratio * imageSize
    private float _hFocalLengthRatio; // 수평 화각 기반 — 너비 기준 거리에 사용
    private float _vFocalLengthRatio; // 수직 화각 기반 — 높이 기준 거리에 사용

    private readonly List<DetectionResult> _results = new();

    private void Start()
    {
        CacheFocalLengthRatios();
    }

    /// <summary>
    /// 카메라 화각으로부터 수평/수직 픽셀 초점거리 비율을 계산해 캐싱한다.
    /// </summary>
    private void CacheFocalLengthRatios()
    {
        float hFovDeg = fallbackHorizontalFovDegrees;
        float vFovDeg = fallbackVerticalFovDegrees;

        Camera cam = Camera.main;
        if (cam != null)
        {
            // Unity fieldOfView는 수직 화각(도)
            vFovDeg = cam.fieldOfView;

            // 수직 화각 → 수평 화각 변환
            float vFovRad = vFovDeg * Mathf.Deg2Rad;
            float hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovRad * 0.5f) * cam.aspect);
            hFovDeg = hFovRad * Mathf.Rad2Deg;
        }

        _hFocalLengthRatio = 0.5f / Mathf.Tan(hFovDeg * 0.5f * Mathf.Deg2Rad);
        _vFocalLengthRatio = 0.5f / Mathf.Tan(vFovDeg * 0.5f * Mathf.Deg2Rad);
    }

    /// <summary>
    /// 추론 결과를 받아 거리 정보를 포함한 DetectionResult 목록을 반환한다.
    /// </summary>
    /// <param name="boxes">NMS 통과 박스 목록 (cx, cy, w, h 는 모델 입력 픽셀 단위)</param>
    /// <param name="labels">클래스 레이블 배열</param>
    /// <param name="imageWidth">모델 입력 이미지 너비/높이 (픽셀, 정사각형 입력 가정)</param>
    /// <returns>거리 정보가 결합된 탐지 결과 목록</returns>
    public List<DetectionResult> Process(NativeList<BoxData> boxes, string[] labels, int imageWidth)
    {
        _results.Clear();

        // 모델 입력이 정사각형(640×640)이므로 imageWidth를 수직/수평 모두에 사용
        float hFocal = _hFocalLengthRatio * imageWidth;
        float vFocal = _vFocalLengthRatio * imageWidth;

        for (int i = 0; i < boxes.Length; i++)
        {
            BoxData box = boxes[i];
            string label = (box.classID >= 0 && box.classID < labels.Length)
                ? labels[box.classID]
                : "Unknown";

            Debug.Log("box label : " + label + " box w : " + box.w + " box h : " + box.h);

            float distance = -1f;
            bool measured = false;

            if (RealHeightTable.TryGetValue(label, out float realHeight) && box.h > 0f)
            {
                // 차량류: 높이 기준  D = H_real * f_v / H_pixel
                distance = realHeight * vFocal / box.h;
                measured = true;
            }
            else if (RealWidthTable.TryGetValue(label, out float realWidth) && box.w > 0f)
            {
                // 그 외: 너비 기준  D = W_real * f_h / W_pixel
                distance = realWidth * hFocal / box.w;
                measured = true;
            }

            _results.Add(new DetectionResult
            {
                box            = box,
                label          = label,
                distanceMeters = distance,
                isMeasured     = measured,
            });
        }

        return _results;
    }

    /// <summary>
    /// 가장 최근에 계산된 탐지+거리 결과를 반환한다.
    /// </summary>
    public IReadOnlyList<DetectionResult> GetLastResults() => _results;

    /// <summary>
    /// 결과 리스트를 비워 이미 출력된 결과가 재출력되지 않도록 한다.
    /// </summary>
    public void ClearResults() => _results.Clear();
}
