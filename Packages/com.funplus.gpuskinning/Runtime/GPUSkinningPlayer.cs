#define USE_ANIMATION_EVENT
using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GPUSkinningPlayer
{
    public delegate void OnAnimEvent(GPUSkinningPlayer player, int eventId);

    private GameObject go = null;

    private Transform transform = null;

    private MeshRenderer mr = null;

    private MeshRenderer partsMeshRender= null;

    private MeshFilter mf = null;

    private float time = 0;

    //private float timeDiff = 0;

    private float crossFadeTime = -1;

    private float crossFadeProgress = 0;

    private float lastPlayedTime = 0;

    private GPUSkinningClip lastPlayedClip = null;

    private int lastPlayingFrameIndex = -1;

    private GPUSkinningClip lastPlayingClip = null;

    private GPUSkinningClip playingClip = null;

    private GPUSkinningPlayerResources res = null;

    private MaterialPropertyBlock mpb = null;
    private MaterialPropertyBlock partsMPB = null;

    /// <summary>
    /// Replace current materials
    /// </summary>
    private Material[] replaceMaterials;

    private Color tintColor = Color.black;

    float curAnimSpeed = 1;

    Action playFinishCallBack;
    private Texture modelMainTexture = null;


    public event OnAnimEvent onAnimEvent;

#if USE_ANIMATION_EVENT
    private AnimationEventDispatcherInterface eventDispatcher;
#endif

    private GPUSKinningCullingMode cullingMode = GPUSKinningCullingMode.CullUpdateTransforms;
    public GPUSKinningCullingMode CullingMode
    {
        get => Application.isPlaying ? cullingMode : GPUSKinningCullingMode.AlwaysAnimate;
        set => cullingMode = value;
    }

    private bool visible = true;
    public bool Visible
    {
        get
        {
            var v = Application.isPlaying ? visible : true;
            return v;
        }
        set => visible = value;
    }

    private bool isPlaying = false;
    public bool IsPlaying => isPlaying;

    public string PlayingClipName => playingClip == null ? null : playingClip.name;

    public Vector3 Position => transform == null ? Vector3.zero : transform.position;

    public Vector3 LocalPosition => transform == null ? Vector3.zero : transform.localPosition;

    public GPUSkinningWrapMode WrapMode => playingClip == null ? GPUSkinningWrapMode.Once : playingClip.wrapMode;

    public bool IsTimeAtTheEndOfLoop
    {
        get
        {
            if(playingClip == null)
            {
                return false;
            }
            else
            {
                // return GetFrameIndex() == ((int)(playingClip.length * playingClip.fps) - 1);
                return time >= playingClip.length;
            }
        }
    }

    public float NormalizedTime
    {
        get
        {
            if(playingClip == null)
            {
                return 0;
            }
            else
            {
                return (float)GetFrameIndex() / (float)((int)(playingClip.length * playingClip.fps) - 1);
            }
        }
        set
        {
            if(playingClip != null)
            {
                float v = Mathf.Clamp01(value);
                this.time = v * playingClip.length;

                // if(WrapMode == GPUSkinningWrapMode.Once)
                // {
                // }
                // else if(WrapMode == GPUSkinningWrapMode.Loop)
                // {
                //     res.Time = v * playingClip.length;
                //     //res.Time = playingClip.length +  v * playingClip.length - this.timeDiff;
                // }
                // else
                // {
                //     throw new System.NotImplementedException();
                // }
            }
        }
    }

    public float CurAnimSpeed
    {
        get => curAnimSpeed;
        set => curAnimSpeed = value;
    }

    public GPUSkinningPlayer(GameObject attachToThisGo, GPUSkinningPlayerResources res)
    {
        go = attachToThisGo;
        transform = go.transform;
        this.res = res;

        mr = go.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            mr = go.AddComponent<MeshRenderer>();
        }
        mf = go.GetComponent<MeshFilter>();
        if (mf == null)
        {
            mf = go.AddComponent<MeshFilter>();
        }

        mr.sharedMaterials = res.GetMaterials();
        mf.sharedMesh = res.GetMesh();

        mpb = new MaterialPropertyBlock();
        partsMPB = new MaterialPropertyBlock();
    }

    public void Play(string clipName,Action finishCallBack = null)
    {
        GPUSkinningClip[] clips = res.GetAnimation().clips;
        int numClips = clips == null ? 0 : clips.Length;
        for(int i = 0; i < numClips; ++i)
        {
            if(clips[i].name == clipName)
            {
                if (playingClip != clips[i] ||
                    (playingClip != null && playingClip.wrapMode == GPUSkinningWrapMode.Once && IsTimeAtTheEndOfLoop) ||
                    (playingClip != null && !isPlaying))
                {
                    this.playFinishCallBack = finishCallBack;
                    SetNewPlayingClip(clips[i]);
                }
                return;
            }
        }
    }

    public void CrossFade(string clipName, float fadeLength,Action playFinishCallBack = null)
    {
        if (playingClip == null)
        {
            Play(clipName,playFinishCallBack);
        }
        else
        {
            GPUSkinningClip[] clips = res.GetAnimation().clips;
            int numClips = clips == null ? 0 : clips.Length;
            for (int i = 0; i < numClips; ++i)
            {
                if (clips[i].name == clipName)
                {
                    if (playingClip != clips[i])
                    {
                        crossFadeProgress = 0;
                        crossFadeTime = fadeLength;
                        this.playFinishCallBack = playFinishCallBack;
                        SetNewPlayingClip(clips[i]);
                        return;
                    }
                    if ((playingClip.wrapMode == GPUSkinningWrapMode.Once && IsTimeAtTheEndOfLoop) ||!isPlaying)
                    {
                        this.playFinishCallBack = playFinishCallBack;
                        SetNewPlayingClip(clips[i]);
                        return;
                    }
                }
            }
        }
    }

    public void Stop()
    {
        isPlaying = false;
    }

    public void Resume()
    {
        if(playingClip != null)
        {
            isPlaying = true;
        }
    }

