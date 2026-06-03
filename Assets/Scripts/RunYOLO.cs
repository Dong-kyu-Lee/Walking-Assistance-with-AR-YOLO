using System;
using System.Collections;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using Unity.Jobs;
using Unity.Collections;
using Unity.Burst;
using Unity.Mathematics;

public class RunYOLO : MonoBehaviour
{
    [Tooltip("Drag a YOLO model .onnx file here")]
    public ModelAsset modelAsset;

    [Tooltip("Drag the classes.txt here")]
    public TextAsset classesAsset;

    [Tooltip("Create a Raw Image in the scene and link it here")]
    public RawImage displayImage;

    [Tooltip("Drag a border box texture here")]
    public Texture2D borderTexture;

    [Tooltip("Select an appropriate font for the labels")]
    public Font font;

    [Tooltip("Change this to the name of the video you put in the Assets/StreamingAssets folder")]
    public string videoFilename = "giraffes.mp4";

    [Tooltip("Check this if using with AR Foundation")]
    public bool isARMode = false;

    public bool IsModelLoaded { get { return isModelLoaded; } }

    const BackendType backend = BackendType.GPUCompute;

    private Transform displayLocation;
    private Worker worker;
    private string[] labels;
    private RenderTexture targetRT;
    private Sprite borderSprite;
    private bool isModelLoaded = false;

    private Tensor<float> inputTensor;

    private const int imageWidth = 640;
    private const int imageHeight = 640;
    private const int maskResolution = 160;

    List<GameObject> boxPool = new List<GameObject>();

    [Tooltip("Drag the PolygonOverlayRenderer component here")]
    [SerializeField] private PolygonOverlayTextureRenderer polygonRenderer;

    [SerializeField] private bool drawPolygonOverlay = true;

    [Tooltip("Intersection over union threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float iouThreshold = 0.45f;

    [Tooltip("Confidence score threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float scoreThreshold = 0.5f;

    [SerializeField] private RectTransform detectionOverlayRoot;

    [Tooltip("Drag the GroundPlaneDistanceEstimator component here")]
    [SerializeField] private GroundPlaneDistanceEstimator groundPlaneDistanceEstimator;

    [SerializeField] private AndroidTTS androidTTS;

    [Header("Demo JSON Export")]
    [SerializeField] private bool filterJsonObjectsByDistance = true;

    [Tooltip("거리 측정된 물체 중 폴리곤을 표시할 최대 개수 (가까운 순)")]
    [SerializeField] private int nearestPolygonCount = 3;

    [Tooltip("스마트폰 카메라 수평 화각 (도). 카메라 이동 보정 정확도에 영향.")]
    [SerializeField, Range(30f, 120f)] private float cameraFovH = 70f;

    // 사용자 인지 혼란을 줄이기 위해 중요하지 않은 클래스는 폴리곤 출력 제외
    private HashSet<string> polygonSkipLabels = new HashSet<string> { "sidewalk_normal", "sidewalk_damaged", "roadway", "bike_lane", "alley", "speed_bump", "ramp" };
    // 거리 여부에 관계 없이 무조건 폴리곤 출력할 클래스 (거리 계산은 탐지된 객체가 실제로 보행자에게 장애물이 될 때만 의미 있으므로)
    private HashSet<string> distanceSkipLabels = new HashSet<string> { "crosswalk", "stairs", "braille_blocks" };

    public struct BoundingBox
    {
        public float centerX;
        public float centerY;
        public float width;
        public float height;
        public string label;
    }

    void Start()
    {
        Application.targetFrameRate = 40;
        if (!isARMode) Screen.orientation = ScreenOrientation.LandscapeLeft;

        labels = classesAsset.text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        LoadModel();

        targetRT = new RenderTexture(imageWidth, imageHeight, 0);
        inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));

        displayLocation = displayImage.transform;
        borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, borderTexture.width, borderTexture.height), new Vector2(borderTexture.width / 2, borderTexture.height / 2));

        if (polygonRenderer != null)
        {
            polygonRenderer.SetOverlayEnabled(drawPolygonOverlay);
        }

        isModelLoaded = true;
    }

