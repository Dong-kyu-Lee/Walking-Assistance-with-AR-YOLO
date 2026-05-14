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
    [SerializeField] private DistanceEstimator distanceEstimator;
    [SerializeField] private TextMeshProUGUI distanceText;

    [Tooltip("거리 정보 갱신 주기 (초)")]
    [SerializeField] private float updateInterval = 3f;

    private float elapsed;
    private bool isCoolingDown;
    private readonly StringBuilder sb = new();

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
            if (distanceEstimator.GetLastResults().Count == 0) return;
            RefreshText();
            isCoolingDown = true;
            elapsed = 0f;
        }
    }

    private void RefreshText()
    {
        IReadOnlyList<DistanceEstimator.DetectionResult> results = distanceEstimator.GetLastResults();

        if (results.Count == 0)
        {
            distanceText.text = string.Empty;
            return;
        }

        distanceText.text = string.Empty;
        bool first = true;
        foreach (var r in results)
        {
            if (!first) distanceText.text += "\n";
            first = false;

            distanceText.text += $"Label : {r.label}\nDistance : {r.distanceMeters:F1} m\n";
            Debug.Log($"Label : {r.label} , Distance : {r.distanceMeters:F1} m");
        }
        distanceEstimator.ClearResults();
    }
}
