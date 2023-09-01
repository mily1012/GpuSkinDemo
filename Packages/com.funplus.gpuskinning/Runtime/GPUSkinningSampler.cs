using System;
using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.Serialization;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.Rendering;
#endif

public class GPUSkinningSampler : MonoBehaviour
{
#if UNITY_EDITOR
    private Animator animator = null;

    private AnimationClip[] animClips = null;

    /// <summary>
    /// 如果是AnimatorOverrideController，就记录一份originalAnimClip
    /// </summary>
    private List<KeyValuePair<AnimationClip, AnimationClip>> animatorControllerOverrides = null;

    [SerializeField]
    private GPUSkinningAnimation anim = null;

    private AnimatorOverrideController animatorOverrideController = null;

	private SkinnedMeshRenderer[] skinnedMeshRenders = null;                    //蒙皮网格缓存

	private GPUSkinningAnimation gpuSkinningAnimation = null;

    private bool mInited = false;               //初始化标记

    private bool isSampling = false;                //采样标记
    public int samplingClipIndex { get; set; }              //当前采样clip索引

    public int samplingFrameIndex { get; set; }             //当前采样clipFrame索引

    #region 序列化相关
    private string animName = null;

    private bool mOnlyExportSelectedMesh = false;                   //是否仅导出选中mesh

    private int mSelectedSkinMeshIndex = 0;

    private string savePath = null;                 //序列化路径

    private bool isCreatedAsset = false;
    #endregion

    #region 资产对象缓存
    private Mesh savedMesh = null;

    private Material[] savedMtrl = null;

    private Texture2D savedTexture = null;
    #endregion
    public void Init(bool onlyExportFirstMesh, int selectedSkinMeshIndex, string savePath, string animName)
    {
        if (mInited)
            return;

        mInited = true;
        this.animName = animName;                               //资产命名
        this.savePath = savePath;                       //输出路径

        mOnlyExportSelectedMesh = onlyExportFirstMesh;
        mSelectedSkinMeshIndex = selectedSkinMeshIndex;

        animator = GetComponent<Animator>();
        if (animator == null)
        {
            DestroyImmediate(this);
            ShowDialog("Cannot find Animator Component");
            return;
        }

        var animatorController = animator.runtimeAnimatorController;
        if (animatorController == null)
        {
            DestroyImmediate(this);
            ShowDialog("Missing RuntimeAnimatorController");
            return;
        }

        //缓存skinmeshRender
        skinnedMeshRenders = GetComponentsInChildren<SkinnedMeshRenderer>();

        //收集所有Clips并设置新的AnimatorOverrideController
        var newOverrideController = new AnimatorOverrideController(animatorController);
        var overrideController = animatorController as AnimatorOverrideController;
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        // If it's AnimatorOverrideController
        if (overrideController != null)
        {
            overrideController.GetOverrides(overrides);
            newOverrideController.ApplyOverrides(overrides);

            List<AnimationClip> overrideClips = new List<AnimationClip>();
            foreach (var pair in overrides)
            {
                if (pair.Value != null)
                    overrideClips.Add(pair.Value);
            }
            animClips = overrideClips.ToArray();

            animatorControllerOverrides = overrides;
        }
        else // If it's AnimatorController
        {
            AnimationClip[] clips = animatorController.animationClips;
            animClips = clips;
            for (int i = 0; i < clips.Length; ++i)
            {
                var pair = new KeyValuePair<AnimationClip, AnimationClip>(clips[i], clips[i]);
                overrides.Add(pair);
            }
            newOverrideController.ApplyOverrides(overrides);
        }
        animator.runtimeAnimatorController = newOverrideController;
        animatorOverrideController = newOverrideController;

        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        gpuSkinningAnimation = anim == null ? ScriptableObject.CreateInstance<GPUSkinningAnimation>() : anim;
        gpuSkinningAnimation.guid = anim == null ? System.Guid.NewGuid().ToString() : gpuSkinningAnimation.guid;
        gpuSkinningAnimation.name = animName;

        //创建骨骼
        List<GPUSkinningBone> bones_result = new List<GPUSkinningBone>();
        CollectBones(bones_result, skinnedMeshRenders, transform);
        GPUSkinningBone[] newBones = bones_result.ToArray();
        gpuSkinningAnimation.bones = newBones;

        InitTransform();
    }

