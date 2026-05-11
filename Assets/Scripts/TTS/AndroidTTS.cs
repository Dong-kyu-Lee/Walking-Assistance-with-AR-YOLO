using System;
using UnityEngine;

public class AndroidTTS : MonoBehaviour
{
    private AndroidJavaObject activity;
    private AndroidJavaObject tts;
    private AndroidJavaClass ttsClass;

    private volatile bool initResultReceived = false;
    private volatile int initStatus = -1;

    private bool isReady = false;
    private bool isInitializing = false;

    private const string TTS_CLASS_NAME = "android.speech.tts.TextToSpeech";

    [SerializeField] private float speakCooldown = 2.0f;

    private float lastSpeakTime = -999f;

    private void Start()
    {
        InitTTS();
    }

    private void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (initResultReceived && tts != null)
        {
            initResultReceived = false;
            CompleteInit(initStatus);
        }
#endif
    }

    public void InitTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (tts != null || isInitializing)
            return;

        isInitializing = true;

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }

        ttsClass = new AndroidJavaClass(TTS_CLASS_NAME);

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            tts = new AndroidJavaObject(
                TTS_CLASS_NAME,
                activity,
                new TtsInitListener(this)
            );
        }));
#else
        Debug.Log("[AndroidTTS] Android 실제 기기에서만 TTS가 실행됩니다.");
#endif
    }

    private void OnInitFromAndroid(int status)
    {
        initStatus = status;
        initResultReceived = true;
    }

    private void CompleteInit(int status)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        isInitializing = false;

        int success = ttsClass.GetStatic<int>("SUCCESS");

        if (status != success)
        {
            Debug.LogError("[AndroidTTS] TTS 초기화 실패");
            isReady = false;
            return;
        }

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            using (AndroidJavaObject koreanLocale = new AndroidJavaObject("java.util.Locale", "ko", "KR"))
            {
                int languageResult = tts.Call<int>("setLanguage", koreanLocale);

                int missingData = ttsClass.GetStatic<int>("LANG_MISSING_DATA");
                int notSupported = ttsClass.GetStatic<int>("LANG_NOT_SUPPORTED");

                if (languageResult == missingData || languageResult == notSupported)
                {
                    Debug.LogError("[AndroidTTS] 한국어 TTS 데이터가 없거나 지원되지 않습니다.");
                    isReady = false;
                    return;
                }
            }

            tts.Call<int>("setSpeechRate", 1.0f);
            tts.Call<int>("setPitch", 1.0f);

            isReady = true;
            Debug.Log("[AndroidTTS] TTS 준비 완료");
        }));
#endif
    }

    public void Speak(string text)
    {
        if (Time.time - lastSpeakTime < speakCooldown)
            return;

        lastSpeakTime = Time.time;
        Speak(text, true);
    }

    public void Speak(string text, bool interruptCurrentSpeech)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isReady || tts == null)
        {
            Debug.LogWarning("[AndroidTTS] 아직 TTS가 준비되지 않았습니다.");
            return;
        }

        if (string.IsNullOrWhiteSpace(text))
            return;

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            int queueMode = interruptCurrentSpeech
                ? ttsClass.GetStatic<int>("QUEUE_FLUSH")
                : ttsClass.GetStatic<int>("QUEUE_ADD");

            using (AndroidJavaObject bundle = new AndroidJavaObject("android.os.Bundle"))
            {
                string utteranceId = $"tts_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

                int result = tts.Call<int>(
                    "speak",
                    text,
                    queueMode,
                    bundle,
                    utteranceId
                );

                int success = ttsClass.GetStatic<int>("SUCCESS");

                if (result != success)
                {
                    Debug.LogWarning("[AndroidTTS] speak 호출 실패: " + result);
                }
            }
        }));
#else
        Debug.Log("[Editor TTS] " + text);
#endif
    }

    public void Stop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (tts == null)
            return;

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            tts.Call<int>("stop");
        }));
#endif
    }

    public void Release()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (tts == null)
            return;

        activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
        {
            tts.Call<int>("stop");
            tts.Call("shutdown");
            tts.Dispose();
            tts = null;
            activity?.Dispose();
            activity = null;
            ttsClass?.Dispose();
            ttsClass = null;
            isReady = false;
        }));
#endif
    }

    private void OnDestroy()
    {
        Release();
    }

    private class TtsInitListener : AndroidJavaProxy
    {
        private readonly AndroidTTS owner;

        public TtsInitListener(AndroidTTS owner)
            : base("android.speech.tts.TextToSpeech$OnInitListener")
        {
            this.owner = owner;
        }

        public void onInit(int status)
        {
            owner.OnInitFromAndroid(status);
        }
    }
}
