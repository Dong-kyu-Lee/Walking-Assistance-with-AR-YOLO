using System;
using System.Collections.Generic;
using System.IO;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/*
 *  YOLO Inference Script
 *  ========================
 *
 * Place this script on the Main Camera and set the script parameters according to the tooltips.
 *
 */

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

    const BackendType backend = BackendType.GPUCompute;

    private Transform displayLocation;
    private Worker worker;
    private string[] labels;
    private RenderTexture targetRT;
    private Sprite borderSprite;

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

    Tensor<float> centersToCorners;
    //bounding box data
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
        if (!isARMode)
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }

        //Parse neural net labels
        labels = classesAsset.text.Split('\n');

        LoadModel();

        targetRT = new RenderTexture(imageWidth, imageHeight, 0);

        //Create image to display video
        displayLocation = displayImage.transform;

        borderSprite = Sprite.Create(borderTexture, new Rect(0, 0, borderTexture.width, borderTexture.height), new Vector2(borderTexture.width / 2, borderTexture.height / 2));
    }
    void LoadModel()
    {
        //Load model
        // 1. 에디터에서 할당한 .onnx 파일을 Sentis가 이해할 수 있는 모델 객체로 메모리에 올립니다.
        var model1 = ModelLoader.Load(modelAsset);

        // 2. YOLO의 중심점 기반 좌표(Center X, Y, W, H)를 사각형의 모서리 좌표(Min X, Y, Max X, Y)로 
        // 변환하기 위한 수학적 계산용 행렬(Tensor)을 만듭니다. (NMS 연산에 필요)
        centersToCorners = new Tensor<float>(new TensorShape(4, 4),
        new float[]
        {
                1,      0,      1,      0,
                0,      1,      0,      1,
                -0.5f,  0,      0.5f,   0,
                0,      -0.5f,  0,      0.5f
        });

        //Here we transform the output of the model1 by feeding it through a Non-Max-Suppression layer.
        // 3. 기존 모델에 새로운 연산 기능을 추가하기 위한 '작업 그래프'를 생성합니다.
        var graph = new FunctionalGraph();
        // 4. 원본 모델(model1)의 입력 계층을 그래프에 등록합니다.
        var inputs = graph.AddInputs(model1);
        // 5. 원본 모델에 데이터를 흘려보냈을 때 나오는 첫 번째 결과물(Raw Output)을 정의합니다.
        // YOLOv8n의 경우 보통 (1, 84, 8400) 형태의 데이터가 나옵니다.
        var modelOutput = Functional.Forward(model1, inputs)[0];                        //shape=(1,84,8400)
        // 6. 데이터 분리: 84개의 정보 중 앞의 4개(0~3)는 박스 좌표(Box Coords)입니다.
        var boxCoords = modelOutput[0, 0..4, ..].Transpose(0, 1);                       //shape=(8400,4)
        // 7. 데이터 분리: 나머지 80개(4~83)는 각 클래스(사물 종류)에 대한 확률 점수입니다.
        var allScores = modelOutput[0, 4.., ..];                                        //shape=(80,8400)
        // 8. 가장 높은 점수를 찾고, 그 점수가 어떤 클래스(ID)인지 계산합니다.
        var scores = Functional.ReduceMax(allScores, 0);                                //shape=(8400)
        var classIDs = Functional.ArgMax(allScores, 0);                                 //shape=(8400)

        // 비최대 억제(NMS) 적용
        // 9. 앞서 만든 행렬(centersToCorners)을 곱해 중심점 좌표를 모서리 좌표로 변환합니다. (NMS 함수의 요구 조건)
        var boxCorners = Functional.MatMul(boxCoords, Functional.Constant(centersToCorners));   //shape=(8400,4)
        // 10. Sentis의 NMS 기능을 사용하여 중복된 박스를 제거하고 가장 확실한 박스의 인덱스만 남깁니다.
        // 설정한 iouThreshold와 scoreThreshold가 여기서 사용됩니다.
        var indices = Functional.NMS(boxCorners, scores, iouThreshold, scoreThreshold); //shape=(N)
        // 11. 최종 선택된 인덱스에 해당하는 좌표(coords)와 클래스 ID(labelIDs)만 골라냅니다.
        var coords = Functional.IndexSelect(boxCoords, 0, indices);                     //shape=(N,4)
        var labelIDs = Functional.IndexSelect(classIDs, 0, indices);                    //shape=(N)

        //Create worker to run model
        // 12. 지금까지 설계한 '개조된 그래프'를 컴파일하여 실제 실행 가능한 형태로 만듭니다.
        // 결과적으로 이 모델은 영상 입력 시 [박스 좌표들, 클래스 ID들] 두 가지를 딱 내뱉게 됩니다.
        worker = new Worker(graph.Compile(coords, labelIDs), backend);
    }


    // 오버로딩: 외부(AR)에서 텍스처를 받아 실행
    public void ExecuteML(Texture sourceTexture)
    {
        ClearAnnotations(); // 1. 이전 프레임에서 그려진 박스들을 모두 비활성화하여 화면을 깨끗이 비웁니다.

            if (sourceTexture)
            {
                Graphics.Blit(sourceTexture, targetRT);

                // AR 모드가 아닐 때만 디버그용으로 화면에 텍스처를 띄웁니다. 
                // AR일 때는 실제 카메라 화면(ARBackground) 위에 박스만 그려야 하므로 텍스처를 덮어씌우지 않습니다.
                if (!isARMode)
                {
                    displayImage.texture = targetRT;
                }
            }
            else return; // 비디오 준비가 안 됐다면 함수를 종료합니다.

        RunInference();
    }

    private void RunInference()
    {

        // 6. Sentis 모델에 입력할 텐서를 생성합니다. 크기는 1개 배치, 3채널(RGB), 640x640입니다.
        using Tensor<float> inputTensor = new Tensor<float>(new TensorShape(1, 3, imageHeight, imageWidth));
        // 7. 유니티의 RenderTexture(targetRT) 데이터를 방금 만든 텐서(inputTensor) 형식으로 변환하여 채웁니다.
        TextureConverter.ToTensor(targetRT, inputTensor, default);

        worker.Schedule(inputTensor); // 8. Worker(추론 엔진)에 입력 데이터를 전달하고 GPU 연산을 시작합니다.

        // 9. 모델의 첫 번째 출력(박스 좌표 정보)을 가져와 CPU에서 읽을 수 있게 복사본을 만듭니다.
        using var output = (worker.PeekOutput("output_0") as Tensor<float>).ReadbackAndClone();
        // 10. 모델의 두 번째 출력(객체의 클래스 ID, 예: '사람', '기린')을 가져와 복사본을 만듭니다.
        using var labelIDs = (worker.PeekOutput("output_1") as Tensor<int>).ReadbackAndClone();

        // 11. 현재 화면 UI(RawImage)의 실제 가로/세로 길이를 구합니다.
        float displayWidth = displayImage.rectTransform.rect.width;
        float displayHeight = displayImage.rectTransform.rect.height;

        // 수정: 이미지를 강제로 늘려서(Stretch) 입력했으므로, 출력 좌표도 화면 크기에 맞춰 단순 비례식으로 복원하면 됩니다.
        float scaleX = displayWidth / (float)imageWidth;
        float scaleY = displayHeight / (float)imageHeight;

        int boxesFound = output.shape[0]; // 13. 모델이 최종적으로 찾아낸 객체(박스)의 개수입니다.

        // 14. 찾아낸 박스들을 루프를 돌며 화면에 그립니다. (최대 200개 제한)
        for (int n = 0; n < Mathf.Min(boxesFound, 200); n++)
        {
            var box = new BoundingBox
            {
                // 15. 좌표 변환: 모델 좌표에 스케일을 곱하고, 유니티 UI 중심점(Center) 기준으로 위치를 보정합니다.
                centerX = output[n, 0] * scaleX - displayWidth / 2,
                centerY = output[n, 1] * scaleY - displayHeight / 2,
                width = output[n, 2] * scaleX,
                height = output[n, 3] * scaleY,
                label = labels[labelIDs[n]], // 16. 클래스 ID 번호를 이용해 실제 이름(예: "giraffe")을 가져옵니다.
            };
            // 17. 최종적으로 계산된 box 데이터를 이용해 화면에 사각형과 텍스트를 실제로 그립니다.
            DrawBox(box, n, displayHeight * 0.05f);
        }
    }

    public void DrawBox(BoundingBox box, int id, float fontSize)
    {
        //Create the bounding box graphic or get from pool
        GameObject panel;
        if (id < boxPool.Count)
        {
            panel = boxPool[id];
            panel.SetActive(true);
        }
        else
        {
            panel = CreateNewBox(Color.yellow);
        }
        //Set box position
        panel.transform.localPosition = new Vector3(box.centerX, -box.centerY);

        //Set box size
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(box.width, box.height);

        //Set label text
        var label = panel.GetComponentInChildren<Text>();
        label.text = box.label;
        label.fontSize = (int)fontSize;
    }

    public GameObject CreateNewBox(Color color)
    {
        //Create the box and set image

        var panel = new GameObject("ObjectBox");
        panel.AddComponent<CanvasRenderer>();
        Image img = panel.AddComponent<Image>();
        img.color = color;
        img.sprite = borderSprite;
        img.type = Image.Type.Sliced;
        panel.transform.SetParent(displayLocation, false);

        //Create the label

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
        foreach (var box in boxPool)
        {
            box.SetActive(false);
        }
    }

    void OnDestroy()
    {
        centersToCorners?.Dispose();
        worker?.Dispose();
    }
}