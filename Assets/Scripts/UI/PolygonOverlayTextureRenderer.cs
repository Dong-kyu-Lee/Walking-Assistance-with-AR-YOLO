using System.Collections.Generic;
using UnityEngine;

public class PolygonOverlayTextureRenderer : MonoBehaviour
{
    [SerializeField] private Material lineMaterial;
    [SerializeField, Min(1)] private int lineThicknessPx = 3;

    private RenderTexture overlayRT;
    private Mesh overlayMesh;

    // 매 프레임 Clear()만 하므로 GC 할당 없음
    private readonly List<Vector3> _verts  = new List<Vector3>(4096);
    private readonly List<int>     _tris   = new List<int>(8192);
    private readonly List<Color>   _colors = new List<Color>(4096);

    private readonly List<(Vector2[] points, Color color)> pendingPolygons = new();

    void Start()
    {
        overlayRT = new RenderTexture(Screen.width, Screen.height, 0,
                                      RenderTextureFormat.ARGB32);
        overlayRT.antiAliasing = 1;
        Shader.SetGlobalTexture("_PolygonOverlayTex", overlayRT);
        Shader.SetGlobalFloat("_PolygonOverlayAvailable", 0f);

        overlayMesh = new Mesh();
        overlayMesh.MarkDynamic(); // 매 프레임 갱신됨을 GPU에 알림
    }

    public void ClearAll()  => pendingPolygons.Clear();

    public void ShowPolygon(int id, Vector2[] normalizedPoints, Color color)
        => pendingPolygons.Add((normalizedPoints, color));

    void LateUpdate() => DrawToTexture();

    void DrawToTexture()
    {
        if (pendingPolygons.Count == 0)
        {
            Shader.SetGlobalFloat("_PolygonOverlayAvailable", 0f);
            return;
        }

        _verts.Clear();
        _tris.Clear();
        _colors.Clear();

        float halfThick = lineThicknessPx * 0.5f;
        float w = overlayRT.width;
        float h = overlayRT.height;

        foreach (var (points, col) in pendingPolygons)
        {
            int n = points.Length;
            if (n < 2) continue;

            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = new Vector2(points[i].x * w, points[i].y * h);
                Vector2 p1 = new Vector2(points[(i + 1) % n].x * w,
                                         points[(i + 1) % n].y * h);

                Vector2 dir = p1 - p0;
                float len = dir.magnitude;
                if (len < 0.001f) continue;
                dir /= len;

                // 수직 벡터로 선분을 두께 있는 사각형으로 확장
                var perp = new Vector2(-dir.y, dir.x) * halfThick;

                int b = _verts.Count;
                _verts.Add(new Vector3(p0.x + perp.x, p0.y + perp.y, 0));
                _verts.Add(new Vector3(p0.x - perp.x, p0.y - perp.y, 0));
                _verts.Add(new Vector3(p1.x + perp.x, p1.y + perp.y, 0));
                _verts.Add(new Vector3(p1.x - perp.x, p1.y - perp.y, 0));

                _colors.Add(col); _colors.Add(col);
                _colors.Add(col); _colors.Add(col);

                // 사각형 = 삼각형 2개
                _tris.Add(b);     _tris.Add(b + 2); _tris.Add(b + 1);
                _tris.Add(b + 1); _tris.Add(b + 2); _tris.Add(b + 3);
            }
        }

        if (_verts.Count == 0)
        {
            Shader.SetGlobalFloat("_PolygonOverlayAvailable", 0f);
            return;
        }

        overlayMesh.Clear();
        overlayMesh.SetVertices(_verts);
        overlayMesh.SetColors(_colors);
        overlayMesh.SetTriangles(_tris, 0);

        var prev = RenderTexture.active;
        RenderTexture.active = overlayRT;
        GL.Clear(true, true, Color.clear);

        GL.PushMatrix();
        GL.LoadPixelMatrix(0, overlayRT.width, overlayRT.height, 0);
        lineMaterial.SetPass(0);
        Graphics.DrawMeshNow(overlayMesh, Matrix4x4.identity); // 단 1번 DrawCall
        GL.PopMatrix();

        RenderTexture.active = prev;
        Shader.SetGlobalFloat("_PolygonOverlayAvailable", 1f);
    }

    void OnDestroy()
    {
        if (overlayRT)   overlayRT.Release();
        if (overlayMesh) Destroy(overlayMesh);
    }
}
