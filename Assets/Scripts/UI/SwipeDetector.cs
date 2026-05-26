using UnityEngine;

public class SwipeDetector : MonoBehaviour
{
    [SerializeField] private VolumeKeyReceiver volumeKeyReceiver;


    [SerializeField] private float minSwipeDistance = 100f;

    private Vector2 touchStartPos;
    private Vector2 touchEndPos;

    void Update()
    {
        if (Input.touchCount <= 0) return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            touchStartPos = touch.position;
        }
        else if (touch.phase == TouchPhase.Ended)
        {
            touchEndPos = touch.position;
            DetectSwipe();
        }
    }

    private void DetectSwipe()
    {
        Vector2 swipeDelta = touchEndPos - touchStartPos;

        if (swipeDelta.magnitude < minSwipeDistance)
            return;

        if (Mathf.Abs(swipeDelta.x) > Mathf.Abs(swipeDelta.y))
        {
            if (swipeDelta.x > 0)
            {
                Debug.Log("오른쪽으로 스와이프");
                OnSwipeRight();
            }
            else
            {
                Debug.Log("왼쪽으로 스와이프");
                OnSwipeLeft();
            }
        }
        else
        {
            if (swipeDelta.y > 0)
            {
                Debug.Log("위로 스와이프");
                OnSwipeUp();
            }
            else
            {
                Debug.Log("아래로 스와이프");
                OnSwipeDown();
            }
        }
    }

    private void OnSwipeLeft()
    {
        volumeKeyReceiver.GetBeforeSetting();
    }

    private void OnSwipeRight()
    {
        volumeKeyReceiver.GetNextSetting();
    }

    private void OnSwipeUp()
    {
        // 위쪽 스와이프 처리
    }

    private void OnSwipeDown()
    {
        // 아래쪽 스와이프 처리
    }
}