#if UNITY_EDITOR
    public void Update_Editor(float timeDelta)
    {
        Update_Internal(timeDelta);
    }
#endif

    /// <summary>
    /// Set/Replace the parts' model
    /// </summary>
    /// <param name="partsObj"></param>
    /// <param name="force"></param>
    public void ReplaceParts(GameObject partsObj, bool force)
    {
        if(partsMeshRender != null && !force)
            return;

        var render = partsObj.GetComponent<MeshRenderer>();
        if (render == null)
        {
            Debug.LogError("ReplaceParts: MeshRenderer is null");
            return;
        }
        var partsTrans = partsObj.transform;
        partsTrans.parent = transform;
        partsTrans.localPosition = Vector3.zero;
        partsTrans.localRotation = Quaternion.identity;
        partsTrans.localScale = Vector3.one;

        partsMeshRender = render;
    }

    /// <summary>
    /// Clear current parts
    /// </summary>
    public void ClearParts()
    {
        if (partsMeshRender != null)
        {
            partsMeshRender.transform.parent = null;
            partsMeshRender = null;
        }
    }

    /// <summary>
    /// Set visible status of current parts
    /// </summary>
    /// <param name="enable"></param>
    public void SetPartsEnable(bool enable)
    {
        if (partsMeshRender != null)
        {
            partsMeshRender.enabled = enable;
        }
    }

    /// <summary>
    /// Replace materials
    /// </summary>
    public void ReplaceMaterials(Material[] materials)
    {
        if(materials == null || materials.Length <=0)
            return;

        replaceMaterials = materials;
        if (mr != null)
            mr.materials = materials;
    }

    /// <summary>
    /// Recover materials from GPUSkinningPlayerResources
    /// </summary>
    public void RecoverMaterials()
    {
        replaceMaterials = null;
        if (mr != null)
            mr.materials = res.GetMaterials();
    }

    public Material[] GetMaterials()
    {
        return res.GetMaterials();
    }

    public void Update(float timeDelta)
    {
        Update_Internal(timeDelta);
    }

    private void FillEvents(GPUSkinningClip clip, GPUSkinningBetterList<GPUSkinningAnimEvent> events)
    {
        events.Clear();
        if(clip != null && clip.events != null && clip.events.Length > 0)
        {
            events.AddRange(clip.events);
        }
    }

    private void SetNewPlayingClip(GPUSkinningClip clip)
    {
        lastPlayedClip = playingClip;
        lastPlayedTime = GetCurrentTime();

        isPlaying = true;
        playingClip = clip;
        time = 0;
        //timeDiff = Random.Range(0, playingClip.length);
    }

    private void Update_Internal(float timeDelta)
    {
        if (!isPlaying || playingClip == null)
        {
            return;
        }

        timeDelta *= playingClip.speed * curAnimSpeed;

        if (playingClip.wrapMode == GPUSkinningWrapMode.Loop)
        {
            UpdateMaterial(timeDelta);
        }
        else if(playingClip.wrapMode == GPUSkinningWrapMode.Once)
        {
            if (time >= playingClip.length)
            {
                time = playingClip.length;
                UpdateMaterial(timeDelta);
                if (playFinishCallBack != null)
                {
                    playFinishCallBack.Invoke();
                    playFinishCallBack = null;
                }
            }
            else
            {
                UpdateMaterial(timeDelta);
                if(time > playingClip.length)
                {
                    time = playingClip.length;
                }
            }
        }
        else
        {
            throw new System.NotImplementedException();
        }

        crossFadeProgress += timeDelta;
        lastPlayedTime += timeDelta;
    }

    private void UpdateEvents(GPUSkinningClip playingClip, int playingFrameIndex, GPUSkinningClip corssFadeClip, int crossFadeFrameIndex)
    {
        UpdateClipEvent(playingClip, playingFrameIndex);
        UpdateClipEvent(corssFadeClip, crossFadeFrameIndex);
    }

    private void UpdateClipEvent(GPUSkinningClip clip, int frameIndex)
    {
        if(clip == null || clip.events == null || clip.events.Length == 0)
        {
            return;
        }

        GPUSkinningAnimEvent[] events = clip.events;
        int numEvents = events.Length;
        for(int i = 0; i < numEvents; ++i)
        {
            if(events[i].frameIndex == frameIndex && onAnimEvent != null)
            {
                onAnimEvent(this, events[i].eventId);
                break;
            }
        }
    }

    //----动画事件----//

