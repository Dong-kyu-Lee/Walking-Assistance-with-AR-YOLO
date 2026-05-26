using TMPro;
using UnityEngine;

public class AmbientNoiseMeter : MonoBehaviour
{
    [SerializeField] private int sampleWindow = 1024;
    [SerializeField] private int sampleRate = 16000;

    private AudioClip micClip;
    private string micDevice;
    private float[] samples;

    [Header("Debug UI")]
    [SerializeField] private TextMeshProUGUI db_text;
    public float CurrentDb { get; private set; }

    private void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[NoiseMeter] 마이크 장치를 찾을 수 없습니다.");
            return;
        }

        micDevice = Microphone.devices[0];
        samples = new float[sampleWindow];

        micClip = Microphone.Start(
            micDevice,
            true,
            1,
            sampleRate
        );
    }

    private void Update()
    {
        if (micClip == null || !Microphone.IsRecording(micDevice))
            return;

        int micPosition = Microphone.GetPosition(micDevice);
        int startPosition = micPosition - sampleWindow;

        if (startPosition < 0)
            return;

        micClip.GetData(samples, startPosition);

        float sum = 0f;

        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }

        float rms = Mathf.Sqrt(sum / samples.Length);

        // 기준값이 없는 상대 dBFS에 가까운 값
        CurrentDb = 20f * Mathf.Log10(rms + 1e-7f);
        db_text.text = "DB: " + CurrentDb.ToString();
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(micDevice))
            Microphone.End(micDevice);
    }
}