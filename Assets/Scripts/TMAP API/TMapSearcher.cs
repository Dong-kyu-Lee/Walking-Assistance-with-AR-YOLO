using System;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.UI;
using TMPro;
// JSON 파싱을 위한 데이터 구조 (필요한 것만 정의)
[Serializable]
public class TmapPoiResponse { public SearchPoiInfo searchPoiInfo; }
[Serializable]
public class SearchPoiInfo { public Pois pois; }
[Serializable]
public class Pois { public Poi[] poi; }
[Serializable]
public class Poi
{
    public string name;
    public string frontLat; // 위도
    public string frontLon; // 경도
}

public class TMapSearcher : MonoBehaviour
{

    [SerializeField] private GPSManager _gpsManager;

    [Header("Debug Value")]
    [SerializeField] private string _originName;
    [SerializeField] private string _destName;

    private bool _enabled = false;

    private string startX;
    private string startY;
    private string endX;
    private string endY;

    private float _startTime;
    private float _endTime;


    [SerializeField] private TextReader _reader;
    [SerializeField] private TMapRouterDrawing _tMapRouterDrawing;
    private Vector3Converter _vector3Converter = new Vector3Converter();
    private Transform _mainCamera;

    private string _apiKey;

    private void Start()
    {
        _mainCamera = Camera.main.transform;
    }

    public void SearchPath(string destText)
    {
        _apiKey = _reader.ApiKey;
        if (_apiKey != null)
            StartCoroutine(SearchPlace(destText));
    }

    IEnumerator SearchPlaceDebug(string startKeyword, bool isStart)
    {
        // 1. URL 구성 (입력받은 키워드를 인코딩하여 포함)
        string encodedKeyword = UnityWebRequest.EscapeURL(startKeyword);
        string url = $"https://apis.openapi.sk.com/tmap/pois?version=1&searchKeyword={encodedKeyword}&count=5&resCoordType=WGS84GEO&reqCoordType=WGS84GEO";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            // 2. 헤더에 AppKey 설정
            www.SetRequestHeader("appKey", _apiKey);
            www.SetRequestHeader("Accept", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // 3. 응답받은 JSON 파싱
                TmapPoiResponse response = JsonUtility.FromJson<TmapPoiResponse>(www.downloadHandler.text);

                if (response?.searchPoiInfo?.pois?.poi?.Length > 0)
                {
                    // 가장 첫 번째 결과 가져오기
                    var topResult = response.searchPoiInfo.pois.poi[0];
                    Debug.Log($"검색 성공! 이름: {topResult.name}, 좌표: {topResult.frontLat}, {topResult.frontLon}");
                    //_nameText.text = topResult.name;
                    //_printText.text = $" N: {topResult.frontLat}\n E: {topResult.frontLon}";

                    // TODO: 여기서 받아온 좌표를 '보행자 경로 안내 API'의 목적지로 전달!

                    if (isStart)
                    {
                        startX = topResult.frontLon;
                        startY = topResult.frontLat;
                    }
                    else
                    {
                        endX = topResult.frontLon;
                        endY = topResult.frontLat;
                    }
                }
            }
            else
            {
                Debug.LogError("검색 에러: " + www.error);
            }
        }
    }

    IEnumerator SearchPlace(string startKeyword)
    {
        // 1. URL 구성 (입력받은 키워드를 인코딩하여 포함)
        string encodedKeyword = UnityWebRequest.EscapeURL(startKeyword);
        string url = $"https://apis.openapi.sk.com/tmap/pois?version=1&searchKeyword={encodedKeyword}&count=5&resCoordType=WGS84GEO&reqCoordType=WGS84GEO";

        using (UnityWebRequest www = UnityWebRequest.Get(url))
        {
            // 2. 헤더에 AppKey 설정
            www.SetRequestHeader("appKey", _apiKey);
            www.SetRequestHeader("Accept", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                // 3. 응답받은 JSON 파싱
                TmapPoiResponse response = JsonUtility.FromJson<TmapPoiResponse>(www.downloadHandler.text);

                if (response?.searchPoiInfo?.pois?.poi?.Length > 0)
                {
                    // 가장 첫 번째 결과 가져오기
                    var topResult = response.searchPoiInfo.pois.poi[0];
                    Debug.Log($"검색 성공! 이름: {topResult.name}, 좌표: {topResult.frontLat}, {topResult.frontLon}");
                    //_nameText.text = topResult.name;
                    //_printText.text = $" N: {topResult.frontLat}\n E: {topResult.frontLon}";

                    // TODO: 여기서 받아온 좌표를 '보행자 경로 안내 API'의 목적지로 전달!

                    endX = topResult.frontLon;
                    endY = topResult.frontLat;

                    StartCoroutine(GetRoute());
                }
            }
            else
            {
                Debug.LogError("검색 에러: " + www.error);
                PathFindingUI.instance.ShowRouteErrorIndicator(true);
            }
        }
    }

    public IEnumerator GetRoute()
    {
        if (_gpsManager == null) yield break;

        _enabled = false;

        string url = "https://apis.openapi.sk.com/tmap/routes/pedestrian?version=1&format=json";

        Vector2D startCoordinate = new Vector2D();
        if (double.TryParse(startX, out double resultX) && double.TryParse(startY, out double resultZ))
        {
            startCoordinate = _vector3Converter.GetCurrentCameraGPS(resultX, resultZ, _mainCamera.position.x, _mainCamera.position.z);
        }

        WWWForm form = new WWWForm();
        //form.AddField("startX", startCoordinate.latitude.ToString());
        //form.AddField("startY", startCoordinate.longitude.ToString());
        form.AddField("startX", _gpsManager.currentLon.ToString());
        form.AddField("startY", _gpsManager.currentLat.ToString());
        form.AddField("endX", endX);
        form.AddField("endY", endY);
        form.AddField("startName", "출발지");
        form.AddField("endName", "목적지");
        form.AddField("reqCoordType", "WGS84GEO");
        form.AddField("resCoordType", "WGS84GEO");

        // UnityWebRequest.Post 를 사용합니다!
        using (UnityWebRequest www = UnityWebRequest.Post(url, form))
        {
            www.SetRequestHeader("appKey", _apiKey.Trim());

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("경로 수신 성공!");
                Debug.Log(www.downloadHandler.text);

                PathFindingUI.instance.ShowRouteErrorIndicator(false);

                // 여기서 응답받은 JSON 텍스트(www.downloadHandler.text)를 파싱합니다.

                if (double.TryParse(_gpsManager.currentLon.ToString(), out double startLon) && double.TryParse(_gpsManager.currentLat.ToString(), out double startLat))
                {
                    _tMapRouterDrawing.ParseRouteData(www.downloadHandler.text, startLon, startLat);
                }
                
            }
            else
            {
                Debug.LogError("경로 수신 에러: " + www.error);
                Debug.LogError("상세 에러: " + www.downloadHandler.text);
                PathFindingUI.instance.ShowRouteErrorIndicator(true);
            }
        }
        _endTime = Time.unscaledTime;

        Debug.Log($"걸린 시간 : {_endTime - _startTime}");
    }
}
