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

        // 중복 실행 방지 (n 프레임마다 실행되도록 함)
        isProcessing = true;

        StartCoroutine(ProcessImageAndRunInference(image));
    }

    private IEnumerator ProcessImageAndRunInference(XRCpuImage image)
    {
        // 이미지 크개를 YOLO 모델 입력 사이즈(640)에 맞춰 최적화
        float ratio = (float)image.width / image.height;
        int targetWidth = 640;
        int targetHeight = Mathf.RoundToInt(640/ratio);

        // 가로 해상도가 640 이하라면 원본 크기 유지
        if (image.width < 640)
        {
            targetWidth = image.width;
            targetHeight = image.height;
        }

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(targetWidth, targetHeight),
            outputFormat = TextureFormat.RGB24,
            transformation = XRCpuImage.Transformation.None
        };

        // AR 카메라 이미지를 '비동기'로 텍스처 데이터로 변환 요청
        var request = image.ConvertAsync(conversionParams);

        // 변환이 끝날 때까지 메인 스레드(화면 렌더링)를 멈추지 않고 프레임 양보
        while (!request.status.IsDone())
        {
            yield return null;
        }

        // 혹시 에러가 났다면 리소스 정리 후 종료
        if (request.status != XRCpuImage.AsyncConversionStatus.Ready)
        {
            image.Dispose();
            request.Dispose();
            isProcessing = false;
            yield break; 
        }

        // 텍스처 생성 또는 재사용
        if (cameraTexture == null || cameraTexture.width != conversionParams.outputDimensions.x || cameraTexture.height != conversionParams.outputDimensions.y)
        {
            cameraTexture = new Texture2D(conversionParams.outputDimensions.x, conversionParams.outputDimensions.y, TextureFormat.RGB24, false);
        }

        // 원시 데이터로 변환
        var rawTextureData = cameraTexture.GetRawTextureData<byte>();
        request.GetData<byte>().CopyTo(rawTextureData); // 백그라운드에서 변환된 데이터를 텍스처로 복사

        // 텍스처 GPU 업로드 (동기적)
        cameraTexture.Apply();

        // 사용 끝난 원본 이미지와 리퀘스트 즉시 메모리 해제 (메모리 누수 방지)
        image.Dispose();
        request.Dispose();

        // YOLO 추론 실행
        yield return StartCoroutine(yoloProcessor.ExecuteML(cameraTexture));

        // 사용자가 설정한 inferenceInterval 프레임 수만큼 대기
        for (int i = 0; i < inferenceInterval; i++)
        {
            yield return null; // 1프레임 쉼
        }

        // 대기가 끝나면 플래그를 해제하여 다음 프레임을 받을 수 있게 함
        isProcessing = false;
    }
}