// PolygonOverlayTextureRenderer.cs
using System.Collections.Generic;
using UnityEngine;

public class PolygonOverlayTextureRenderer : MonoBehaviour
{
    [SerializeField] private Material lineMaterial; // 기본 Sprites/Default 사용

    // GL.LINE_STRIP은 GLES3에서 두께 1만 지원하므로, 픽셀 오프셋 다중 패스로 두께를 흉내냄.
    // 값이 1이면 단일 선, 3이면 3×3=9패스, 5이면 5×5=25패스 (홀수 권장).
    [SerializeField, Min(1)] private int lineThicknessPx = 3;

    private RenderTexture overlayRT;
    private readonly List<(Vector2[] points, Color color)> pendingPolygons = new();

    void Start()
    {
        // 화면 해상도로 RenderTexture 생성
        overlayRT = new RenderTexture(Screen.width, Screen.height, 0,
                                      RenderTextureFormat.ARGB32);
        overlayRT.antiAliasing = 1;
        Shader.SetGlobalTexture("_PolygonOverlayTex", overlayRT);
        Shader.SetGlobalFloat("_PolygonOverlayAvailable", 0f);
    }

    public void ClearAll()
    {
        pendingPolygons.Clear();
    }

    public void ShowPolygon(int id, Vector2[] normalizedPoints, Color color)
    {
        pendingPolygons.Add((normalizedPoints, color));
    }

    // OnPreRender는 Camera 컴포넌트가 있는 GameObject에만 호출되므로 사용 불가.
    // LateUpdate는 모든 Update/Coroutine 완료 후 호출되어
    // RunYOLO.ExecuteML이 ShowPolygon을 채운 뒤 반드시 실행됨.
    void LateUpdate() => DrawToTexture();

    void DrawToTexture()
    {
        if (pendingPolygons.Count == 0)
        {
            Shader.SetGlobalFloat("_PolygonOverlayAvailable", 0f);
            return;
        }

        var prev = RenderTexture.active;
        RenderTexture.active = overlayRT;
        GL.Clear(true, true, Color.clear);

        GL.PushMatrix();
        // bottom=height, top=0 으로 설정해 GL y=0이 RT 위쪽(UV.y=1)에 저장되게 함.
        // 셰이더의 ST 변환(localUV.y = 1 - screenUV.y) 이후 RT를 샘플링하므로,
        // 화면 위쪽(screenUV.y=1) → localUV.y=0 → RT UV.y=0,
        // 화면 아래쪽(screenUV.y=0) → localUV.y=1 → RT UV.y=1 로 읽힘.
        // canvasPoints.y = 1 - maskY/res 이므로 위쪽 물체(maskY=0) → canvasY=1 → GL y=h → RT UV.y=0 ✓
        //                                        아래쪽 물체(maskY=max) → canvasY=0 → GL y=0 → RT UV.y=1 ✓
        GL.LoadPixelMatrix(0, overlayRT.width, overlayRT.height, 0);
        lineMaterial.SetPass(0);

        int half = lineThicknessPx / 2;
        foreach (var (points, col) in pendingPolygons)
        {
            // 두께만큼 픽셀 오프셋을 달리한 선을 여러 번 그려 굵기를 만듦
            for (int ox = -half; ox <= half; ox++)
            {
                for (int oy = -half; oy <= half; oy++)
                {
                    GL.Begin(GL.LINE_STRIP);
                    GL.Color(col);
                    foreach (var p in points)
                        GL.Vertex3(p.x * overlayRT.width + ox,
                                   p.y * overlayRT.height + oy, 0);
                    // 첫 점을 다시 추가해 폐곡선 닫기
                    if (points.Length > 0)
                        GL.Vertex3(points[0].x * overlayRT.width + ox,
                                   points[0].y * overlayRT.height + oy, 0);
                    GL.End();
                }
            }
        }

        GL.PopMatrix();
        RenderTexture.active = prev;
        Shader.SetGlobalFloat("_PolygonOverlayAvailable", 1f);
    }

    void OnDestroy()
    {
        if (overlayRT) overlayRT.Release();
    }
}
