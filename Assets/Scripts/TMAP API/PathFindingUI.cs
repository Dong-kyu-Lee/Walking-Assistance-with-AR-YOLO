using TMPro;
using UnityEngine;

public class PathFindingUI : MonoBehaviour
{
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private TMapSearcher _tmapSearcher;

    [Header("활성화 여부를 보여주는 UI")]
    [SerializeField] private GameObject _isGpsOn;
    [SerializeField] private GameObject _isAlign;
    [SerializeField] private GameObject _isGetRoute;
    [SerializeField] private GameObject _isLineRendered;

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
            Destroy(this);
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
        _isGpsOn.SetActive(true);
        _isAlign.SetActive(true);
        ResetDebug();
    }

    private void ResetDebug()
    {
        _isGetRoute.SetActive(true);
        _isLineRendered.SetActive(true);
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

    public void ShowIsGPSOn(bool isSuccess) => _isGpsOn.SetActive(!isSuccess);
    public void ShowIsAlign(bool isSuccess) => _isAlign.SetActive(!isSuccess);
    public void ShowIsGetRoute(bool isSuccess) => _isGetRoute.SetActive(!isSuccess);
    public void ShowIsLineRendered(bool isSuccess) => _isLineRendered.SetActive(!isSuccess);
}