    void LoadModel()
    {
        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, backend);
        Debug.Log("모델 로드");
    }

    void OnDestroy()
    {
        worker?.Dispose();
        inputTensor?.Dispose();

        if (targetRT != null)
        {
            targetRT.Release();
            Destroy(targetRT);
            targetRT = null;
        }
    }

    public IEnumerator ExecuteML(Texture sourceTexture, Quaternion captureAttitude = default)
    {
        //ClearAnnotations();

        if (sourceTexture == null) yield break;

        // UV 좌표의 Y축을 반전시켜 텍스처를 상하로 뒤집어 targetRT에 복사
        Graphics.Blit(sourceTexture, targetRT, new Vector2(1, -1), new Vector2(0, 1));

        TextureConverter.ToTensor(targetRT, inputTensor, default);

        worker.Schedule(inputTensor);

        // GPU 추론 커맨드 제출 후 즉시 Readback 요청하지 않고 1프레임 양보
        // → 렌더링과 GPU 추론이 같은 프레임 내에서 경쟁하지 않도록 분리
        yield return null;

        var output0 = worker.PeekOutput("output0") as Tensor<float>;
        var output1 = worker.PeekOutput("output1") as Tensor<float>;

        // GPU→CPU 비동기 Readback 요청
        output0.ReadbackRequest();
        output1.ReadbackRequest();

        while (!output0.IsReadbackRequestDone() || !output1.IsReadbackRequestDone())
        {
            yield return null;
        }

        var shape0 = output0.shape;
        int dim1 = shape0.rank > 1 ? shape0[shape0.rank - 2] : 1;
        int dim2 = shape0.rank > 0 ? shape0[shape0.rank - 1] : 1;

        int numAnchors = math.max(dim1, dim2);
        int numAttributes = math.min(dim1, dim2);
        int numMaskWeights = 32;
        int numClasses = numAttributes - 4 - numMaskWeights;
        bool isTransposed = (dim1 == numAnchors);

        var outputData0 = output0.DownloadToNativeArray();
        var outputData1 = output1.DownloadToNativeArray();

        var resultBoxes = new NativeList<BoxData>(Allocator.TempJob);
        // NMS 통과 박스별 32개 가중치를 순서대로 저장 (b번째 박스 → [b*32 .. b*32+31])
        var resultWeights = new NativeList<float>(Allocator.TempJob);

        try
        {
            var nmsJob = new NMSJob
            {
                outputData = outputData0,
                numAnchors = numAnchors,
                numClasses = numClasses,
                numMaskWeights = numMaskWeights,
                isTransposed = isTransposed,
                iouThreshold = iouThreshold,
                scoreThreshold = scoreThreshold,
                resultBoxes = resultBoxes,
                resultWeights = resultWeights
            };
            nmsJob.Schedule().Complete();

            if (groundPlaneDistanceEstimator != null)
            {
                Debug.Log("거리 정보 처리 시작");
                groundPlaneDistanceEstimator.Process(
                    resultBoxes,
                    labels,
                    imageWidth,
                    imageHeight,
                    sourceTexture != null ? sourceTexture.width : 0,
                    sourceTexture != null ? sourceTexture.height : 0
                );
            }

            if (polygonRenderer != null)
            {
                polygonRenderer.SetOverlayEnabled(drawPolygonOverlay);
            }

            if (drawPolygonOverlay && polygonRenderer != null)
            {
                polygonRenderer.ClearAll();
            }

            int numBoxes = math.min(resultBoxes.Length, 200);
            // 폴리곤 출력할 박스 인덱스 집합 계산
            HashSet<int> polygonShowIndices = BuildPolygonShowSet(
                resultBoxes,
                groundPlaneDistanceEstimator != null ? groundPlaneDistanceEstimator.GetLastResults() : null,
                labels,
                numBoxes);

            if (numBoxes > 0)
            {
                // 객체 수 × maskRes² 크기의 per-object binary mask 배열 (0=배경, 1=전경)
                var perObjectMasks = new NativeArray<byte>(numBoxes * maskResolution * maskResolution, Allocator.TempJob);
                try
                {
                    var maskJob = new MaskGenerationJob
                    {
                        prototypes    = outputData1,
                        boxes         = resultBoxes.AsArray().GetSubArray(0, numBoxes),
                        maskWeights   = resultWeights.AsArray().GetSubArray(0, numBoxes * numMaskWeights),
                        perObjectMasks = perObjectMasks,
                        maskRes       = maskResolution,
                        imgRes        = imageWidth,
                        numMaskWeights = numMaskWeights
                    };
                    maskJob.Schedule(maskResolution * maskResolution, 64).Complete();

                    // 추론 시작과 현재 사이의 카메라 회전 변화량 계산 (카메라 이동 보정)
                    Quaternion currentAttitude = Input.gyro.enabled ? Input.gyro.attitude : Quaternion.identity;
                    Quaternion rotDelta = Quaternion.Inverse(captureAttitude) * currentAttitude;
                    // 카메라 종횡비: 마스크는 정방형이지만 실제 카메라는 16:9 등 비정방형이므로
                    // X·Y 초점거리를 각각 다르게 적용해야 위아래 보정 배율이 맞음
                    float cameraAspect = sourceTexture != null
                        ? (float)sourceTexture.width / sourceTexture.height
                        : 16f / 9f;

                    for (int i = 0; i < numBoxes; i++)
                    {
                        if (!polygonShowIndices.Contains(i)) continue;

                        var contourPoints = MarchingSquaresUtil.GetContour(perObjectMasks, maskResolution, i);
                        if (contourPoints.Count < 3) continue;

                        // mask 좌표 → UILineRenderer scale=true 기준 정규화 좌표 [0,1]
                        // normY: Y축 반전 (maskY=0이 이미지 상단 → normY=1이 Canvas 상단)
                        var canvasPoints = new Vector2[contourPoints.Count];
                        for (int p = 0; p < contourPoints.Count; p++)
                        {
                            Vector2 normalized = new Vector2(
                                contourPoints[p].x / maskResolution,
                                1.0f - contourPoints[p].y / maskResolution
                            );
                            canvasPoints[p] = CompensateRotation(normalized, rotDelta, cameraAspect);
                        }

                        if (drawPolygonOverlay && polygonRenderer != null)
                        {
                            polygonRenderer.ShowPolygon(i, canvasPoints, Color.green);
                        }
                    }
                }
                finally
                {
                    if (perObjectMasks.IsCreated) perObjectMasks.Dispose();
                }
            }

            // ProcessResults(resultBoxes); (바운딩박스는 출력 제외, 데이터는 가져옴)
        }
        finally
        {
            if (resultBoxes.IsCreated) resultBoxes.Dispose();
            if (resultWeights.IsCreated) resultWeights.Dispose();
            if (outputData0.IsCreated) outputData0.Dispose();
            if (outputData1.IsCreated) outputData1.Dispose();
        }
    }

    public IEnumerator ExecuteMLForJson(
    Texture sourceTexture,
    Action<List<DemoObjectJson>> onComplete,
    Quaternion captureAttitude = default)
    {
        yield return ExecuteMLInternal(
            sourceTexture,
            captureAttitude,
            drawPolygon: false,
            onComplete: onComplete
        );
    }

    private IEnumerator ExecuteMLInternal(
    Texture sourceTexture,
    Quaternion captureAttitude,
    bool drawPolygon,
    Action<List<DemoObjectJson>> onComplete)
    {
        List<DemoObjectJson> jsonObjects = new();

        if (sourceTexture == null)
        {
            onComplete?.Invoke(jsonObjects);
            yield break;
        }

        // UV 좌표의 Y축을 반전시켜 텍스처를 상하로 뒤집어 targetRT에 복사
        Graphics.Blit(sourceTexture, targetRT, new Vector2(1, -1), new Vector2(0, 1));

        TextureConverter.ToTensor(targetRT, inputTensor, default);

        worker.Schedule(inputTensor);

        // GPU 추론 커맨드 제출 후 1프레임 대기
        yield return null;

        var output0 = worker.PeekOutput("output0") as Tensor<float>;
        var output1 = worker.PeekOutput("output1") as Tensor<float>;

        if (output0 == null || output1 == null)
        {
            Debug.LogError("YOLO output0 또는 output1을 찾을 수 없습니다.");
            onComplete?.Invoke(jsonObjects);
            yield break;
        }

        output0.ReadbackRequest();
        output1.ReadbackRequest();

        while (!output0.IsReadbackRequestDone() || !output1.IsReadbackRequestDone())
        {
            yield return null;
        }

        var shape0 = output0.shape;
        int dim1 = shape0.rank > 1 ? shape0[shape0.rank - 2] : 1;
        int dim2 = shape0.rank > 0 ? shape0[shape0.rank - 1] : 1;

        int numAnchors = math.max(dim1, dim2);
        int numAttributes = math.min(dim1, dim2);
        int numMaskWeights = 32;
        int numClasses = numAttributes - 4 - numMaskWeights;
        bool isTransposed = dim1 == numAnchors;

        var outputData0 = output0.DownloadToNativeArray();
        var outputData1 = output1.DownloadToNativeArray();

        var resultBoxes = new NativeList<BoxData>(Allocator.TempJob);
        var resultWeights = new NativeList<float>(Allocator.TempJob);

        try
        {
            var nmsJob = new NMSJob
            {
                outputData = outputData0,
                numAnchors = numAnchors,
                numClasses = numClasses,
                numMaskWeights = numMaskWeights,
                isTransposed = isTransposed,
                iouThreshold = iouThreshold,
                scoreThreshold = scoreThreshold,
                resultBoxes = resultBoxes,
                resultWeights = resultWeights
            };

            nmsJob.Schedule().Complete();

            if (groundPlaneDistanceEstimator != null)
            {
                groundPlaneDistanceEstimator.Process(
                    resultBoxes,
                    labels,
                    imageWidth,
                    imageHeight,
                    sourceTexture != null ? sourceTexture.width : 0,
                    sourceTexture != null ? sourceTexture.height : 0
                );
            }

            if (drawPolygon && polygonRenderer != null)
            {
                polygonRenderer.ClearAll();
            }

            int numBoxes = math.min(resultBoxes.Length, 200);

            HashSet<int> polygonShowIndices = !drawPolygon && !filterJsonObjectsByDistance
                ? BuildAllObjectSet(numBoxes)
                : BuildPolygonShowSet(
                    resultBoxes,
                    groundPlaneDistanceEstimator != null ? groundPlaneDistanceEstimator.GetLastResults() : null,
                    labels,
                    numBoxes
                );

            if (numBoxes > 0)
            {
                var perObjectMasks =
                    new NativeArray<byte>(numBoxes * maskResolution * maskResolution, Allocator.TempJob);

                try
                {
                    var maskJob = new MaskGenerationJob
                    {
                        prototypes = outputData1,
                        boxes = resultBoxes.AsArray().GetSubArray(0, numBoxes),
                        maskWeights = resultWeights.AsArray().GetSubArray(0, numBoxes * numMaskWeights),
                        perObjectMasks = perObjectMasks,
                        maskRes = maskResolution,
                        imgRes = imageWidth,
                        numMaskWeights = numMaskWeights
                    };

                    maskJob.Schedule(maskResolution * maskResolution, 64).Complete();

                    Quaternion currentAttitude = Input.gyro.enabled
                        ? Input.gyro.attitude
                        : Quaternion.identity;

                    Quaternion rotDelta = Quaternion.Inverse(captureAttitude) * currentAttitude;

                    float cameraAspect = sourceTexture != null
                        ? (float)sourceTexture.width / sourceTexture.height
                        : 16f / 9f;

                    for (int i = 0; i < numBoxes; i++)
                    {
                        if (!polygonShowIndices.Contains(i))
                            continue;

                        var contourPoints = MarchingSquaresUtil.GetContour(
                            perObjectMasks,
                            maskResolution,
                            i
                        );

                        if (contourPoints.Count < 3)
                            continue;

                        var canvasPoints = new Vector2[contourPoints.Count];

                        for (int p = 0; p < contourPoints.Count; p++)
                        {
                            Vector2 normalized = new Vector2(
                                contourPoints[p].x / maskResolution,
                                1.0f - contourPoints[p].y / maskResolution
                            );

                            canvasPoints[p] = CompensateRotation(
                                normalized,
                                rotDelta,
                                cameraAspect
                            );
                        }

                        BoxData box = resultBoxes[i];

                        string className =
                            box.classID >= 0 && box.classID < labels.Length
                                ? labels[box.classID]
                                : $"class_{box.classID}";

                        DemoObjectJson obj = new DemoObjectJson
                        {
                            className = className,
                            classId = box.classID,
                            score = box.score,
                            priority = GetDemoPriority(className),
                            box = new DemoBoxJson
                            {
                                cx = box.cx / imageWidth,
                                cy = box.cy / imageHeight,
                                w = box.w / imageWidth,
                                h = box.h / imageHeight
                            },
                            polygon = new List<DemoPointJson>()
                        };

                        for (int p = 0; p < canvasPoints.Length; p++)
                        {
                            obj.polygon.Add(new DemoPointJson
                            {
                                x = Mathf.Clamp01(canvasPoints[p].x),
                                y = Mathf.Clamp01(canvasPoints[p].y)
                            });
                        }

                        jsonObjects.Add(obj);

                        if (drawPolygon && polygonRenderer != null)
                        {
                            polygonRenderer.ShowPolygon(i, canvasPoints, Color.green);
                        }
                    }
                }
                finally
                {
                    if (perObjectMasks.IsCreated)
                        perObjectMasks.Dispose();
                }
            }
        }
        finally
        {
            if (resultBoxes.IsCreated) resultBoxes.Dispose();
            if (resultWeights.IsCreated) resultWeights.Dispose();
            if (outputData0.IsCreated) outputData0.Dispose();
            if (outputData1.IsCreated) outputData1.Dispose();
        }

        onComplete?.Invoke(jsonObjects);
    }

    private int GetDemoPriority(string className)
    {
        return className switch
        {
            "motorcycle" => 100,
            "bus" => 95,
            "truck" => 95,
            "car" => 90,
            "bicycle" => 80,
            "wheelchair" => 85,
            "person" => 70,
            "crosswalk" => 60,
            "stairs" => 80,
            "braille_blocks" => 60,
            _ => 50
        };
    }

    public void ClearAnnotations()
    {
        polygonRenderer.ClearAll();
    }

    public void SetPolygonOutputEnabled(bool enabled)
    {
        drawPolygonOverlay = enabled;

        if (polygonRenderer != null)
        {
            polygonRenderer.SetOverlayEnabled(enabled);
        }
    }

    public void TogglePolygonOutput()
    {
        SetPolygonOutputEnabled(!drawPolygonOverlay);
    }

    public void GetObstacleDetectedVoice(string className)
    {
        androidTTS.Speak($"{className} 장애물이 감지되었습니다.");
    }

    // 폴리곤을 출력할 박스 인덱스 집합을 반환한다.
    // - polygonSkipLabels: 항상 제외
    // - distanceSkipLabels: 거리와 무관하게 항상 포함
    // - 그 외: 거리 측정 성공한 것 중 nearestPolygonCount개만 포함
    private HashSet<int> BuildPolygonShowSet(
        NativeList<BoxData> boxes,
        IReadOnlyList<GroundPlaneDistanceEstimator.DetectionResult> distResults,
        string[] labels,
        int numBoxes)
    {
        var showSet = new HashSet<int>();

        if (distResults == null)
        {
            for (int i = 0; i < numBoxes; i++)
            {
                string lbl = (boxes[i].classID >= 0 && boxes[i].classID < labels.Length) ? labels[boxes[i].classID] : "";
                if (!polygonSkipLabels.Contains(lbl)) showSet.Add(i);
            }
            return showSet;
        }

        var measured = new List<(int idx, float dist)>();

        for (int i = 0; i < numBoxes; i++)
        {
            string lbl = (boxes[i].classID >= 0 && boxes[i].classID < labels.Length) ? labels[boxes[i].classID] : "";
            if (polygonSkipLabels.Contains(lbl)) continue;

            if (distanceSkipLabels.Contains(lbl))
            {
                showSet.Add(i);
            }
            else if (i < distResults.Count && distResults[i].isMeasured && distResults[i].distanceMeters > 0f)
            {
                measured.Add((i, distResults[i].distanceMeters));
            }
        }

        measured.Sort((a, b) => a.dist.CompareTo(b.dist));
        int take = math.min(math.max(nearestPolygonCount, 0), measured.Count);
        for (int i = 0; i < take; i++)
            showSet.Add(measured[i].idx);

        return showSet;
    }

    private HashSet<int> BuildAllObjectSet(int numBoxes)
    {
        var showSet = new HashSet<int>();

        for (int i = 0; i < numBoxes; i++)
        {
            showSet.Add(i);
        }

        return showSet;
    }

    // 캡처 시점과 현재 사이의 카메라 회전 변화량을 이용해 2D 정규화 좌표를 보정한다.
    // normPt: [0,1] 정규화 좌표, rotDelta: Inverse(captureAttitude) * currentAttitude
    // cameraAspect: 실제 카메라 종횡비 (width/height). X·Y 초점거리를 각각 계산하는 데 사용.
    private Vector2 CompensateRotation(Vector2 normPt, Quaternion rotDelta, float cameraAspect)
    {
        // [0,1] → 중심 기준 좌표
        float cx = normPt.x - 0.5f;
        float cy = normPt.y - 0.5f;

        // 수평 화각으로 수평 초점거리 추정 (정규화 단위)
        float fH = 0.5f / Mathf.Tan(cameraFovH * 0.5f * Mathf.Deg2Rad);
        // 수직 초점거리: 카메라가 비정방형이므로 fV = fH * aspectRatio
        // (수직 화각이 수평보다 좁은 만큼, 같은 정규화 거리가 더 작은 각도를 커버함)

        // 카메라 공간 3D 방향 벡터 (Y축 반전: 화면 아래 = 카메라 -Y)
        // Y를 cameraAspect로 나눠 수직 화각 차이를 보정
        Vector3 dir = new Vector3(cx, -cy / cameraAspect, fH);

        // 회전 델타를 적용해 현재 카메라 공간에서의 투영 위치 계산
        Vector3 compensated = rotDelta * dir;

        // 자이로 pitch(위아래) 극성이 화면 Y축과 반대이므로 Y 변위만 반전
        // (기본 폴리곤 위치는 dir.y 기준이고, 변위 방향만 뒤집어야 하므로 cy 부호 전체를 바꾸지 않음)
        compensated.y = 2f * dir.y - compensated.y;

        if (compensated.z <= 0f) return normPt;

        float newCx = compensated.x / compensated.z * fH;
        // 역투영 시 Y는 fH * aspectRatio 로 스케일 복원
        float newCy = -compensated.y / compensated.z * fH * cameraAspect;

        return new Vector2(newCx + 0.5f, newCy + 0.5f);
    }
}

