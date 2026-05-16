using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// 지면 평면 가정법으로 YOLO 탐지 결과에 거리 정보를 추가하는 컴포넌트.
/// 바운딩박스 하단 중앙점이 지면에 닿는다고 가정하고,
/// 카메라 높이와 수직 화각으로 거리를 계산한다.
///
/// pitch 계산: Input.acceleration.z 사용.
/// Android에서 가속도계는 화면 회전과 무관하게 portrait 기기 좌표계로 반환된다.
/// LandscapeLeft 모드에서 카메라 하향 pitch θ 만큼 기울면
/// acceleration.z = g·sin(θ) 이므로 pitch = asin(acceleration.z) 가 성립한다.
/// </summary>
public class GroundPlaneDistanceEstimator : MonoBehaviour
{
    [Tooltip("지면으로부터 카메라까지의 높이 (미터)")]
    [SerializeField] private float cameraHeightMeters = 1.7f;

    [Tooltip("가속도계로 pitch를 자동 계산할지 여부. false이면 아래 수동 값 사용.")]
    [SerializeField] private bool useGyro = true;

    [Tooltip("자동 pitch 미사용 시 수동으로 지정하는 pitch 각도 (도, 양수 = 아래 방향)")]
    [SerializeField] private float manualPitchDownDegrees = 10f;

    [Tooltip("Camera.main으로 수직 화각을 얻지 못할 때 사용하는 fallback 값 (도)")]
    [SerializeField] private float fallbackVerticalFovDegrees = 45f;

    [Tooltip("보행 진동 노이즈 제거용 저역통과 필터 계수 (0=고정, 1=필터 없음)")]
    [SerializeField, Range(0.01f, 1f)] private float accelFilterAlpha = 0.1f;

    private const float MinAngleDeg = 0.5f;

    private float _vFovDeg;
    private float _filteredAccelZ;

    public float CurrentPitchDownDegrees { get; private set; }

    private readonly List<DistanceEstimator.DetectionResult> _results = new();

    private void Start()
    {
        Camera cam = Camera.main;
        _vFovDeg = (cam != null) ? cam.fieldOfView : fallbackVerticalFovDegrees;
        _filteredAccelZ = Input.acceleration.z;
    }

    private void Update()
    {
        if (useGyro)
            UpdatePitchFromAccelerometer();
        else
            CurrentPitchDownDegrees = manualPitchDownDegrees;

        Debug.Log("Current Pitch Down Degrees: " + CurrentPitchDownDegrees);
    }

    private void UpdatePitchFromAccelerometer()
    {
        // 보행 진동 노이즈를 지수 이동 평균으로 제거
        _filteredAccelZ = Mathf.Lerp(_filteredAccelZ, Input.acceleration.z, accelFilterAlpha);

        // LandscapeLeft 모드에서 카메라 하향 pitch θ 만큼 기울면
        // acceleration.z = g·sin(θ) 이므로 pitch = asin(acceleration.z)
        CurrentPitchDownDegrees = -Mathf.Asin(Mathf.Clamp(_filteredAccelZ, -1f, 1f)) * Mathf.Rad2Deg;
    }

    /// <summary>하위 호환성을 위해 남겨둔 메서드. 가속도계 방식에서는 사용하지 않는다.</summary>
    public void Calibrate() { }

    /// <summary>
    /// 추론 결과를 받아 거리 정보를 포함한 DetectionResult 목록을 반환한다.
    /// </summary>
    /// <param name="boxes">NMS 통과 박스 목록 (cx, cy, w, h 는 모델 입력 픽셀 단위)</param>
    /// <param name="labels">클래스 레이블 배열</param>
    /// <param name="imageWidth">모델 입력 이미지 너비/높이 (픽셀, 정사각형 입력 가정)</param>
    public List<DistanceEstimator.DetectionResult> Process(
        NativeList<BoxData> boxes, string[] labels, int imageWidth)
    {
        _results.Clear();

        float imageCenterY = imageWidth * 0.5f;
        float pitchDeg = CurrentPitchDownDegrees;

        for (int i = 0; i < boxes.Length; i++)
        {
            BoxData box = boxes[i];
            string label = (box.classID >= 0 && box.classID < labels.Length)
                ? labels[box.classID]
                : "Unknown";

            // 바운딩박스 하단 중앙 픽셀 (객체가 지면과 접촉하는 지점)
            float bboxBottomY = box.cy + box.h * 0.5f;

            // 이미지 중앙에서 해당 픽셀까지의 수직 각도 (양수 = 아래쪽)
            float angleFromCenterDeg = _vFovDeg * (bboxBottomY - imageCenterY) / imageWidth;

            // 수평선 기준 아래쪽 총 각도 = 카메라 pitch + 픽셀 오프셋 각도
            float totalAngleDeg = pitchDeg + angleFromCenterDeg;

            float distance = -1f;
            bool measured = false;

            if (totalAngleDeg > MinAngleDeg)
            {
                distance = cameraHeightMeters / Mathf.Tan(totalAngleDeg * Mathf.Deg2Rad);
                measured = true;
            }

            _results.Add(new DistanceEstimator.DetectionResult
            {
                box            = box,
                label          = label,
                distanceMeters = distance,
                isMeasured     = measured,
            });
        }

        return _results;
    }

    public IReadOnlyList<DistanceEstimator.DetectionResult> GetLastResults() => _results;

    public void ClearResults() => _results.Clear();
}
