using UnityEngine;

public class Step2BackgroundTextureProvider : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private Texture sourceTexture;

    [Header("Target Material")]
    [SerializeField] private Material backgroundMaterial;

    [Header("Debug")]
    [SerializeField] private bool assignOnEnableOnly = false;
    [SerializeField] private bool verboseLog = true;

    private static readonly int BackgroundTexId = Shader.PropertyToID("_Step2BackgroundTex");
    private static readonly int DebugModeId = Shader.PropertyToID("_DebugMode");

    private void OnEnable()
    {
        AssignTexture();
    }

    private void LateUpdate()
    {
        if (!assignOnEnableOnly)
            AssignTexture();
    }

    [ContextMenu("Assign Texture Now")]
    public void AssignTexture()
    {
        if (backgroundMaterial == null)
        {
            if (verboseLog)
                Debug.LogWarning("[Step2BackgroundTextureProvider] backgroundMaterial이 비어 있습니다.");
            return;
        }

        Texture texToAssign = sourceTexture != null ? sourceTexture : Texture2D.redTexture;

        backgroundMaterial.SetTexture(BackgroundTexId, texToAssign);

        if (verboseLog)
        {
            Texture assigned = backgroundMaterial.GetTexture(BackgroundTexId);
            Debug.Log(
                $"[Step2BackgroundTextureProvider] assigned = {(assigned != null ? assigned.name : "null")}, " +
                $"sameRef = {ReferenceEquals(texToAssign, assigned)}, " +
                $"debugMode = {backgroundMaterial.GetFloat(DebugModeId)}"
            );
        }
    }

    [ContextMenu("Set Debug Mode: Texture")]
    public void SetDebugTexture()
    {
        if (backgroundMaterial != null)
            backgroundMaterial.SetFloat(DebugModeId, 0f);
    }

    [ContextMenu("Set Debug Mode: UV")]
    public void SetDebugUV()
    {
        if (backgroundMaterial != null)
            backgroundMaterial.SetFloat(DebugModeId, 1f);
    }

    [ContextMenu("Set Debug Mode: Red")]
    public void SetDebugRed()
    {
        if (backgroundMaterial != null)
            backgroundMaterial.SetFloat(DebugModeId, 2f);
    }
}