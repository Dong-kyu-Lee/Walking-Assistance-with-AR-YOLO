using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR.ARCore;
using System.Collections;

[System.Serializable]
public class ApiConfig
{
    public string tmapApiKey;
}
public class TextReader : MonoBehaviour
{
    public string ApiKey;

    void Start()
    {
        // 파일 읽기는 비동기로 처리하는 것이 안전합니다.
        StartCoroutine(LoadConfigCoroutine());
    }

    IEnumerator LoadConfigCoroutine()
    {
        // StreamingAssets 폴더의 경로를 가져옵니다. (플랫폼마다 경로가 다름)
        string filePath = Path.Combine(Application.streamingAssetsPath, "config.json");
        Debug.Log(filePath);
        string jsonString = "";

        // 안드로이드 환경이거나 URL 형태인 경우 WebRequest를 사용해야 함
        if (filePath.Contains("://") || filePath.Contains(":///"))
        {
            using (UnityWebRequest www = UnityWebRequest.Get(filePath))
            {
                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.Success)
                {
                    jsonString = www.downloadHandler.text;
                }
                else
                {
                    Debug.LogError("설정 파일을 읽어오지 못했습니다: " + www.error);
                }
            }
        }
        else // PC(에디터), iOS 환경에서는 일반 파일처럼 읽을 수 있음
        {
            if (File.Exists(filePath))
            {
                jsonString = File.ReadAllText(filePath);
            }
            else
            {
                Debug.LogError("config.json 파일이 존재하지 않습니다.");
            }
        }

        // 텍스트(JSON)를 파싱해서 변수에 쏙 담아주기
        if (!string.IsNullOrEmpty(jsonString))
        {
            ApiConfig config = JsonUtility.FromJson<ApiConfig>(jsonString);
            ApiKey = config.tmapApiKey;

            Debug.Log("TMAP API 키 로드 성공!"); // 확인용 (실제 빌드 시엔 키를 로그로 찍지 마세요)
            Debug.Log(ApiKey);

            // ⬇️ 이 아래부터 보행 보조 경로 탐색 등 본격적인 TMAP API 호출을 시작하면 됩니다.
            // RequestTmapRoute(); 
        }
    }
}
