using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GPUSkinningWrapMode
{
    Once,
    Loop
}

public enum GPUSKinningCullingMode
{
    AlwaysAnimate,
    CullUpdateTransforms,
    CullCompletely
}

public class GPUSkinningExecuteOncePerFrame
{
    private int frameCount = -1;

    public bool CanBeExecute()
    {
        if (Application.isPlaying)
        {
            return frameCount != Time.frameCount;
        }
        else
        {
            return true;
        }
    }

    public void MarkAsExecuted()
    {
        if (Application.isPlaying)
        {
            frameCount = Time.frameCount;
        }
    }
}

public class GPUSkinningPlayerResources
{
    private Mesh mesh = null;
    private Material[] materials = null;
    private Texture2D texture = null;
    private GPUSkinningAnimation anim = null;

    public List<GPUSkinAnimator> players = new List<GPUSkinAnimator>();

    private Vector4 textureSizeNumPixelsPerFrame;
    private Vector4 pixelSegmentation;
    private Vector4 crossFade;

    private GPUSkinningExecuteOncePerFrame executeOncePerFrame = new GPUSkinningExecuteOncePerFrame();

    private static int _GPUSkinning_TextureMatrix = -1;
    private static int _GPUSkinning_TextureSize_NumPixelsPerFrame = 0;
    private static int _GPUSkinning_FrameIndex_PixelSegmentation = 0;
    private static int _GPUSkinning_BlendOn = 0;
    private static int _GPUSkinning_CrossFade = 0;
    private static int _GPUSkinning_TintColor = 0;

    public GPUSkinningPlayerResources()
    {
        if (_GPUSkinning_TextureMatrix == -1)
        {
            _GPUSkinning_TextureMatrix = Shader.PropertyToID("_GPUSkinning_TextureMatrix");
            _GPUSkinning_TextureSize_NumPixelsPerFrame = Shader.PropertyToID("_GPUSkinning_TextureSize_NumPixelsPerFrame");
            _GPUSkinning_FrameIndex_PixelSegmentation = Shader.PropertyToID("_GPUSkinning_FrameIndex_PixelSegmentation");
            _GPUSkinning_BlendOn =  Shader.PropertyToID("_GPUSkinning_BlendOn");
            _GPUSkinning_CrossFade = Shader.PropertyToID("_GPUSkinning_CrossFade");
            _GPUSkinning_TintColor = Shader.PropertyToID("_TintColor");
        }
    }

    public void Init(Mesh meshRes, Material[] materialsRes, Texture2D textureRes, GPUSkinningAnimation animRes)
    {
        mesh = meshRes;
        materials = materialsRes;
        texture = textureRes;
        anim = animRes;

        InitMaterial(materials);

        textureSizeNumPixelsPerFrame.x = anim.textureWidth;
        textureSizeNumPixelsPerFrame.y = anim.textureHeight;
        textureSizeNumPixelsPerFrame.z = anim.boneLenght * 3 /*treat 3 pixels as a float3x4*/;
        textureSizeNumPixelsPerFrame.w = 0;
    }

    public string GUID
    {
        get
        {
            if (anim)
                return anim.guid;
            else
                return "";
        }
    }

    public void Destroy()
    {
        mesh = null;

        if(materials != null)
        {
            for(int i = 0; i < materials.Length; ++i)
            {
                materials[i] = null;
            }
            materials = null;
        }

        texture = null;
        anim = null;

        if (players != null)
        {
            players.Clear();
            players = null;
        }
    }

    /// <summary>
    /// Update Material's property
    /// </summary>
    public void Update()
    {
        if (executeOncePerFrame.CanBeExecute())
        {
            executeOncePerFrame.MarkAsExecuted();

            for (int i = 0; i < materials.Length; ++i)
            {
                var mat = materials[i];
                if(mat == null)
                    continue;

                mat.SetTexture(_GPUSkinning_TextureMatrix, texture);
                mat.SetVector(_GPUSkinning_TextureSize_NumPixelsPerFrame, textureSizeNumPixelsPerFrame);
            }
        }
    }

    /// <summary>
    /// Update external material
    /// </summary>
    /// <param name="mat"></param>
    public void UpdateMaterial(Material mat)
    {
        if(mat == null)
            return;

        mat.SetTexture(_GPUSkinning_TextureMatrix, texture);
        mat.SetVector(_GPUSkinning_TextureSize_NumPixelsPerFrame, textureSizeNumPixelsPerFrame);
    }

    /// <summary>
    /// Update external materials
    /// </summary>
    /// <param name="mat"></param>
    public void UpdateMaterials(Material[] materials)
    {
        if(materials == null)
            return;

        for (int i = 0; i < materials.Length; ++i)
        {
            var mat = materials[i];
            if(mat == null)
                continue;

            mat.SetTexture(_GPUSkinning_TextureMatrix, texture);
            mat.SetVector(_GPUSkinning_TextureSize_NumPixelsPerFrame, textureSizeNumPixelsPerFrame);
        }
    }

    /// <summary>
    /// Update Material Property block
    /// </summary>
    public void UpdatePlayingData(MaterialPropertyBlock mpb, GPUSkinningClip playingClip, int frameIndex,
        GPUSkinningClip lastPlayedClip, int crossFadeFrameIndex, float crossFadeTime, float crossFadeProgress, Color tintColor)
    {
        pixelSegmentation.x = frameIndex;
        pixelSegmentation.y = playingClip.pixelSegmentation;
        pixelSegmentation.z = 0;
        pixelSegmentation.w = 0;

        mpb.SetVector(_GPUSkinning_FrameIndex_PixelSegmentation, pixelSegmentation);
        mpb.SetColor(_GPUSkinning_TintColor, tintColor);

        if (GPUSkinningPlayer.IsCrossFadeBlending(lastPlayedClip, crossFadeTime, crossFadeProgress))
        {
            crossFade.x = crossFadeFrameIndex;
            crossFade.y = lastPlayedClip.pixelSegmentation;
            crossFade.z = GPUSkinningPlayer.CrossFadeBlendFactor(crossFadeProgress, crossFadeTime);
            crossFade.w = 0;

            mpb.SetVector(_GPUSkinning_CrossFade, crossFade);
            mpb.SetFloat(_GPUSkinning_BlendOn, 1);
        }
        else
        {
            mpb.SetFloat(_GPUSkinning_BlendOn, 0);
        }
    }

    /// <summary>
    /// Initialize materials
    /// </summary>
    private void InitMaterial(Material[] materials)
    {
        foreach (var mat in materials)
        {
            if(mat == null)
                continue;

            mat.enableInstancing = true;
        }
    }

    public Material[] GetMaterials()
    {
        return materials;
    }

    public Mesh GetMesh()
    {
        return mesh;
    }

    public GPUSkinningAnimation GetAnimation()
    {
        return anim;
    }
}
