using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UILineRenderer 인스턴스를 Pool 방식으로 관리하며 객체별 폴리곤 윤곽선을 Canvas 위에 렌더링한다.
/// 추론·마스크 계산은 RunYOLO가, 렌더링만 이 클래스가 담당한다.
/// </summary>
public class PolygonOverlayRenderer : MonoBehaviour
{
    [Tooltip("폴리곤을 그릴 기준 RawImage (displayImage와 동일한 오브젝트)")]
    [SerializeField] private RawImage displayImage;

    [Tooltip("폴리곤 선 굵기 (UILineRenderer scale=true 기준 픽셀 단위)")]
    [SerializeField, Min(0.001f)] private float lineThickness = 3f;

    private readonly List<UILineRenderer> pool = new List<UILineRenderer>();

    /// <summary>이전 프레임의 모든 폴리곤을 비활성화.</summary>
    public void ClearAll()
    {
        foreach (var lr in pool)
            lr.gameObject.SetActive(false);
    }

    /// <summary>
    /// id번째 폴리곤을 표시한다. pool에 여유가 있으면 재사용, 없으면 새로 생성.
    /// </summary>
    /// <param name="id">박스 인덱스 (0-based)</param>
    /// <param name="points">
    /// UILineRenderer scale=true 기준 정규화 좌표 [0,1].
    /// 마지막 점 = 첫 점이어야 폐곡선으로 표시됨.
    /// </param>
    /// <param name="color">선 색상</param>
    public void ShowPolygon(int id, Vector2[] points, Color color)
    {
        if (points == null || points.Length < 2) return;

        var lr = GetOrCreate(id);
        lr.gameObject.SetActive(true);
        lr.color     = color;
        lr.thickness = lineThickness;
        lr.SetPositions(points);
    }

    /// <summary>pool에서 id번째 UILineRenderer를 반환하거나 새로 생성해 pool에 추가.</summary>
    private UILineRenderer GetOrCreate(int id)
    {
        if (id < pool.Count)
            return pool[id];

        // UILineRenderer 추가 시 [RequireComponent(RectTransform)]에 의해 RectTransform도 자동 추가됨
        var go = new GameObject($"PolygonLine_{id}");
        go.transform.SetParent(displayImage.transform, false);

        var lr = go.AddComponent<UILineRenderer>();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // scale=true: 정규화 좌표 [0,1]을 RectTransform 크기로 자동 스케일링
        lr.scale         = true;
        lr.endCaps       = false; // 폐곡선이므로 endCap 불필요
        lr.raycastTarget = false;
        lr.thickness     = lineThickness;

        pool.Add(lr);
        return lr;
    }
}
