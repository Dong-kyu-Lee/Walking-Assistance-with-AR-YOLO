using UnityEngine;

public class EditorCameraControl : MonoBehaviour
{
#if UNITY_EDITOR // 에디터에서만 작동하도록 제한
    public float sensitivity = 2.0f;
    private float rotationX = 0;
    private float rotationY = 0;

    void Update()
    {
        // 마우스 우클릭을 누른 상태에서만 화면 회전
        if (Input.GetMouseButton(1))
        {
            rotationY += Input.GetAxis("Mouse X") * sensitivity;
            rotationX -= Input.GetAxis("Mouse Y") * sensitivity;
            rotationX = Mathf.Clamp(rotationX, -90, 90); // 위아래 회전 제한

            transform.eulerAngles = new Vector3(rotationX, rotationY, 0);
        }
    }
#endif
}