    public void BeginSample()
    {
        samplingClipIndex = 0;
        isCreatedAsset = false;
    }

    public void EndSample()
    {
        CreateTextureMatrix(savePath, anim);
        ClearGpuSKinAnimationFrames();
        samplingClipIndex = -1;

        // skin object
        GameObject skinObject = new GameObject();
        skinObject.transform.localScale = localScale;
        skinObject.name = $"{animName}_skin";
        var gpuSkinAnimator = skinObject.AddComponent<GPUSkinAnimator>();
        gpuSkinAnimator.anim = anim;
        gpuSkinAnimator.mesh = savedMesh;
        gpuSkinAnimator.mtrl = savedMtrl;
        gpuSkinAnimator.animTexture = savedTexture;
        gpuSkinAnimator.Init();

        var meshRenderer = skinObject.GetComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;

        var assetPath = $"{savePath}/{animName}_skin.prefab";
        PrefabUtility.SaveAsPrefabAsset(skinObject, assetPath);

        AssetDatabase.ImportAsset(assetPath);

        GameObject.DestroyImmediate(skinObject, true);
    }

    /// <summary>
    /// 烘焙完贴图清空asset文件中的矩阵信息
    /// </summary>
    public void ClearGpuSKinAnimationFrames()
    {
        string dir = savePath;
        string savedAnimPath = dir + "/GPUSKinning_Anim_" + animName + ".asset";
        foreach (var clip in gpuSkinningAnimation.clips)
        {
            clip.frames = null;
        }

        EditorUtility.SetDirty(gpuSkinningAnimation);
        if (anim != gpuSkinningAnimation)
        {
            anim = CreateOrReplaceAsset(gpuSkinningAnimation, savedAnimPath);
        }

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();
    }
    public void StartSample()
	{
        if (isSampling)
            return;

        AnimationClip animClip = animClips[samplingClipIndex];
        if (!IsValid())
            return;

        samplingFrameIndex = 0;

        // new animation clips may has the same name of old animation clips, so need to record the status whether it need to override.
        int numClips = gpuSkinningAnimation.clips == null ? 0 : gpuSkinningAnimation.clips.Length;
        int overrideClipIndex = -1;
        for (int i = 0; i < numClips; ++i)
        {
            if (gpuSkinningAnimation.clips[i].name == animClip.name)
            {
                overrideClipIndex = i;
                break;
            }
        }

        GPUSkinningClip gpuSkinningClip = new GPUSkinningClip();
        var gpuSkinningClipName = GetStateName(((AnimatorController)animatorOverrideController.runtimeAnimatorController), animClip);
        gpuSkinningClip.name = string.IsNullOrEmpty(gpuSkinningClipName) ? animClip.name : gpuSkinningClipName;
        gpuSkinningClip.fps = (int)animClip.frameRate;
        gpuSkinningClip.length = animClip.length;
        gpuSkinningClip.wrapMode = animClip.isLooping ? GPUSkinningWrapMode.Loop : GPUSkinningWrapMode.Once;
        gpuSkinningClip.frames = new GPUSkinningFrame[(int)(animClip.frameRate * animClip.length)];
        gpuSkinningClip.speed = 1;

        if (gpuSkinningAnimation.clips == null)
        {
            gpuSkinningAnimation.clips = new GPUSkinningClip[] {gpuSkinningClip};
        }
        else
        {
            if (overrideClipIndex == -1)
            {
                List<GPUSkinningClip> clips = new List<GPUSkinningClip>(gpuSkinningAnimation.clips);
                clips.Add(gpuSkinningClip);
                gpuSkinningAnimation.clips = clips.ToArray();
            }
            else
            {
                GPUSkinningClip overridedClip = gpuSkinningAnimation.clips[overrideClipIndex];
                RestoreCustomClipData(overridedClip, gpuSkinningClip);
                gpuSkinningAnimation.clips[overrideClipIndex] = gpuSkinningClip;
            }
        }

        ResetDefaultPose();
        SetCurrentAnimationClip();
        RecordAnimator();

        isSampling = true;
    }