public struct BoxData
{
    public float cx, cy, w, h;
    public int classID;
    public float score;
}

[BurstCompile]
public struct NMSJob : IJob
{
    [ReadOnly] public NativeArray<float> outputData;

    public int numAnchors;
    public int numClasses;
    public int numMaskWeights;
    public bool isTransposed;

    public float iouThreshold;
    public float scoreThreshold;

    public NativeList<BoxData> resultBoxes;
    // 각 결과 박스의 마스크 가중치 (numMaskWeights개씩 순서대로 저장)
    public NativeList<float> resultWeights;

    public void Execute()
    {
        var candidates = new NativeList<BoxData>(numAnchors, Allocator.Temp);
        // candidates와 1:1 대응하는 가중치 (numMaskWeights개씩 연속 저장)
        var candWeights = new NativeList<float>(numAnchors * numMaskWeights, Allocator.Temp);

        for (int a = 0; a < numAnchors; a++)
        {
            float maxScore = -1f;
            int bestClassID = -1;

            for (int c = 0; c < numClasses; c++)
            {
                float score = isTransposed
                    ? outputData[a * (4 + numClasses + numMaskWeights) + (c + 4)]
                    : outputData[(c + 4) * numAnchors + a];

                if (score > maxScore)
                {
                    maxScore = score;
                    bestClassID = c;
                }
            }

            if (maxScore >= scoreThreshold)
            {
                BoxData box = new BoxData();
                box.score = maxScore;
                box.classID = bestClassID;

                if (isTransposed)
                {
                    int baseIdx = a * (4 + numClasses + numMaskWeights);
                    box.cx = outputData[baseIdx + 0];
                    box.cy = outputData[baseIdx + 1];
                    box.w  = outputData[baseIdx + 2];
                    box.h  = outputData[baseIdx + 3];

                    for (int w = 0; w < numMaskWeights; w++)
                        candWeights.Add(outputData[baseIdx + 4 + numClasses + w]);
                }
                else
                {
                    box.cx = outputData[0 * numAnchors + a];
                    box.cy = outputData[1 * numAnchors + a];
                    box.w  = outputData[2 * numAnchors + a];
                    box.h  = outputData[3 * numAnchors + a];

                    for (int w = 0; w < numMaskWeights; w++)
                        candWeights.Add(outputData[(4 + numClasses + w) * numAnchors + a]);
                }

                candidates.Add(box);
            }
        }

        while (candidates.Length > 0)
        {
            int maxIdx = 0;
            float maxScore = -1f;

            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].score > maxScore)
                {
                    maxScore = candidates[i].score;
                    maxIdx = i;
                }
            }

            BoxData bestBox = candidates[maxIdx];
            resultBoxes.Add(bestBox);
            for (int w = 0; w < numMaskWeights; w++)
                resultWeights.Add(candWeights[maxIdx * numMaskWeights + w]);

            float bMinX = bestBox.cx - bestBox.w / 2f;
            float bMinY = bestBox.cy - bestBox.h / 2f;
            float bMaxX = bestBox.cx + bestBox.w / 2f;
            float bMaxY = bestBox.cy + bestBox.h / 2f;
            float bArea = bestBox.w * bestBox.h;

            // 역방향 순회: RemoveAtSwapBack 시 아직 미방문 인덱스만 교체됨
            for (int i = candidates.Length - 1; i >= 0; i--)
            {
                bool remove = (i == maxIdx);

                if (!remove && candidates[i].classID == bestBox.classID)
                {
                    BoxData cBox = candidates[i];
                    float cMinX = cBox.cx - cBox.w / 2f;
                    float cMinY = cBox.cy - cBox.h / 2f;
                    float cMaxX = cBox.cx + cBox.w / 2f;
                    float cMaxY = cBox.cy + cBox.h / 2f;
                    float cArea = cBox.w * cBox.h;

                    float interMinX = math.max(bMinX, cMinX);
                    float interMinY = math.max(bMinY, cMinY);
                    float interMaxX = math.min(bMaxX, cMaxX);
                    float interMaxY = math.min(bMaxY, cMaxY);

                    float interW = math.max(0, interMaxX - interMinX);
                    float interH = math.max(0, interMaxY - interMinY);
                    float interArea = interW * interH;

                    float unionArea = bArea + cArea - interArea;
                    float iou = unionArea > 0f ? interArea / unionArea : 0f;

                    remove = (iou >= iouThreshold);
                }

                if (remove)
                {
                    // candidates와 candWeights를 동시에 SwapBack 제거
                    int lastIdx = candidates.Length - 1;
                    if (i != lastIdx)
                    {
                        for (int w = 0; w < numMaskWeights; w++)
                            candWeights[i * numMaskWeights + w] = candWeights[lastIdx * numMaskWeights + w];
                    }
                    for (int w = 0; w < numMaskWeights; w++)
                        candWeights.RemoveAtSwapBack(candWeights.Length - 1);

                    candidates.RemoveAtSwapBack(i);
                }
            }
        }

        candidates.Dispose();
        candWeights.Dispose();
    }
}

