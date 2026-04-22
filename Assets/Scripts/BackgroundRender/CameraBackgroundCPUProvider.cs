using UnityEngine;

public class CameraBackgroundCPUProvider : MonoBehaviour
{
    [Header("Background Material")]
    [SerializeField] private Material backgroundMaterial;

    [Header("Editor Fallback")]
    [SerializeField] private Texture editorFallbackTexture;

    [Header("Shader Property")]
    [SerializeField] private string texturePropertyName = "_Step2BackgroundTex";
    [SerializeField] private string debugModePropertyName = "_DebugMode";

    [Header("Debug")]
    [SerializeField] private bool verboseLog = true;

    private AndroidJavaObject cameraController;
    private Texture2D cameraTexture;

    private int backgroundTexId;
    private int debugModeId;

    private const string PluginClassName = "com.example.unitycamerax.CameraXController";

    // 상태 확인용
    private Texture2D texBlue;
    private Texture2D texYellow;
    private Texture2D texMagenta;
    private Texture2D texCyan;

    private void Awake()
    {
        backgroundTexId = Shader.PropertyToID(texturePropertyName);
        debugModeId = Shader.PropertyToID(debugModePropertyName);

        CreateDebugTextures();

        if (backgroundMaterial != null)
        {
            backgroundMaterial.SetFloat(debugModeId, 0f);
            backgroundMaterial.SetTexture(backgroundTexId, texBlue);
        }
    }

    private void Start()
    {
        if (Application.platform != RuntimePlatform.Android)
        {
            if (backgroundMaterial != null)
            {
                backgroundMaterial.SetTexture(
                    backgroundTexId,
                    editorFallbackTexture != null ? editorFallbackTexture : texBlue
                );
            }

            if (verboseLog)
                Debug.Log("[CameraBackgroundCpuProvider] 에디터: fallback 사용");

            return;
        }

        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject currentActivity =
                    unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                cameraController = new AndroidJavaObject(PluginClassName, currentActivity);
                cameraController.Call("startCamera");

                if (backgroundMaterial != null)
                    backgroundMaterial.SetTexture(backgroundTexId, texBlue);

                if (verboseLog)
                    Debug.Log("[CameraBackgroundCpuProvider] startCamera 호출 완료");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraBackgroundCpuProvider] 카메라 초기화 실패: {e}");

            if (backgroundMaterial != null)
                backgroundMaterial.SetTexture(backgroundTexId, texMagenta);
        }
    }

    private void Update()
    {
        if (backgroundMaterial == null)
            return;

        if (Application.platform != RuntimePlatform.Android)
            return;

        if (cameraController == null)
        {
            backgroundMaterial.SetTexture(backgroundTexId, texMagenta);
            return;
        }

        UpdateCameraTexture();
    }

    private void UpdateCameraTexture()
    {
        int width = 0;
        int height = 0;

        try
        {
            width = cameraController.Call<int>("getFrameWidth");
            height = cameraController.Call<int>("getFrameHeight");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraBackgroundCpuProvider] 크기 조회 실패: {e}");
            backgroundMaterial.SetTexture(backgroundTexId, texMagenta);
            return;
        }

        if (width <= 0 || height <= 0)
        {
            backgroundMaterial.SetTexture(backgroundTexId, texYellow);

            if (verboseLog)
                Debug.Log($"[CameraBackgroundCpuProvider] width/height = 0 ({width}x{height})");

            return;
        }

        byte[] frameData = null;

        try
        {
            frameData = cameraController.Call<byte[]>("getLatestFrameData");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CameraBackgroundCpuProvider] frameData 조회 실패: {e}");
            backgroundMaterial.SetTexture(backgroundTexId, texMagenta);
            return;
        }

        if (frameData == null || frameData.Length == 0)
        {
            backgroundMaterial.SetTexture(backgroundTexId, texMagenta);

            if (verboseLog)
                Debug.Log("[CameraBackgroundCpuProvider] frameData null/empty");

            return;
        }

        int expectedLength = width * height * 4; // RGBA32 가정
        if (frameData.Length < expectedLength)
        {
            backgroundMaterial.SetTexture(backgroundTexId, texCyan);

            if (verboseLog)
                Debug.LogWarning($"[CameraBackgroundCpuProvider] frame length mismatch: actual={frameData.Length}, expected={expectedLength}");

            return;
        }

        if (cameraTexture == null || cameraTexture.width != width || cameraTexture.height != height)
        {
            if (cameraTexture != null)
                Destroy(cameraTexture);

            cameraTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            cameraTexture.wrapMode = TextureWrapMode.Clamp;
            cameraTexture.filterMode = FilterMode.Bilinear;

            if (verboseLog)
                Debug.Log($"[CameraBackgroundCpuProvider] cameraTexture 생성 {width}x{height}");
        }

        cameraTexture.LoadRawTextureData(frameData);
        cameraTexture.Apply(false, false);

        backgroundMaterial.SetTexture(backgroundTexId, cameraTexture);

        if (verboseLog)
            Debug.Log($"[CameraBackgroundCpuProvider] 카메라 적용 성공 {width}x{height}, bytes={frameData.Length}");
    }

    private void OnDestroy()
    {
        if (cameraController != null)
        {
            try
            {
                cameraController.Call("stopCamera");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[CameraBackgroundCpuProvider] stopCamera 실패: {e}");
            }

            cameraController = null;
        }

        if (cameraTexture != null)
        {
            Destroy(cameraTexture);
            cameraTexture = null;
        }

        if (texBlue != null) Destroy(texBlue);
        if (texYellow != null) Destroy(texYellow);
        if (texMagenta != null) Destroy(texMagenta);
        if (texCyan != null) Destroy(texCyan);
    }

    private void CreateDebugTextures()
    {
        texBlue = CreateSolidTexture(Color.blue);
        texYellow = CreateSolidTexture(Color.yellow);
        texMagenta = CreateSolidTexture(Color.magenta);
        texCyan = CreateSolidTexture(Color.cyan);
    }

    private Texture2D CreateSolidTexture(Color color)
    {
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;

        Color[] pixels = new Color[4] { color, color, color, color };
        tex.SetPixels(pixels);
        tex.Apply(false, false);

        return tex;
    }
}
