using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using Sirenix.OdinInspector;

[ExecuteInEditMode]
public class GpuSkinAnimatorTest : MonoBehaviour
{
    public List<string> clipsName = new List<string>();

    //[ValueDropdown("clipsName")]
    public string curAnimType = "";

    string lastAnimType = "";
    private GPUSkinAnimator skinAnimator;
    public GameObject partsGO;
    public Material[] replaceMaterials;
    public Texture2D mainTexture;

    void Start()
    {
        skinAnimator = this.gameObject.GetComponent<GPUSkinAnimator>();
        if (!skinAnimator)
        {
            Debug.LogError($"该物体 {gameObject.name} 没有挂载GPUSkinAnimator脚本");
            return;
        }

        skinAnimator.Init();
        if (skinAnimator.anim != null)
        {
            var clips = skinAnimator.anim.clips;

            clipsName.Clear();
            foreach (var clip in clips)
                clipsName.Add(clip.name);

            if (clipsName.Count > 0)
            {
                lastAnimType = clipsName[0];
                skinAnimator.Play(clipsName[0]);
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyUp(KeyCode.K))
        {
            if (partsGO != null)
            {
                var obj = GameObject.Instantiate(partsGO);
                skinAnimator.ReplaceParts(obj, true);
            }
        }
        else if (Input.GetKeyUp(KeyCode.A))
        {
            if (replaceMaterials != null)
            {
                skinAnimator.ReplaceMaterials(replaceMaterials);
            }

            if(mainTexture != null)
                skinAnimator.SetModelMainTexture(mainTexture);
        }
        else if (Input.GetKeyUp(KeyCode.Q))
        {
            skinAnimator.RecoverMaterials();
        }

        if (lastAnimType == curAnimType)
            return;

        lastAnimType = curAnimType;
        skinAnimator.Play(curAnimType);
    }
}
