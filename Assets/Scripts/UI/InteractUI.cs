using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class InteractUI : MonoBehaviour
{
    [SerializeField] Button leftButton;
    [SerializeField] Button rightButton;
    [SerializeField] float interval = 0.5f;
    private float leftButtonTimer = 0f;
    private float rightButtonTimer = 0f;
    private int leftClickCount = 0;
    private int rightClickCount = 0;

    private string debugText = "";

    void Start()
    {
        leftButton.onClick.AddListener(OnLeftButtonClicked);
        rightButton.onClick.AddListener(OnRightButtonClicked);
    }

    void Update()
    {
        // 좌측 버튼 타이머 감소
        if (leftButtonTimer > 0)
        {
            leftButtonTimer -= Time.deltaTime;
        }
        else if (leftClickCount > 0)
        {
            leftClickCount = 0;
        }

        // 우측 버튼 타이머 감소
        if (rightButtonTimer > 0)
        {
            rightButtonTimer -= Time.deltaTime;
        }
        else if (rightClickCount > 0)
        {
            rightClickCount = 0;
        }
    }

    private void OnLeftButtonClicked()
    {
        leftClickCount++;
        leftButtonTimer = interval;

        if (leftClickCount == 1)
        {
            // 싱글 클릭 감지
        }
        else if (leftClickCount == 2)
        {
            // 더블 클릭 감지
            OnLeftButtonDoubleClicked();
            leftClickCount = 0;
        }
    }

    private void OnRightButtonClicked()
    {
        rightClickCount++;
        rightButtonTimer = interval;

        if (rightClickCount == 1)
        {
            // 싱글 클릭 감지
        }
        else if (rightClickCount == 2)
        {
            // 더블 클릭 감지
            OnRightButtonDoubleClicked();
            rightClickCount = 0;
        }
    }

    // AR 길찾기 씬으로 이동
    private void OnLeftButtonDoubleClicked()
    {
        Debug.Log("Left Button Double Clicked");
        debugText = "Left Button Double Clicked";
        // 좌측 버튼 더블 클릭 시 실행할 로직을 여기에 작성해주세요
        if(SceneManager.GetActiveScene().name != "ARScene")
        {
            SceneManager.LoadScene("ARScene");
        }
        else {
            Debug.Log("이미 ARScene에 있습니다.");
        }
    }

    // 시각 개선 씬으로 이동
    private void OnRightButtonDoubleClicked()
    {
        Debug.Log("Right Button Double Clicked");
        debugText = "Right Button Double Clicked";
        // 우측 버튼 더블 클릭 시 실행할 로직을 여기에 작성해주세요
        if(SceneManager.GetActiveScene().name != "CameraXScene")
        {
            SceneManager.LoadScene("CameraXScene");
        }
        else {
            Debug.Log("이미 CameraXScene에 있습니다.");
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 500, 100), debugText);
    }
}
