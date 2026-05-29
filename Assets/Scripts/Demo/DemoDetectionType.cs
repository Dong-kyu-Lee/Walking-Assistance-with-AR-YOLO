using System;
using System.Collections.Generic;

[Serializable]
public class DemoDetectionJson
{
    public string videoFile;
    public int videoWidth;
    public int videoHeight;
    public double fps;
    public int frameCount;
    public List<DemoFrameJson> frames = new();
}

[Serializable]
public class DemoFrameJson
{
    public int frame;
    public List<DemoObjectJson> objects = new();
}

[Serializable]
public class DemoObjectJson
{
    public string className;
    public int classId;
    public float score;
    public int priority;

    // 모델 입력 기준 bbox. 현재 RunYOLO는 640x640 기준이므로 정규화해서 저장.
    public DemoBoxJson box;

    // polygon은 [0, 1] 정규화 좌표로 저장.
    // 현재 polygonRenderer.ShowPolygon()에 넣는 canvasPoints와 같은 기준.
    public List<DemoPointJson> polygon = new();
}

[Serializable]
public class DemoBoxJson
{
    public float cx;
    public float cy;
    public float w;
    public float h;
}

[Serializable]
public class DemoPointJson
{
    public float x;
    public float y;
}