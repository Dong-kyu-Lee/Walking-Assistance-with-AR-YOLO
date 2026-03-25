using TMPro;
using UnityEngine;

[System.Serializable]
public struct ErrorDescriptions
{
    public GameObject ErrorIndicator;
    public string ErrorText;
}

public enum IndicatorType
{
    gps,
    aline,
    route,
    lineRenderer
}


public class PathFindingUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TMapSearcher _tmapSearcher;


    [Header("활성화 여부를 보여주는 UI")]
    [SerializeField] private GameObject _gpsErrorIndicator;
    [SerializeField] private GameObject _alineErrorIndicator;
    [SerializeField] private GameObject _routeErrorIndicator;
    [SerializeField] private GameObject _lineRenderedErrorIndicator;

    [Header("에러 메시지 출력 텍스트")]
    [SerializeField] private TextMeshProUGUI _gpsErrorText;
    [SerializeField] private TextMeshProUGUI _alineErrorText;
    [SerializeField] private TextMeshProUGUI _routeErrorText;
    [SerializeField] private TextMeshProUGUI _lineRenderErrorText;
    [SerializeField] private TextMeshProUGUI _coordinateText;
    [SerializeField] private TextMeshProUGUI _trueAngleText;

    #region Singleton
    private static PathFindingUI s_instance;
    public static PathFindingUI instance { get { return s_instance; } }

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        if (s_instance != null)
        {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
    }

    private void OnDestroy()
    {
        Dispose();
    }

    private void Dispose()
    {
        s_instance = null;
    }
    #endregion

    private void Start()
    {
        _gpsErrorIndicator.SetActive(true);
        _alineErrorIndicator.SetActive(true);
        ResetDebug();

    }

    private void ResetDebug()
    {
        _routeErrorIndicator.SetActive(true);
        _lineRenderedErrorIndicator.SetActive(true);
    }

    public void PrintCurrentCoordinate(double startlat, double startlon)
    {
        _coordinateText.text = $"현재 좌표 : 위도 {startlat}, 경도 {startlon}";
    }

    public void PrintInputTrueHeading(float angle, float cameraAngle)
    {
        _trueAngleText.text = $"북쪽으로부터 {angle}도, 현재 카메라 각도 {cameraAngle}도.";
    }

    public void SetDescription(string description)
    {
        _descriptionText.text = description;
    }

    public void OnClickFindPathBtn()
    {
        if (_tmapSearcher == null)
        {
            Debug.Log("경로값을 반환하는 스크립트가 없습니다.");
            _descriptionText.text = "경로값을 반환하는 스크립트가 없습니다.";
            return;
        }
        ResetDebug();
        _tmapSearcher.SearchPath(_inputField.text);
    }

    public void ShowText(string text, IndicatorType type)
    {
        switch (type)
        {
            case IndicatorType.gps:
                _gpsErrorText.text = text;
                break;
            case IndicatorType.aline:
                _alineErrorText.text = text;
                break;
            case IndicatorType.route:
                _routeErrorText.text = text;
                break;
            case IndicatorType.lineRenderer:
                _lineRenderErrorText.text = text;
                break;
            default:
                break;
        }

            

        _descriptionText.text = text;
    }

    public void ShowGPSErrorIndicator(bool isError)
    {
            _gpsErrorIndicator.SetActive(isError);
    }
    public void ShowAlignErrorIndicator(bool isError)
    {
            _alineErrorIndicator.SetActive(isError);
    }

    public void ShowRouteErrorIndicator(bool isError)
    {
            _routeErrorIndicator.SetActive(isError);
    }

    public void ShowLineRenderedErrorIndicator(bool isError)
    {
            _lineRenderedErrorIndicator.SetActive(isError);
    }

}