    public void ExportOnlyMeshAndMaterial(GameObject partsObject, string savePartsPath)
    {
        var renders = partsObject.GetComponentsInChildren<SkinnedMeshRenderer>();

        var partsName = partsObject.gameObject.name;

        // create mesh
        if (skinnedMeshRenders == null)
        {
            Debug.LogError("ExportOnlyMeshAndMaterial: skinnedMeshRenders is null");
            return;
        }

        Mesh newMesh = CreateMesh(renders, "GPUSkinning_Mesh", true);

        var meshName = $"GPUSKinning_Mesh_{partsName}";
        newMesh.name = meshName;
        string savedMeshPath = $"{savePartsPath}/{meshName}.asset";
        var savedPartsMesh = CreateOrReplaceAsset(newMesh, savedMeshPath);

        // create material
        var mats = CreateShaderAndMaterial(renders, savePartsPath, animName);

        // parts object
        GameObject partsSkinObject = new GameObject();
        partsSkinObject.transform.localScale = localScale;
        partsSkinObject.name = $"{partsName}_parts_skin";

        var filter = partsSkinObject.AddComponent<MeshFilter>();
        filter.mesh = savedPartsMesh;
        var meshRender = partsSkinObject.AddComponent<MeshRenderer>();
        meshRender.materials = mats;

        var assetPath = $"{savePartsPath}/{partsName}_parts_skin.prefab";
        PrefabUtility.SaveAsPrefabAsset(partsSkinObject, assetPath);

        AssetDatabase.ImportAsset(assetPath);

        GameObject.DestroyImmediate(partsSkinObject, true);
    }

    /// <summary>
    /// restore clips' event from old clips
    /// </summary>
    /// <param name="src"></param>
    /// <param name="dest"></param>
    private void RestoreCustomClipData(GPUSkinningClip src, GPUSkinningClip dest)
    {                         
        if(src.events != null)
        {
            int totalFrames = (int)(dest.length * dest.fps);
            dest.events = new GPUSkinningAnimEvent[src.events.Length];
            for(int i = 0; i < dest.events.Length; ++i)
            {
                GPUSkinningAnimEvent evt = new GPUSkinningAnimEvent();
                evt.eventId = src.events[i].eventId;
                evt.frameIndex = Mathf.Clamp(src.events[i].frameIndex, 0, totalFrames - 1);
                dest.events[i] = evt;
            }
        }
    }

    /// <summary>
    /// Record animator and then playback.
    /// </summary>
    private void RecordAnimator()
    {
        if (animator != null)
        {
            GPUSkinningClip clip = gpuSkinningAnimation.clips[samplingClipIndex];
            int numFrames = (int)(clip.fps * clip.length);

            animator.applyRootMotion = false;
            animator.Rebind();
            animator.recorderStartTime = 0;

            animator.StartRecording(numFrames);
            for (int i = 0; i < numFrames; ++i)
            {
                animator.Update(1.0f / clip.fps);
            }
            animator.StopRecording();
            animator.StartPlayback();
        }

    }

    private void ResetDefaultPose()
    {
            animator.Play("DefaultPose");
            animator.Update(0f);
    }

    /// <summary>
    /// Set current animation clip played by animationcontroller.
    /// </summary>
    private void SetCurrentAnimationClip()
    {
        if (animatorOverrideController != null)
        {
            var oldOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            animatorOverrideController.GetOverrides(oldOverrides);

            var newOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            AnimationClip animClip = animClips[samplingClipIndex];
            if (oldOverrides.Count <= 0) // if not exist override pairs
            {
                foreach (var clip in animatorOverrideController.animationClips)
                {
                    var newPair = new KeyValuePair<AnimationClip, AnimationClip>(clip, animClip);
                    newOverrides.Add(newPair);
                }
            }
            else
            {
                foreach (var pair in oldOverrides)
                {
                    var newPair = new KeyValuePair<AnimationClip, AnimationClip>(pair.Key, animClip);
                    newOverrides.Add(newPair);
                }
            }

            animatorOverrideController.ApplyOverrides(newOverrides);
        }
    }

