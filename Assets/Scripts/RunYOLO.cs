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

    const BackendType backend = BackendType.CPU;

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
    [SerializeField] private PolygonOverlayRenderer polygonRenderer;

    [Tooltip("Intersection over union threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float iouThreshold = 0.45f;

    [Tooltip("Confidence score threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float scoreThreshold = 0.5f;

    [SerializeField] private RectTransform detectionOverlayRoot;

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
        Application.targetFrameRate = 60;
        if (!isARMode) Screen.orientation = ScreenOrientation.LandscapeLeft;

        labels = classesAsset.text.Split('\n');
        LoadModel();

        targetRT = new RenderTexture(imageWidth, imageHeight, 0);
        inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));

        displayLocation = displayImage.transform;
        borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, borderTexture.width, borderTexture.height), new Vector2(borderTexture.width / 2, borderTexture.height / 2));

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

    public IEnumerator ExecuteML(Texture sourceTexture)
    {
        //ClearAnnotations();

        if (sourceTexture == null) yield break;

        // UV 좌표의 Y축을 반전시켜 텍스처를 상하로 뒤집어 targetRT에 복사
        Graphics.Blit(sourceTexture, targetRT, new Vector2(1, -1), new Vector2(0, 1));

        TextureConverter.ToTensor(targetRT, inputTensor, default);

        worker.Schedule(inputTensor);

        var output0 = worker.PeekOutput("output0") as Tensor<float>;
        var output1 = worker.PeekOutput("output1") as Tensor<float>;

        // BackendType이 GPU일 경우, GPU->CPU로 결과 복사 
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

            polygonRenderer.ClearAll();

            int numBoxes = math.min(resultBoxes.Length, 200);
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

                    for (int i = 0; i < numBoxes; i++)
                    {
                        var contourPoints = MarchingSquaresUtil.GetContour(perObjectMasks, maskResolution, i);
                        if (contourPoints.Count < 3) continue;

                        // mask 좌표 → UILineRenderer scale=true 기준 정규화 좌표 [0,1]
                        // normY: Y축 반전 (maskY=0이 이미지 상단 → normY=1이 Canvas 상단)
                        var canvasPoints = new Vector2[contourPoints.Count];
                        for (int p = 0; p < contourPoints.Count; p++)
                        {
                            canvasPoints[p] = new Vector2(
                                contourPoints[p].x / maskResolution,
                                1.0f - contourPoints[p].y / maskResolution
                            );
                        }

                        polygonRenderer.ShowPolygon(i, canvasPoints, Color.green);
                    }
                }
                finally
                {
                    if (perObjectMasks.IsCreated) perObjectMasks.Dispose();
                }
            }

            ProcessResults(resultBoxes);

        }
        finally
        {
            if (resultBoxes.IsCreated) resultBoxes.Dispose();
            if (resultWeights.IsCreated) resultWeights.Dispose();
            if (outputData0.IsCreated) outputData0.Dispose();
            if (outputData1.IsCreated) outputData1.Dispose();
        }
    }

    private void ProcessResults(NativeList<BoxData> resultBoxes)
    {
        RectTransform targetRect = detectionOverlayRoot != null ?
            detectionOverlayRoot : displayImage.rectTransform;

        float displayWidth = targetRect.rect.width;
        float displayHeight = targetRect.rect.height;
        float scaleX = displayWidth / (float)imageWidth;
        float scaleY = displayHeight / (float)imageHeight;

        int boxesFound = math.min(resultBoxes.Length, 200);

        for (int i = 0; i < boxesFound; i++)
        {
            var b = resultBoxes[i];

            var box = new BoundingBox
            {
                centerX = b.cx * scaleX - displayWidth / 2,
                centerY = b.cy * scaleY - displayHeight / 2,
                width = b.w * scaleX,
                height = b.h * scaleY,
                label = (b.classID >= 0 && b.classID < labels.Length) ? labels[b.classID] : "Unknown",
            };
            DrawBox(box, i, displayHeight * 0.05f);
        }

        HideUnusedBoxes(boxesFound);
    }

    private void HideUnusedBoxes(int usedCount)
    {
        for (int i = usedCount; i < boxPool.Count; i++)
        {
            if (boxPool[i].activeSelf)
                boxPool[i].SetActive(false);
        }
    }

    public void DrawBox(BoundingBox box, int id, float fontSize)
    {
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else panel = CreateNewBox(Color.yellow);

        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);

        var label = panel.GetComponentInChildren<Text>();
        label.text = box.label;
        label.fontSize = (int)fontSize;
    }

    public GameObject CreateNewBox(Color color)
    {
        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.color = color;
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayLocation, false);

        var text = new GameObject("ObjectLabel");
        text.AddComponent<CanvasRenderer>();
        text.transform.SetParent(panel.transform, false);
        Text txt = text.AddComponent<Text>();
        txt.font = font;
        txt.color = color;
        txt.fontSize = 40;
        txt.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform rt2 = text.GetComponent<RectTransform>();
        rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
        rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
        rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
        rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
        rt2.anchorMin = new Vector2(0, 0);
        rt2.anchorMax = new Vector2(1, 1);

        boxPool.Add(panel);
        return panel;
    }

    public void ClearAnnotations()
    {
        foreach (var box in boxPool) box.SetActive(false);
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
