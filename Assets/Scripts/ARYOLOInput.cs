using System;
using System.Collections; // 코루틴 사용을 위해 추가
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARYOLOInput : MonoBehaviour
{
    [Header("AR Components")]
    public ARCameraManager cameraManager;

    [Header("YOLO Reference")]
    public RunYOLO yoloProcessor; // 기존 RunYOLO 스크립트 참조

    [Header("Optimization Settings")]
    [Tooltip("몇 프레임마다 추론을 실행할지 설정합니다 (예: 5 = 5프레임당 1번 추론)")]
    [Range(1, 60)]
    public int inferenceInterval = 5; // 인스펙터에서 조절 가능한 프레임 간격 변수

    private Texture2D cameraTexture;
    private bool isProcessing = false; // 현재 추론 및 대기 중인지 확인하는 플래그

    void OnEnable()
    {
        // 카메라 프레임이 업데이트될 때마다 호출될 이벤트 연결
        cameraManager.frameReceived += OnCameraFrameReceived;
    }

    void OnDisable()
    {
        cameraManager.frameReceived -= OnCameraFrameReceived;
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        // 코루틴이 실행 중(추론 대기 중)이라면 이번 카메라 프레임은 무시하고 넘어감
        if (isProcessing) return;

        // 1. AR 카메라의 최신 텍스처(GPU)를 가져옵니다.
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
        {
            return;
        }

        // 이미지 변환 설정
        int downsample = 1;
        if (image.width > 1000) downsample = 2;

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width / downsample, image.height / downsample),
            outputFormat = TextureFormat.RGB24,
            transformation = XRCpuImage.Transformation.None
        };

        // 텍스처 생성 또는 재사용
        if (cameraTexture == null || cameraTexture.width != conversionParams.outputDimensions.x || cameraTexture.height != conversionParams.outputDimensions.y)
        {
            cameraTexture = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, TextureFormat.RGB24, false);
        }

        // 원시 데이터로 변환
        var rawTextureData = cameraTexture.GetRawTextureData<byte>();
        image.Convert(conversionParams, rawTextureData);
        image.Dispose(); // 중요: 사용 후 이미지 리소스 해제

        // 텍스처 GPU 업로드
        cameraTexture.Apply();

        // 2. 코루틴을 시작하여 YOLO 추론을 실행하고 지정된 프레임만큼 대기
        StartCoroutine(ProcessAndSkipFrames());
    }

    private IEnumerator ProcessAndSkipFrames()
    {
        // 플래그를 true로 설정하여 대기 시간 동안 새로운 프레임을 처리하지 않도록 잠금
        isProcessing = true;

        // YOLO 추론 실행
        yoloProcessor.ExecuteML(cameraTexture);

        // 사용자가 설정한 inferenceInterval 프레임 수만큼 대기
        for (int i = 0; i < inferenceInterval; i++)
        {
            yield return null; // 1프레임 쉼
        }

        // 대기가 끝나면 플래그를 해제하여 다음 프레임을 받을 수 있게 함
        isProcessing = false;
    }
}