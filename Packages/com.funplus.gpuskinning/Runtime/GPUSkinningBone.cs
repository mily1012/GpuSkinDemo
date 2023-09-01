using UnityEngine;
using System.Collections;

public class GPUSkinningBone
{
	[System.NonSerialized]
	public Transform transform = null;

	[System.NonSerialized]
	public Matrix4x4 bindpose;

    [System.NonSerialized]
    public bool missed;
}
