using UnityEngine;
using System.Collections;

public class GPUSkinningAnimation : ScriptableObject
{
    public string guid = null;

#if  UNITY_EDITOR
    public GPUSkinningBone[] _bones = null;
#endif

    public GPUSkinningBone[] bones
    {
        get
        {
#if UNITY_EDITOR
            return _bones;
#else
            return null;
#endif
        }

        set
        {
#if UNITY_EDITOR
            _bones = value;
            boneLenght = _bones.Length;
#endif
        }
    }

    public int boneLenght = 0;

    public GPUSkinningClip[] clips = null;

    public Bounds bounds;

    public int textureWidth = 0;

    public int textureHeight = 0;

    public float sphereRadius = 1.0f;
}