#if USE_ANIMATION_EVENT
    public void SetEventDispatcher(AnimationEventDispatcherInterface eventDispatcher)
    {
        this.eventDispatcher = eventDispatcher;
    }

    private void UpdateAnimationEvent(GPUSkinningClip clip, float deltaTime)
    {
        if(clip == null || eventDispatcher == null)
        {
            return;
        }
        //记录上一帧和当前帧时间 触发这之间的所有事件
        float startTime = this.time % clip.length;
        float endTime = startTime + deltaTime;
        eventDispatcher.DispatchIntervalTime(clip.name, startTime, endTime);
        //超过动画时间时 截断 然后触发0到endtime的这一部分事件
        if(endTime > clip.length)
        {
            eventDispatcher.DispatchIntervalTime(clip.name, 0, endTime - clip.length);
        }
    }
#endif

    //----动画事件End----//

    private void UpdateMaterial(float deltaTime)
    {
        int frameIndex = GetFrameIndex();
        time += deltaTime;

        if(lastPlayingClip == playingClip && lastPlayingFrameIndex == frameIndex)
        {
            // update materials
            if(replaceMaterials != null)
                res.UpdateMaterials(replaceMaterials);
            else
                res.Update();

            if(partsMeshRender)
                res.UpdateMaterial(partsMeshRender.sharedMaterial);
            return;
        }

        lastPlayingClip = playingClip;
        lastPlayingFrameIndex = frameIndex;

        int crossFadeFrameIndex = -1;
        var isCrossFadeBlending = IsCrossFadeBlending(lastPlayedClip, crossFadeTime, crossFadeProgress);
        if (isCrossFadeBlending)
            crossFadeFrameIndex = GetCrossFadeFrameIndex();

        if (Visible || CullingMode == GPUSKinningCullingMode.AlwaysAnimate)
        {
            // update materials
            if(replaceMaterials != null)
                res.UpdateMaterials(replaceMaterials);
            else
                res.Update();

            if(partsMeshRender)
                res.UpdateMaterial(partsMeshRender.sharedMaterial);

            mr.GetPropertyBlock(mpb);
            res.UpdatePlayingData(mpb, playingClip, frameIndex,
                lastPlayedClip, GetCrossFadeFrameIndex(), crossFadeTime, crossFadeProgress,
                tintColor
            );
            mr.SetPropertyBlock(mpb);

            if (partsMeshRender != null)
            {
                partsMeshRender.GetPropertyBlock(partsMPB);
                res.UpdatePlayingData(partsMPB, playingClip, frameIndex,
                    lastPlayedClip, GetCrossFadeFrameIndex(), crossFadeTime, crossFadeProgress,
                    tintColor
                );
                partsMeshRender.SetPropertyBlock(partsMPB);
            }
        }

        UpdateEvents(playingClip, frameIndex, isCrossFadeBlending ? lastPlayedClip : null, crossFadeFrameIndex);
#if USE_ANIMATION_EVENT
        UpdateAnimationEvent(playingClip, deltaTime);
        UpdateAnimationEvent(isCrossFadeBlending ? lastPlayedClip : null, deltaTime);
#endif
    }

    public static bool IsCrossFadeBlending(GPUSkinningClip lastPlayedClip, float crossFadeTime, float crossFadeProgress)
    {
        return lastPlayedClip != null && crossFadeTime > 0 && crossFadeProgress <= crossFadeTime;
    }

    public static float CrossFadeBlendFactor(float crossFadeProgress, float crossFadeTime)
    {
        return Mathf.Clamp01(crossFadeProgress / crossFadeTime);
    }

    private bool IsBlending()
    {
        if(res == null)
        {
            return false;
        }

        if(playingClip == null)
        {
            return false;
        }

        if(IsCrossFadeBlending(lastPlayedClip, crossFadeTime, crossFadeProgress))
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private float GetCurrentTime()
    {
        return this.time;
        // var time = 0;
        // if (WrapMode == GPUSkinningWrapMode.Once)
        // {
        //     time = this.time;
        // }
        // else if (WrapMode == GPUSkinningWrapMode.Loop)
        // {
        //     time = res.Time;// + (playingClip.individualDifferenceEnabled ? this.timeDiff : 0);
        // }
        // else
        // {
        //     throw new System.NotImplementedException();
        // }
    }

    private int GetFrameIndex()
    {
        float time = GetCurrentTime();
        if (playingClip.length == time)
        {
            return playingClip.FrameCount;
        }
        else
        {
            return playingClip.GetFrameIndex(time);
        }
    }

    private int GetCrossFadeFrameIndex()
    {
        if (lastPlayedClip == null)
        {
            return 0;
        }

        if (lastPlayedClip.wrapMode == GPUSkinningWrapMode.Once)
        {
            if (lastPlayedTime >= lastPlayedClip.length)
            {
                return lastPlayedClip.FrameCount;
            }
            else
            {
                return lastPlayedClip.GetFrameIndex(lastPlayedTime);
            }
        }
        else if (lastPlayedClip.wrapMode == GPUSkinningWrapMode.Loop)
        {
            return lastPlayedClip.GetFrameIndex(lastPlayedTime);
        }
        else
        {
            throw new System.NotImplementedException();
        }
    }

    public void SetTintColor(Color color)
    {
        tintColor = color;
    }

    public void SetModelMainTexture(Texture tex)
    {
        modelMainTexture = tex;
        mr.GetPropertyBlock(mpb);
        mpb.SetTexture("_BaseMap",tex);
        mr.SetPropertyBlock(mpb);

    }

#if UNITY_EDITOR
    /// <summary>
    /// 编辑器调用 预览某个时间点
    /// </summary>
    /// <param name="time"></param>
    public void SetTimeEditor(float time)
    {
        this.time = time;
        Update_Editor(0);
    }
#endif
}
