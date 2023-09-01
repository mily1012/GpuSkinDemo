using UnityEngine;
using System.Collections;

[System.Serializable]
public class GPUSkinningFrame
{
    #if UNITY_EDITOR
    public Matrix4x4[] matrices = null;
    #endif
}
