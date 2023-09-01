using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteInEditMode]
public class GPUSkinAnimator : MonoBehaviour
{
    [SerializeField]
    public GPUSkinningAnimation anim = null;

    [SerializeField]
    public Mesh mesh = null;

    [SerializeField]
    public Material[] mtrl = null;

    [SerializeField]
    public Texture2D animTexture = null;

    private int defaultPlayingClipIndex = 0;

    public string curAnimName = "";


    [SerializeField]
    public GPUSKinningCullingMode cullingMode = GPUSKinningCullingMode.CullUpdateTransforms;

    private static GPUSkinningPlayerMonoManager playerManager = new GPUSkinningPlayerMonoManager();

    private GPUSkinningPlayer player = null;
    public bool Visible
    {
        get
        {
            if (player != null)
                return player.Visible;
            else
                return false;
        }

        set
        {
            if (player != null)
                player.Visible = value;
        }
    }

    public Vector3 Position
    {
        get
        {
            if (player != null)
                return player.Position;
            else
                return Vector3.zero;
        }
    }

    public void Init()
    {
        if(player != null)
        {
            return;
        }

        if (anim != null && mesh != null && mtrl != null && mtrl.Length > 0 && animTexture != null)
        {
            GPUSkinningPlayerResources res = null;

            if (Application.isPlaying)
            {
                playerManager.Register(anim, mesh, mtrl, animTexture, this, out res);
            }
            else
            {
                res = new GPUSkinningPlayerResources();
                res.Init(mesh, mtrl, animTexture, anim);
            }

            player = new GPUSkinningPlayer(gameObject, res);
            player.CullingMode = cullingMode;
        }
    }

    public void Play(string ani,Action finishCallBack = null)
    {
        curAnimName = ani;
        if(player != null)
            player.Play(ani,finishCallBack);
    }

    public void CrossFade(string ani, float fadeTime,Action finishCallBack = null)
    {
        curAnimName = ani;
        if(player != null)
            player.CrossFade(ani, fadeTime,finishCallBack);
    }

    public void ReplaceParts(GameObject partsGO, bool force)
    {
        if (player != null)
        {
            player.ReplaceParts(partsGO, force);
        }
    }
    public void ClearParts()
    {
        if (player != null)
        {
            player.ClearParts();
        }
    }

    public void SetPartsEnable(bool enable)
    {
        if (player != null)
        {
            player.SetPartsEnable(enable);
        }
    }

    public void ReplaceMaterials(Material[] materials)
    {
        if (player != null)
        {
            player.ReplaceMaterials(materials);
        }
    }

    public void RecoverMaterials()
    {
        if (player != null)
        {
            player.RecoverMaterials();
        }
    }

#if UNITY_EDITOR
    public void DeletePlayer()
    {
        player = null;
    }

    public void Update_Editor(float deltaTime)
    {
        if(player != null && !Application.isPlaying)
        {
            player.Update_Editor(deltaTime);
            #if UNITY_EDITOR
            EditorUtility.SetDirty(gameObject);
            #endif
        }
    }

    /*private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            Init();
            Update_Editor(0);
        }
    }*/
#endif

    void Awake()
    {
        Init();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.update += EditorUpdateFunc;
        }
#endif
    }

    private void Start()
    {
        if (player != null && player.IsPlaying == false && anim != null && anim.clips != null && anim.clips.Length > 0)
        {
            player.Play(anim.clips[Mathf.Clamp(defaultPlayingClipIndex, 0, anim.clips.Length)].name);
        }

#if UNITY_EDITOR
        Update_Editor(0);
#endif
    }
    int GetIdleNameIndex()
    {
        for (int i = 0; i < anim.clips.Length; i++)
        {
            if (anim.clips[i].name == "thecity_idle01")
            {
                return i;
            }
        }
        return -1;
    }
    private void Update()
    {
        if (player != null)
        {
#if UNITY_EDITOR
            if(Application.isPlaying)
            {
                player.Update(Time.deltaTime);
            }
            else
            {
                player.Update_Editor(0);
            }
#else
            player.Update(Time.deltaTime);
#endif
        }
    }

#if UNITY_EDITOR
    private void EditorUpdateFunc()
    {
        Update_Editor(0.03f);
    }
#endif

    private void OnDestroy()
    {
        player = null;
        anim = null;
        mesh = null;
        mtrl = null;
        animTexture = null;

        if (Application.isPlaying)
        {
            playerManager.Unregister(this);
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorApplication.update -= EditorUpdateFunc;
            //Resources.UnloadUnusedAssets();
            //UnityEditor.EditorUtility.UnloadUnusedAssetsImmediate();
        }
#endif
    }

    void OnDrawGizmosSelected()
    {
        if(mesh ==null)
            return;

        // Display the explosion radius when selected
        Gizmos.color = new Color(1, 1, 0, 0.15F);
        Matrix4x4 matrix = transform.localToWorldMatrix;
        var worldPos = matrix.MultiplyPoint(mesh.bounds.center);

        Gizmos.DrawWireCube(worldPos, mesh.bounds.size);
    }

    public void SetTintColor(Color color)
    {
        player?.SetTintColor(color);
    }

    public void SetModelMainTexture(Texture mainTexture)
    {
        player?.SetModelMainTexture(mainTexture);
    }

    public void SetCurNormalizedTime(float normalizeTime)
    {
        if (player != null)
        {
            player.NormalizedTime = normalizeTime;
        }
    }

    public void SetCurSpeed(float speed)
    {
        if (player != null)
        {
            player.CurAnimSpeed = speed;
        }
    }

    public GPUSkinningPlayer GetPlayer()
    {
        return player;
    }

    public float GetAnimClipLength(string animName)
    {
        foreach (var clip in anim.clips)
        {
            if (clip.name == animName)
            {
                return clip.length;
            }
        }

        return 0;
    }

    public void ResetAnim()
    {
        if (player != null && player.IsPlaying == false && anim != null && anim.clips != null && anim.clips.Length > 0)
        {
            var index = GetIdleNameIndex();
            if (index >= 0)
                player.Play(anim.clips[index].name);
        }
    }
}
