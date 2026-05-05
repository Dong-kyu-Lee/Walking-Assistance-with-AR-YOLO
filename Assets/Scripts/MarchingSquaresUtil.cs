using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// Marching Squares 알고리즘으로 binary mask에서 윤곽선 폴리곤 포인트를 추출하는 유틸리티.
/// MonoBehaviour가 아닌 순수 static 클래스로, 렌더링 책임을 갖지 않는다.
/// </summary>
public static class MarchingSquaresUtil
{
    // 코너 비트 인코딩: TL=8, TR=4, BR=2, BL=1
    // 엣지 인덱스:  0=top, 1=right, 2=bottom, 3=left
    // 각 행: { seg0_edgeA, seg0_edgeB, seg1_edgeA, seg1_edgeB } — -1 은 세그먼트 없음
    private static readonly int[,] segmentTable = new int[16, 4]
    {
        {-1,-1,-1,-1}, //  0: 0000 — 전부 배경
        { 3, 2,-1,-1}, //  1: 0001 — BL만       → left-bottom
        { 2, 1,-1,-1}, //  2: 0010 — BR만        → bottom-right
        { 3, 1,-1,-1}, //  3: 0011 — BL+BR       → left-right
        { 0, 1,-1,-1}, //  4: 0100 — TR만        → top-right
        { 0, 3, 1, 2}, //  5: 0101 — TR+BL 안장  → top-left / right-bottom
        { 0, 2,-1,-1}, //  6: 0110 — TR+BR       → top-bottom
        { 0, 3,-1,-1}, //  7: 0111 — TR+BR+BL    → top-left
        { 0, 3,-1,-1}, //  8: 1000 — TL만        → top-left
        { 0, 2,-1,-1}, //  9: 1001 — TL+BL       → top-bottom
        { 0, 1, 3, 2}, // 10: 1010 — TL+BR 안장  → top-right / left-bottom
        { 0, 1,-1,-1}, // 11: 1011 — TL+BR+BL    → top-right
        { 3, 1,-1,-1}, // 12: 1100 — TL+TR       → left-right
        { 1, 2,-1,-1}, // 13: 1101 — TL+TR+BL    → right-bottom
        { 3, 2,-1,-1}, // 14: 1110 — TL+TR+BR    → left-bottom
        {-1,-1,-1,-1}, // 15: 1111 — 전부 전경
    };

    /// <summary>
    /// 엣지 중점을 정수 키로 인코딩 (float 비교 오차 방지를 위해 좌표 × 2).
    /// </summary>
    private static Vector2Int EdgeKey(int col, int row, int edge)
    {
        switch (edge)
        {
            case 0: return new Vector2Int(col * 2 + 1, row * 2);          // top
            case 1: return new Vector2Int((col + 1) * 2, row * 2 + 1);   // right
            case 2: return new Vector2Int(col * 2 + 1, (row + 1) * 2);   // bottom
            case 3: return new Vector2Int(col * 2,       row * 2 + 1);   // left
            default: return Vector2Int.zero;
        }
    }

    /// <summary>엣지 중점의 실제 mask 좌표계 위치.</summary>
    private static Vector2 EdgePoint(int col, int row, int edge)
    {
        switch (edge)
        {
            case 0: return new Vector2(col + 0.5f, row);
            case 1: return new Vector2(col + 1f,   row + 0.5f);
            case 2: return new Vector2(col + 0.5f, row + 1f);
            case 3: return new Vector2(col,         row + 0.5f);
            default: return Vector2.zero;
        }
    }

    /// <summary>
    /// per-object mask 슬라이스에서 윤곽선 폴리곤 포인트를 추출한다.
    /// 반환값은 mask 좌표계 [0, maskRes] 기준이며, 마지막 점 = 첫 점 (폐곡선).
    /// </summary>
    /// <param name="masks">perObjectMasks 배열 (boxes[b]: offset = b * maskRes * maskRes)</param>
    /// <param name="maskRes">마스크 해상도 (예: 160)</param>
    /// <param name="boxIndex">추출할 객체 인덱스</param>
    public static List<Vector2> GetContour(NativeArray<byte> masks, int maskRes, int boxIndex)
    {
        int offset = boxIndex * maskRes * maskRes;

        var keyToPoint = new Dictionary<Vector2Int, Vector2>();
        var adjacency  = new Dictionary<Vector2Int, List<Vector2Int>>();

        for (int row = 0; row < maskRes - 1; row++)
        {
            for (int col = 0; col < maskRes - 1; col++)
            {
                bool tl = masks[offset + row       * maskRes + col]     == 1;
                bool tr = masks[offset + row       * maskRes + col + 1] == 1;
                bool br = masks[offset + (row + 1) * maskRes + col + 1] == 1;
                bool bl = masks[offset + (row + 1) * maskRes + col]     == 1;

                int c = (tl ? 8 : 0) | (tr ? 4 : 0) | (br ? 2 : 0) | (bl ? 1 : 0);

                AddEdge(col, row, segmentTable[c, 0], segmentTable[c, 1], keyToPoint, adjacency);
                AddEdge(col, row, segmentTable[c, 2], segmentTable[c, 3], keyToPoint, adjacency);
            }
        }

        if (keyToPoint.Count == 0)
            return new List<Vector2>();

        return TracePolygon(keyToPoint, adjacency);
    }

    private static void AddEdge(int col, int row, int e0, int e1,
        Dictionary<Vector2Int, Vector2>        keyToPoint,
        Dictionary<Vector2Int, List<Vector2Int>> adjacency)
    {
        if (e0 < 0 || e1 < 0) return;

        var kA = EdgeKey(col, row, e0);
        var kB = EdgeKey(col, row, e1);

        keyToPoint[kA] = EdgePoint(col, row, e0);
        keyToPoint[kB] = EdgePoint(col, row, e1);

        if (!adjacency.ContainsKey(kA)) adjacency[kA] = new List<Vector2Int>();
        if (!adjacency.ContainsKey(kB)) adjacency[kB] = new List<Vector2Int>();

        adjacency[kA].Add(kB);
        adjacency[kB].Add(kA);
    }

    /// <summary>
    /// 그래프를 순회해 연속적인 폴리곤 포인트 목록을 구성.
    /// 마지막 점 = 첫 점으로 폐곡선 처리.
    /// </summary>
    private static List<Vector2> TracePolygon(
        Dictionary<Vector2Int, Vector2>        keyToPoint,
        Dictionary<Vector2Int, List<Vector2Int>> adjacency)
    {
        var polygon = new List<Vector2>(keyToPoint.Count);
        var visited = new HashSet<Vector2Int>();

        Vector2Int startKey = default;
        foreach (var k in keyToPoint.Keys) { startKey = k; break; }

        var current = startKey;
        while (true)
        {
            if (visited.Contains(current)) break;
            visited.Add(current);
            polygon.Add(keyToPoint[current]);

            bool foundNext = false;
            foreach (var neighbor in adjacency[current])
            {
                if (!visited.Contains(neighbor))
                {
                    current = neighbor;
                    foundNext = true;
                    break;
                }
            }
            if (!foundNext) break;
        }

        // UILineRenderer는 폐곡선을 지원하지 않으므로 첫 점을 끝에 추가
        if (polygon.Count >= 2)
            polygon.Add(polygon[0]);

        return polygon;
    }
}
