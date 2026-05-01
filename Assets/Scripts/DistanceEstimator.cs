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

        // 추정 거리(미터). 측정 주기가 아닌 경우 이전 값 유지, 추정 불가 시 -1
        public float distanceMeters;

        // 이번 프레임에 실제로 거리를 측정했는지 여부
        public bool isMeasured;
    }

    // 클래스명 → 실제 평균 너비(미터). Inspector에서 편집할 수 없는 초기값.
    // TODO: 실제 사용 클래스에 맞게 수정 필요
    private static readonly Dictionary<string, float> RealWidthTable = new()
    {
        { "person",     0.50f },
        { "car",        1.80f },
        { "truck",      2.50f },
        { "bus",        2.50f },
        { "bicycle",    0.60f },
        { "motorcycle", 0.80f },
        { "dog",        0.40f },
        { "cat",        0.30f },
        { "chair",      0.50f },
        { "bottle",     0.08f },
    };


    private readonly List<DetectionResult> _results = new();

    /// <summary>
    /// 추론 결과를 받아 거리 정보를 포함한 DetectionResult 목록을 반환한다.
    /// </summary>
    /// <param name="boxes">NMS 통과 박스 목록</param>
    /// <param name="labels">클래스 레이블 배열</param>
    /// <param name="imageWidth">모델 입력 이미지 너비 (픽셀)</param>
    /// <returns>거리 정보가 결합된 탐지 결과 목록</returns>
    public List<DetectionResult> Process(NativeList<BoxData> boxes, string[] labels, int imageWidth)
    {
        _results.Clear();

        // ...

        return _results;
    }

    /// <summary>
    /// 가장 최근에 계산된 탐지+거리 결과를 반환한다.
    /// </summary>
    public IReadOnlyList<DetectionResult> GetLastResults() => _results;
}