    /// <summary>
    /// Create mesh from skinmeshrenderer, and fill uv1/uv2 by bones' index and weight.
    /// </summary>
    /// <param name="mesh"></param>
    /// <param name="meshName"></param>
    /// <returns></returns>
    private Mesh CreateMesh(SkinnedMeshRenderer[] renders, string meshName, bool searchByName)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector4> tangents = new List<Vector4>();
        List<Color> colors = new List<Color>();
        List<Vector2> uvs = new List<Vector2>();
        List<Vector4> uv1s = new List<Vector4>();
        List<int[]> triangles = new List<int[]>();
        int submeshCount = 0;

        foreach (var r in renders)
        {
            var mesh = r.sharedMesh;

            var isReadable = mesh.isReadable;
            mesh.UploadMeshData(false);

            // append vertex/normal/tangent/color/uv
            int vertexStartIndex = vertices.Count;
            vertices.AddRange(mesh.vertices);

            if (mesh.normals != null && mesh.normals.Length > 0)
                normals.AddRange(mesh.normals);

            if (mesh.tangents != null && mesh.tangents.Length > 0)
                tangents.AddRange(mesh.tangents);

            if (mesh.colors != null && mesh.colors.Length > 0)
            {
                colors.AddRange(mesh.colors);

            }
            else
            {
                //防止出现colors数量和vertices数量不一致的情况
                for (int i = 0; i < mesh.vertices.Length; i++)
                {
                    colors.Add(new Color(1,1,1,1));
                }
            }

            if (mesh.uv != null && mesh.uv.Length > 0)
                uvs.AddRange(mesh.uv);

            int numVertices = mesh.vertexCount;
            BoneWeight[] boneWeights = mesh.boneWeights;
            Vector4[] uv1 = new Vector4[numVertices];
            Vector4[] uv2 = new Vector4[numVertices];

            var currentRenderBones = r.bones;
            for(int i = 0; i < numVertices; ++i)
            {
                BoneWeight boneWeight = boneWeights[i];

                // sort by bone's weight.
                BoneWeightSortData[] weights = new BoneWeightSortData[2];
                weights[0] = new BoneWeightSortData(){ index=boneWeight.boneIndex0, weight=boneWeight.weight0 };
                weights[1] = new BoneWeightSortData(){ index=boneWeight.boneIndex1, weight=boneWeight.weight1 };
                //weights[2] = new BoneWeightSortData(){ index=boneWeight.boneIndex2, weight=boneWeight.weight2 };
                //weights[3] = new BoneWeightSortData(){ index=boneWeight.boneIndex3, weight=boneWeight.weight3 };
                System.Array.Sort(weights);

                GPUSkinningBone bone0 = GetBoneByTransform(currentRenderBones[weights[0].index], searchByName);
                GPUSkinningBone bone1 = GetBoneByTransform(currentRenderBones[weights[1].index], searchByName);
                //GPUSkinningBone bone2 = GetBoneByTransform(searchBond[weights[2].index]);
                //GPUSkinningBone bone3 = GetBoneByTransform(searchBond[weights[3].index]);

                // Calculate the bones' index and record them in uv2/uv3 with weight data.
                Vector4 skinData_01 = new Vector4();
                skinData_01.x = GetBoneIndex(bone0);
                if(skinData_01.x == -1)
                    Debug.LogError($"Cannot index the bone:{bone0.transform.name}");
                skinData_01.z = GetBoneIndex(bone1);
                if(skinData_01.z == -1)
                    Debug.LogError($"Cannot index the bone:{bone1.transform.name}");

                var weightSum =  weights[0].weight + weights[1].weight;
                skinData_01.y = weights[0].weight / weightSum;
                skinData_01.w = weights[1].weight / weightSum;

                uv1[i] = skinData_01;

                /*Vector4 skinData_23 = new Vector4();
                skinData_23.x = GetBoneIndex(bone2);
                skinData_23.y = weights[2].weight;
                skinData_23.z = GetBoneIndex(bone3);
                skinData_23.w = weights[3].weight;
                uv2[i] = skinData_23;*/
            }

            uv1s.AddRange(uv1);
            //uv2s.AddRange(uv2);

            List<int> triangle = new List<int>();
            for (int j = 0; j < mesh.triangles.Length; ++j)
            {
                triangle.Add(vertexStartIndex + mesh.triangles[j]);
            }

            submeshCount++;
            triangles.Add(triangle.ToArray());

            mesh.UploadMeshData(!isReadable);
        }

