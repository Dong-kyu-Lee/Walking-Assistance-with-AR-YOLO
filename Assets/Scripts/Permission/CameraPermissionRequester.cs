using System.Collections;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

public class CameraPermissionRequester : MonoBehaviour
{
    public bool IsGranted { get; private set; }

    public IEnumerator RequestCameraPermission()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (Permission.HasUserAuthorizedPermission(Permission.Camera))
        {
            IsGranted = true;
            yield break;
        }

        bool isDone = false;
        bool granted = false;

        PermissionCallbacks callbacks = new PermissionCallbacks();

        callbacks.PermissionGranted += permissionName =>
        {
            granted = true;
            isDone = true;
        };

        callbacks.PermissionDenied += permissionName =>
        {
            granted = false;
            isDone = true;
        };

        callbacks.PermissionDeniedAndDontAskAgain += permissionName =>
        {
            granted = false;
            isDone = true;
        };

        Permission.RequestUserPermission(Permission.Camera, callbacks);

        yield return new WaitUntil(() => isDone);

        IsGranted = granted;
#else
        IsGranted = true;
        yield break;
#endif
    }
}