[BurstCompile]
public struct MaskGenerationJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> prototypes;
    [ReadOnly] public NativeArray<BoxData> boxes;
    // 각 박스의 마스크 가중치: boxes[b]의 가중치는 maskWeights[b*numMaskWeights .. (b+1)*numMaskWeights-1]
    [ReadOnly] public NativeArray<float> maskWeights;

    // 객체별 binary mask: boxes[b]의 픽셀 (x,y) → perObjectMasks[b*maskRes*maskRes + y*maskRes + x]
    // 병렬 픽셀 루프 안에서 b축으로도 쓰므로 Safety System 경고 억제 필요
    [NativeDisableParallelForRestriction]
    public NativeArray<byte> perObjectMasks;

    public int maskRes;
    public int imgRes;
    public int numMaskWeights;

    public void Execute(int index)
    {
        if (boxes.Length == 0) return;

        int x = index % maskRes;
        int y = index / maskRes;

        float scale = (float)imgRes / maskRes;
        float imgX = x * scale;
        float imgY = y * scale;

        for (int b = 0; b < boxes.Length; b++)
        {
            var box = boxes[b];

            if (imgX < box.cx - box.w / 2f || imgX > box.cx + box.w / 2f ||
                imgY < box.cy - box.h / 2f || imgY > box.cy + box.h / 2f)
                continue;

            float maskVal = 0f;
            int weightBase = b * numMaskWeights;
            for (int p = 0; p < numMaskWeights; p++)
            {
                int protoIdx = p * (maskRes * maskRes) + y * maskRes + x;
                maskVal += maskWeights[weightBase + p] * prototypes[protoIdx];
            }

            float sigmoid = 1f / (1f + math.exp(-maskVal));

            if (sigmoid > 0.5f)
            {
                // Y축 반전 없이 이미지 좌표계 그대로 저장 (Marching Squares에서 사용)
                perObjectMasks[b * maskRes * maskRes + y * maskRes + x] = 1;
            }
        }
    }
}
