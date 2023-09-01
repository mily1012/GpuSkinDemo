using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationTextureDescAsset : ScriptableObject
{
    [SerializeField]
    public AnimationTextureDesc desc;

    [SerializeField]
    public string parseError;
}