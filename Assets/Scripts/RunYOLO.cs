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

    //Image size for the model
    private const int imageWidth = 640;
    private const int imageHeight = 640;

    List<GameObject> boxPool = new();

    [Tooltip("Intersection over union threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float iouThreshold = 0.5f;

    [Tooltip("Confidence score threshold used for non-maximum suppression")]
    [SerializeField, Range(0, 1)]
    float scoreThreshold = 0.5f;

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
        // 💡 [핵심 변경점] Sentis의 복잡한 그래프 Slicing 제거! 
        // 모델을 원본 그대로 로드하고 즉시 Worker에 할당합니다.
        var model = ModelLoader.Load(modelAsset);
        worker = new Worker(model, backend);
        Debug.Log("모델 로드");
    }

    void OnDestroy()
    {
        worker?.Dispose();
        inputTensor?.Dispose();
    }

    public IEnumerator ExecuteML(Texture sourceTexture)
    {
        ClearAnnotations();

        if (sourceTexture == null) yield break;

        Graphics.Blit(sourceTexture, targetRT, new Vector2(1, -1), new Vector2(0, 1));
        if (!isARMode) displayImage.texture = targetRT;

        TextureConverter.ToTensor(targetRT, inputTensor, default);
        worker.Schedule(inputTensor);

        // 1. 단 하나의 원본 출력 텐서만 가져오기
        var outputTensor = worker.PeekOutput() as Tensor<float>;
        outputTensor.ReadbackRequest();

        while (!outputTensor.IsReadbackRequestDone())
        {
            yield return null;
        }

        // 2. 모델의 형태(Shape)를 동적으로 파악
        // (YOLO 모델에 따라 [1, 84, 8400] 형태일 수도, [1, 8400, 84] 형태일 수도 있음)
        var shape = outputTensor.shape;
        int dim1 = shape.rank > 1 ? shape[shape.rank - 2] : 1;
        int dim2 = shape.rank > 0 ? shape[shape.rank - 1] : 1;

        // 더 긴 쪽이 앵커(박스) 개수, 짧은 쪽이 속성(4개 좌표 + N개 클래스)
        int numAnchors = math.max(dim1, dim2);
        int numAttributes = math.min(dim1, dim2);
        int numClasses = numAttributes - 4;
        bool isTransposed = (dim1 == numAnchors);

        // 3. 네이티브 배열 다운로드
        var outputData = outputTensor.DownloadToNativeArray();

        // Job에서 파싱된 결과를 바로 담을 리스트 (좌표, 점수, 클래스ID가 하나로 통합됨)
        var resultBoxes = new NativeList<BoxData>(Allocator.TempJob);

        try
        {
            var nmsJob = new NMSJob
            {
                outputData = outputData,
                numAnchors = numAnchors,
                numClasses = numClasses,
                isTransposed = isTransposed,
                iouThreshold = iouThreshold,
                scoreThreshold = scoreThreshold,
                resultBoxes = resultBoxes
            };

            nmsJob.Schedule().Complete();

            ProcessResults(resultBoxes);
        }
        finally
        {
            // 메모리 누수 완벽 방지
            if (resultBoxes.IsCreated) resultBoxes.Dispose();
            if (outputData.IsCreated) outputData.Dispose();
        }
    }

    private void ProcessResults(NativeList<BoxData> resultBoxes)
    {
        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;
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

// Job System 전용 경계 상자 구조체
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
    public bool isTransposed;

    public float iouThreshold;
    public float scoreThreshold;

    public NativeList<BoxData> resultBoxes;

    public void Execute()
    {
        // 1. 임계치를 넘는 후보군 추출
        NativeList<BoxData> candidates = new NativeList<BoxData>(numAnchors, Allocator.Temp);

        for (int a = 0; a < numAnchors; a++)
        {
            float maxScore = -1f;
            int bestClassID = -1;

            // 각 앵커에 대해 가장 높은 클래스 점수 찾기
            for (int c = 0; c < numClasses; c++)
            {
                // 데이터 배열이 가로로 긴지 세로로 긴지에 따라 인덱스 접근 방식 변경
                float score = isTransposed
                    ? outputData[a * (numClasses + 4) + (c + 4)]
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

                // 좌표 데이터 추출 (첫 4개 원소)
                if (isTransposed)
                {
                    int baseIdx = a * (numClasses + 4);
                    box.cx = outputData[baseIdx + 0];
                    box.cy = outputData[baseIdx + 1];
                    box.w = outputData[baseIdx + 2];
                    box.h = outputData[baseIdx + 3];
                }
                else
                {
                    box.cx = outputData[0 * numAnchors + a];
                    box.cy = outputData[1 * numAnchors + a];
                    box.w = outputData[2 * numAnchors + a];
                    box.h = outputData[3 * numAnchors + a];
                }

                candidates.Add(box);
            }
        }

        // 2. NMS (비최대 억제) 루프 실행
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

            float bMinX = bestBox.cx - bestBox.w / 2f;
            float bMinY = bestBox.cy - bestBox.h / 2f;
            float bMaxX = bestBox.cx + bestBox.w / 2f;
            float bMaxY = bestBox.cy + bestBox.h / 2f;
            float bArea = bestBox.w * bestBox.h;

            for (int i = candidates.Length - 1; i >= 0; i--)
            {
                if (i == maxIdx)
                {
                    candidates.RemoveAtSwapBack(i);
                    continue;
                }

                BoxData cBox = candidates[i];
                if (cBox.classID != bestBox.classID) continue;

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

                if (iou >= iouThreshold)
                {
                    candidates.RemoveAtSwapBack(i);
                }
            }
        }

        candidates.Dispose();
    }
}