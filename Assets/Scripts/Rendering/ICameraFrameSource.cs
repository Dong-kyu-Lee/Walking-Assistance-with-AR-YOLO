using UnityEngine;

public interface ICameraFrameSource
{
    Texture FrameTexture { get; }
    int FrameWidth { get; }
    int FrameHeight { get; }
    bool HasFrame { get; }
}