        Mesh newMesh = new Mesh();
        newMesh.name = meshName;
        newMesh.vertices = vertices.ToArray();
        newMesh.normals = normals.ToArray();
        newMesh.tangents = tangents.ToArray();
        newMesh.colors = colors.ToArray();
        newMesh.uv = uvs.ToArray();
        newMesh.SetUVs(1, uv1s);
		//newMesh.SetUVs(2, uv2s);

        newMesh.subMeshCount = submeshCount;
        for (int k = 0; k < submeshCount; ++k)
        {
            newMesh.SetTriangles(triangles[k], k);
        }

        SerializedObject obj = new SerializedObject(newMesh);
        var property = obj.FindProperty("m_IsReadable");
        property.boolValue = false;
        obj.ApplyModifiedProperties();

        //newMesh.UploadMeshData(true);
        return newMesh;
    }

	private class BoneWeightSortData : System.IComparable<BoneWeightSortData>
	{
		public int index = 0;

		public float weight = 0;

		public int CompareTo(BoneWeightSortData b)
		{
			return weight > b.weight ? -1 : 1;
		}
	}

    /// <summary>
    /// Collect bonpose from skinmeshrender
    /// </summary>
    /// <param name="bones_result"></param>
    /// <param name="bones_smr"></param>
    /// <param name="bindposes"></param>
    /// <param name="currentBoneTransform"></param>
    /// <param name="currentBoneIndex"></param>
	private void CollectBones(List<GPUSkinningBone> bones_result, SkinnedMeshRenderer[] renderers, Transform currentBoneTransform)
	{
		GPUSkinningBone currentBone = new GPUSkinningBone();

        currentBone.transform = currentBoneTransform;
        currentBone.bindpose = Matrix4x4.identity;
        currentBone.missed = true;

        foreach (var r in renderers)
        {
            int indexOfSmrBones = System.Array.IndexOf(r.bones, currentBoneTransform);
            if (indexOfSmrBones == -1)
            {
                continue;
            }

            currentBone.missed = false;
            currentBone.bindpose = r.sharedMesh.bindposes[indexOfSmrBones];
        }

        if(currentBone.missed == false)
            bones_result.Add(currentBone);

        int numChildren = currentBone.transform.childCount;
		if(numChildren > 0)
		{
            for(int i = 0; i < numChildren; ++i)
			{
				CollectBones(bones_result, renderers, currentBone.transform.GetChild(i));
			}
		}
	}

    /// <summary>
    /// Calculate pixelSegmentation of the clips, and the animation texture's size to fill the data.
    /// </summary>
    /// <param name="gpuSkinningAnim"></param>
    private void CalculateAnimationTexture(GPUSkinningAnimation gpuSkinningAnim)
    {
        int numPixels = 0;

        GPUSkinningClip[] clips = gpuSkinningAnim.clips;
        int numClips = clips.Length;
        for (int clipIndex = 0; clipIndex < numClips; ++clipIndex)
        {
            GPUSkinningClip clip = clips[clipIndex];
            clip.pixelSegmentation = numPixels;

            GPUSkinningFrame[] frames = clip.frames;
            int numFrames = frames.Length;
            numPixels += gpuSkinningAnim.bones.Length * 3/*treat 3 pixels as a float3x4*/ * numFrames;
        }

        CalculateTextureSize(numPixels, out gpuSkinningAnim.textureWidth, out gpuSkinningAnim.textureHeight);
    }

    /// <summary>
    /// Create animationTexture and write matrix information into it.
    /// </summary>
    /// <param name="dir"></param>
    /// <param name="gpuSkinningAnim"></param>
    private void CreateTextureMatrix(string dir, GPUSkinningAnimation gpuSkinningAnim)
    {
        var texture = new Texture2D(gpuSkinningAnim.textureWidth, gpuSkinningAnim.textureHeight, TextureFormat.RGBAHalf, false, true);
        Color[] pixels = texture.GetPixels();
        int pixelIndex = 0;
        for (int clipIndex = 0; clipIndex < gpuSkinningAnim.clips.Length; ++clipIndex)
        {
            GPUSkinningClip clip = gpuSkinningAnim.clips[clipIndex];
            GPUSkinningFrame[] frames = clip.frames;
            int numFrames = frames.Length;
            for (int frameIndex = 0; frameIndex < numFrames; ++frameIndex)
            {
                GPUSkinningFrame frame = frames[frameIndex];
                if (frame.matrices == null)
                {
                    Debug.LogError("matrices is null");
                    continue;
                }

                Matrix4x4[] matrices = frame.matrices;
                int numMatrices = matrices.Length;
                for (int matrixIndex = 0; matrixIndex < numMatrices; ++matrixIndex)
                {
                    Matrix4x4 matrix = matrices[matrixIndex];
                    pixels[pixelIndex++] = new Color(matrix.m00, matrix.m01, matrix.m02, matrix.m03);
                    pixels[pixelIndex++] = new Color(matrix.m10, matrix.m11, matrix.m12, matrix.m13);
                    pixels[pixelIndex++] = new Color(matrix.m20, matrix.m21, matrix.m22, matrix.m23);
                }
            }
        }

        texture.filterMode = FilterMode.Point;
        texture.SetPixels(pixels);
        texture.Apply(false, true);

        var textureName = $"GPUSKinning_Texture_{animName}";
        texture.name = textureName;
        texture.hideFlags = 0;

        dir = dir.Replace('\\','/');
        string savedAssetPath = $"{dir}/{textureName}.asset";
        string textureDescPath = $"{dir}/{textureName}.desc";
        string duplicateTexturePath = "";

        var textureHash = texture.imageContentsHash.ToString();

        // search the same texture hash
        string[] guids = AssetDatabase.FindAssets("t:AnimationTextureDescAsset", null);
        foreach (string guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var textureDescAsset = AssetDatabase.LoadAssetAtPath<AnimationTextureDescAsset>(path);
            if (textureDescAsset && textureDescAsset.desc.contentHash == textureHash)
            {
                duplicateTexturePath = textureDescAsset.desc.texturePath;
                break;
            }
        }

        Texture2D duplicateTexture = null;
        if (!string.IsNullOrEmpty(duplicateTexturePath))
            duplicateTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(duplicateTexturePath);

        if (string.IsNullOrEmpty(duplicateTexturePath) || duplicateTexture == null)
        {
            savedTexture = CreateOrReplaceAsset(texture, savedAssetPath);

            // create texture desc file.
            var descJson = $"{{\"texturePath\":\"{savedAssetPath}\",\"contentHash\":\"{savedTexture.imageContentsHash}\"}}";
            File.WriteAllText(textureDescPath, descJson);
            AssetDatabase.ImportAsset(textureDescPath);
        }
        else
            savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(duplicateTexturePath);
    }

    /// <summary>
    /// Calculate animationTexture's size by pixels' number.
    /// </summary>
    /// <param name="numPixels"></param>
    /// <param name="texWidth"></param>
    /// <param name="texHeight"></param>
    private void CalculateTextureSize(int numPixels, out int texWidth, out int texHeight)
    {
        texWidth = 1;
        texHeight = 1;
        while (true)
        {
            if (texWidth * texHeight >= numPixels) break;
            texWidth *= 2;
            if (texWidth * texHeight >= numPixels) break;
            texHeight *= 2;
        }

        texHeight = (numPixels + texWidth - 1) / texWidth;
    }

    Vector3 localScale = Vector3.one;

    private void InitTransform()
    {
        transform.parent = null;
        transform.position = Vector3.zero;
        transform.eulerAngles = Vector3.zero;
        localScale = transform.localScale;
    }

	public void CustomUpdate()
	{
        if(!isSampling)
			return;

        GPUSkinningClip clip = gpuSkinningAnimation.clips[samplingClipIndex];
        int totalFrams = (int)(clip.length * clip.fps);

        if (samplingFrameIndex >= totalFrams)
        {
            if(animator != null)
            {
                animator.StopPlayback();
            }

            if (anim != null)
            {
                string animPath = AssetDatabase.GetAssetPath(anim);
                savePath =  System.IO.Path.GetDirectoryName(animPath);
            }

            if (!Directory.Exists(savePath))
                Directory.CreateDirectory(savePath);

            string dir = savePath;

            var meshBounds = CalculateBoundsAuto();
			string savedAnimPath = dir + "/GPUSKinning_Anim_" + animName + ".asset";
            CalculateAnimationTexture(gpuSkinningAnimation);
            EditorUtility.SetDirty(gpuSkinningAnimation);
            if (anim != gpuSkinningAnimation)
            {
                anim = CreateOrReplaceAsset(gpuSkinningAnimation, savedAnimPath);
            }

            // Create mesh/material/shader assets from skinmeshrenderer.
            if (!isCreatedAsset)
            {
                SkinnedMeshRenderer[] exportedMeshRender;
                if (mOnlyExportSelectedMesh)
                {
                    exportedMeshRender = new SkinnedMeshRenderer[1];
                    if (mSelectedSkinMeshIndex >= skinnedMeshRenders.Length)
                        mSelectedSkinMeshIndex = skinnedMeshRenders.Length - 1;

                    exportedMeshRender[0] = skinnedMeshRenders[mSelectedSkinMeshIndex];
                }
                else
                    exportedMeshRender = skinnedMeshRenders;

                Mesh newMesh = CreateMesh(exportedMeshRender, "GPUSkinning_Mesh", false);
                newMesh.bounds = meshBounds;

                var meshName = $"GPUSKinning_Mesh_{animName}";
                newMesh.name = meshName;
                string savedMeshPath = $"{dir}/{meshName}.asset";
                savedMesh = CreateOrReplaceAsset(newMesh, savedMeshPath);

                savedMtrl = CreateShaderAndMaterial(exportedMeshRender, dir, animName);
                isCreatedAsset = true;
            }

			AssetDatabase.Refresh();
			AssetDatabase.SaveAssets();

            isSampling = false;
            return;
        }

        // Calculate current time
        float time = clip.length * ((float)samplingFrameIndex / totalFrams);
        // new frame data
        GPUSkinningFrame frame = new GPUSkinningFrame();
        clip.frames[samplingFrameIndex] = frame;
        frame.matrices = new Matrix4x4[gpuSkinningAnimation.bones.Length];

        // set playbackTime of the animator
        if (animator != null)
        {
            animator.playbackTime = time;
            animator.Update(0);
        }

        SamplingCoroutine(frame);
    }

    private Bounds CalculateBoundsAuto()
    {
        Matrix4x4[] matrices = gpuSkinningAnimation.clips[0].frames[0].matrices;
        GPUSkinningBone[] bones = gpuSkinningAnimation.bones;
        Vector3 min = Vector3.one * 9999;
        Vector3 max = min * -1;
        for (int i = 0; i < bones.Length; ++i)
        {
            Vector4 pos = (matrices[i] * bones[i].bindpose.inverse) * new Vector4(0, 0, 0, 1);
            min.x = Mathf.Min(min.x, pos.x);
            min.y = Mathf.Min(min.y, pos.y);
            min.z = Mathf.Min(min.z, pos.z);
            max.x = Mathf.Max(max.x, pos.x);
            max.y = Mathf.Max(max.y, pos.y);
            max.z = Mathf.Max(max.z, pos.z);
        }

        Bounds bounds = new Bounds();
        bounds.SetMinMax(min, max);

        return bounds;
    }

    /// <summary>
    /// Record the bones' matrices of current  frame
    /// </summary>
    /// <param name="frame"></param>
    /// <returns></returns>
    private void SamplingCoroutine(GPUSkinningFrame frame)
    {
		//yield return new WaitForEndOfFrame();

        GPUSkinningBone[] bones = gpuSkinningAnimation.bones;
        int numBones = bones.Length;
        for(int i = 0; i < numBones; ++i)
        {
            Transform boneTransform = bones[i].transform;
            GPUSkinningBone currentBone = GetBoneByTransform(boneTransform);

            //bindpose先从mesh local->bone local->world->mesh local
            //-------------------蒙皮矩阵3步-----------------------
            //1.顶点从mesh space变换到bone space
            //2.在bone space乘以动画文件中的变换矩阵，在本例中直接利用LocalToWorldMatrix变换到world space
            //3.顶点从world space变换回mesh space
            //另外一种算法是在第二步bone space逐级计算，显然本例性能更优
            frame.matrices[i] = currentBone.transform.root.transform.worldToLocalMatrix * currentBone.transform.localToWorldMatrix * currentBone.bindpose;
        }

        ++samplingFrameIndex;
    }

	private Material[] CreateShaderAndMaterial(SkinnedMeshRenderer[] renders, string dir, string name)
	{
        var shader = Shader.Find("GPUSkinning/SimpleLit");
        if( shader == null)
        {
            Debug.LogError("not found GPUSkinning/SimpleLit shader!");
            return null;
        }

        List<Material> materialList = new List<Material>();
        foreach (var r in renders)
        {
            var materials = r.sharedMaterials;
            foreach (var mat in materials)
            {
                if(mat == null)
                    continue;

                Material mtrl = new Material(shader);
                mtrl.CopyPropertiesFromMaterial(mat);

                var matName = $"GPUSKinning_Material_{name}_{mat.name}";
                mtrl.name = matName;
                mtrl.enableInstancing = true;

                string savedMtrlPath = $"{dir}/{matName}.mat";
                var savedMat = CreateOrReplaceAsset<Material>(mtrl, savedMtrlPath);
                materialList.Add(savedMat);
            }
        }

        return materialList.ToArray();
    }
