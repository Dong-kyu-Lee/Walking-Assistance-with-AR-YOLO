using System;
using TMPro;
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

    [Header("앰비언트 측정 필드")]
    [SerializeField] private AmbientNoiseMeter noiseMeter;

    [Header("실제 스마트폰 볼륨 조정")]
    private AndroidJavaObject audioManager;
    private int originalMediaVolume = -1;
    [SerializeField] private float volume = 0.5f;
    [SerializeField] private float pitch = 1f;
    public float Volume { get => volume; }
    public float Pitch { get => pitch; }

    [Header("DebugUI")]
    [SerializeField] private TextMeshProUGUI pitch_text;
    [SerializeField] private TextMeshProUGUI rate_text;
    [SerializeField] private TextMeshProUGUI volume_text;

    private void Start()
    {
        InitTTS();

#if UNITY_ANDROID && !UNITY_EDITOR
    Invoke(nameof(InitAudioVolume), 0.5f);
#endif
    }

    private void InitAudioVolume()
    {
        SaveCurrentMediaVolume();

        // 앱 실행 중 TTS가 잘 들리도록 미디어 볼륨을 80%로 설정
        SetMediaVolumeRatio(0.8f, false);
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

        using (AndroidJavaClass contextClass = new AndroidJavaClass("android.content.Context"))
        {
            string audioService = contextClass.GetStatic<string>("AUDIO_SERVICE");
            audioManager = activity.Call<AndroidJavaObject>("getSystemService", audioService);
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
            tts.Call<int>("setPitch", 0.8f);

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
        float volume1, pitch1, rate;

        if (noiseMeter.CurrentDb > -25f)        // 상대적으로 매우 시끄러움
        {
            volume1 = 0.4f;
            pitch1 = 0.5f;
        }
        else if (noiseMeter.CurrentDb > -40f)   // 보통 이상
        {
            volume1 = 0.4f;
            pitch1 = 0.5f;
        }
        else                       // 조용함
        {
            volume1 = 0.4f;
            pitch1 = 0.5f;
        }

        pitch_text.text = "pitch: " + pitch1.ToString();
        volume_text.text = "volume: " + volume1.ToString();

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

            tts.Call<int>("setPitch", pitch);

            using (AndroidJavaObject bundle = new AndroidJavaObject("android.os.Bundle"))
            {
                
                string utteranceId = $"tts_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";

                bundle.Call("putFloat", "volume", Mathf.Clamp01(volume));

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
        RestoreMediaVolume();
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

    // 앱 실행 시 볼륨을 일정 수치로 변경하는 메소드
    public void SetMediaVolumeRatio(float ratio, bool showUI = false)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    if (audioManager == null)
    {
        Debug.LogWarning("[AndroidTTS] AudioManager가 초기화되지 않았습니다.");
        return;
    }

    ratio = Mathf.Clamp01(ratio);

    using (AndroidJavaClass audioManagerClass = new AndroidJavaClass("android.media.AudioManager"))
    {
        int streamMusic = audioManagerClass.GetStatic<int>("STREAM_MUSIC");
        int flagShowUI = audioManagerClass.GetStatic<int>("FLAG_SHOW_UI");

        int maxVolume = audioManager.Call<int>("getStreamMaxVolume", streamMusic);
        int targetVolume = Mathf.RoundToInt(maxVolume * ratio);

        int flags = showUI ? flagShowUI : 0;

        audioManager.Call(
            "setStreamVolume",
            streamMusic,
            targetVolume,
            flags
        );

        Debug.Log($"[AndroidTTS] Media volume set: {targetVolume}/{maxVolume}");
    }
#else
        Debug.Log($"[Editor] SetMediaVolumeRatio: {ratio}");
#endif
    }

    // 현재 볼륨값을 저장하는 메소드
    public void SaveCurrentMediaVolume()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    if (audioManager == null)
        return;

    using (AndroidJavaClass audioManagerClass = new AndroidJavaClass("android.media.AudioManager"))
    {
        int streamMusic = audioManagerClass.GetStatic<int>("STREAM_MUSIC");
        originalMediaVolume = audioManager.Call<int>("getStreamVolume", streamMusic);
    }
#endif
    }

    // 저장한 볼륨값을 불러오는 메소드
    public void RestoreMediaVolume()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
    if (audioManager == null || originalMediaVolume < 0)
        return;

    using (AndroidJavaClass audioManagerClass = new AndroidJavaClass("android.media.AudioManager"))
    {
        int streamMusic = audioManagerClass.GetStatic<int>("STREAM_MUSIC");

        audioManager.Call(
            "setStreamVolume",
            streamMusic,
            originalMediaVolume,
            0
        );
    }
#endif
    }

    public void AddVolume()
    {
        volume = Mathf.Clamp01(volume + 0.1f);
        volume = Mathf.Round(volume * 10f) / 10f; // 소수점 첫째 자리까지 반올림
    }

    public void RemoveVolume()
    {
        volume = Mathf.Clamp01(volume - 0.1f);
        volume = Mathf.Round(volume * 10f) / 10f; // 소수점 첫째 자리까지 반올림
    }

    public void AddPitch()
    {
        pitch = Mathf.Clamp(pitch + 0.1f, 0.1f, 2f);
        pitch = Mathf.Round(pitch * 10f) / 10f; // 소수점 첫째 자리까지 반올림
    }

    public void RemovePitch()
    {
        pitch = Mathf.Clamp(pitch - 0.1f, 0.1f, 2f);
        pitch = Mathf.Round(pitch * 10f) / 10f; // 소수점 첫째 자리까지 반올림
    }

    public void ShowTestTTS() => Speak("테스트 음성 입니다.");
}
