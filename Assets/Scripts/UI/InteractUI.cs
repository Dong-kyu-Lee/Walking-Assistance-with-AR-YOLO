using UnityEngine;
using UnityEngine.UI;

public class InteractUI : MonoBehaviour
{
    [SerializeField] Button leftButton;
    [SerializeField] Button rightButton;
    [SerializeField] float interval = 0.5f;
    private float leftButtonTimer = 0f;
    private float rightButtonTimer = 0f;

    void Start()
    {
        
    }

    public void OnLeftButtonClick()
    {

    }
}