//-----------------------------------------------------------------------------------------------------

    public bool IsSamplingProgress()
    {
        return samplingClipIndex != -1;
    }

    public bool IsAnimatorOrAnimation()
    {
        return animator != null;
    }

    public bool IsSampling()
    {
        return isSampling;
    }


    private GPUSkinningBone GetBoneByTransform(Transform transform, bool searchByName = false)
	{
		GPUSkinningBone[] bones = gpuSkinningAnimation.bones;
		int numBones = bones.Length;
        for(int i = 0; i < numBones; ++i)
        {
            var match = false;
            if (searchByName)
                match = bones[i].transform.name == transform.name;
            else
                match = bones[i].transform == transform;

            if(match)
                return bones[i];
        }

        return null;
	}

    private int GetBoneIndex(GPUSkinningBone bone)
    {
        return System.Array.IndexOf(gpuSkinningAnimation.bones, bone);
    }

	public static void ShowDialog(string msg)
	{
		EditorUtility.DisplayDialog("GPUSkinning", msg, "OK");
	}

    T CreateOrReplaceAsset<T> (T asset, string path) where T: UnityEngine.Object
    {
        T existingAsset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existingAsset == null)
        {
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.ImportAsset(path);
            existingAsset = AssetDatabase.LoadAssetAtPath<T>(path);
        }
        else
        {
            EditorUtility.CopySerialized(asset, existingAsset);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
        }

        if (asset.hideFlags != existingAsset.hideFlags)
            existingAsset.hideFlags = asset.hideFlags;

        return existingAsset;
    }

    private string GetStateName(AnimatorController machine, AnimationClip clip)
    {
        foreach( var layer in machine.layers)
        {
            foreach (var childAnimatorState in layer.stateMachine.states)
            {
                if (childAnimatorState.state.motion != null && childAnimatorState.state.motion == clip)
                {
                    return childAnimatorState.state.name;
                }
            }
        }
       
        return null;
    }

    public AnimationClip[] GetAllClips()
    {
        return animClips;
    }

    private bool IsValid()
    {
        if (string.IsNullOrEmpty(animName.Trim()))
        {
            ShowDialog("Animation name is empty.");
            return false;
        }

        if (animClips == null || animClips.Length == 0)
        {
            ShowDialog("Please set Anim Clips.");
            return false;
        }

        AnimationClip animClip = animClips[samplingClipIndex];
        if (animClip == null)
        {
            isSampling = false;
            return false;
        }

        int numFrames = (int)(animClip.frameRate * animClip.length);
        if (numFrames == 0)
        {
            isSampling = false;
            return false;
        }

        if (skinnedMeshRenders == null || skinnedMeshRenders.Length <= 0)
        {
            ShowDialog("Cannot find SkinnedMeshRenderers.");
            return false;
        }

        foreach (var smr in skinnedMeshRenders)
        {
            if (smr.sharedMesh == null)
            {
                ShowDialog("Cannot find SkinnedMeshRenderer.mesh.");
                return false;
            }

            Mesh mesh = smr.sharedMesh;
            if (mesh == null)
            {
                ShowDialog("Missing Mesh");
                return false;
            }
        }

        return true;
    }
#endif
}
