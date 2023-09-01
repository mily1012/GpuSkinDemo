using UnityEngine;
using System.Collections;

[System.Serializable]
public class GPUSkinningClip
{
    public string name = null;

    public float length = 0.0f;

    public int fps = 0;

    public float speed = 1;

    public GPUSkinningWrapMode wrapMode = GPUSkinningWrapMode.Once;

    public GPUSkinningFrame[] frames = null;

    public int pixelSegmentation = 0;

    public GPUSkinningAnimEvent[] events = null;

    public int FrameCount
    {
        get
        {
            return (int)(length * fps) - 1;
        }
    }

    public int GetFrameIndex(float time)
    {
        return (int) (time * fps) % (int) (length * fps);
    }
}
