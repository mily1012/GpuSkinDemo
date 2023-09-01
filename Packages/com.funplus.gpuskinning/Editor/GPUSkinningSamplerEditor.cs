using UnityEngine;
using UnityEditor;
using System.IO;

public class GPUSkinningSamplerEditor : EditorWindow
{
    GameObject selectedObject;
    GameObject selectedParts;
    bool onlyExportSelecctedMesh = false;
    int selectedSkinMeshIndex = -1;
    bool onlyExportParts = false;

    GPUSkinningSampler sampler;
    GameObject instObject;

    string folderPath;
    string partsFolderPath;

    [MenuItem("Assets/ReUploadMesh")]
    static void ReUploadMesh()
    {
        var selectObject = Selection.activeObject;
        Mesh mesh = (Mesh) selectObject;
        if (mesh != null)
        {
            var assetPath = AssetDatabase.GetAssetPath(mesh);
            if(Path.GetExtension(assetPath) != ".asset")
                return;

            var isReadable = mesh.isReadable;
            var text = File.ReadAllText(assetPath);

            if (!isReadable)
            {
                text = text.Replace("m_IsReadable: 0", "m_IsReadable: 1");
                text = text.Replace("m_IsReadable: 1", "m_IsReadable: 0");
            }
            else
            {
                text = text.Replace("m_IsReadable: 1", "m_IsReadable: 0");
                text = text.Replace("m_IsReadable: 0", "m_IsReadable: 1");
            }
            File.WriteAllText(assetPath, text);
        }
    }

    [MenuItem("Window/GPUSkinning Baker")]
    static void Init()
    {
        var window = (GPUSkinningSamplerEditor)EditorWindow.GetWindow(typeof(GPUSkinningSamplerEditor));
        window.ShowUtility();
    }

    void OnGUI()
    {
        int height = 3;

        EditorGUI.BeginChangeCheck();
        selectedObject = (GameObject)EditorGUI.ObjectField(new Rect(3, height, position.width - 6, 20), "Select Charactor", selectedObject, typeof(GameObject));
        height += 25;

        // End the code block and reset folderpath if a change occurred
        if (EditorGUI.EndChangeCheck())
        {
            folderPath = "";
            selectedSkinMeshIndex = -1;
        }

        if (selectedObject != null)
        {
            onlyExportSelecctedMesh = GUI.Toggle(new Rect(3, height, 300, 20), onlyExportSelecctedMesh, "Only export one skinmesh from character");
            height += 25;

            if(onlyExportSelecctedMesh)
            {
                var skinnedMeshRenderers = selectedObject.GetComponentsInChildren<SkinnedMeshRenderer>();

                string[] skinMeshNames = new string[skinnedMeshRenderers.Length];
                int idx = 0;
                int boneLen = -1;
                int perfectIndex = 0;
                foreach (var renderer in skinnedMeshRenderers)
                {
                    skinMeshNames[idx] = renderer.name;
                    if (boneLen < renderer.bones.Length)
                    {
                        boneLen = renderer.bones.Length;
                        perfectIndex = idx;
                    }

                    idx++;
                }

                if (selectedSkinMeshIndex == -1)
                    selectedSkinMeshIndex = perfectIndex;

                if (selectedSkinMeshIndex >= skinnedMeshRenderers.Length)
                    selectedSkinMeshIndex = skinnedMeshRenderers.Length - 1;

                selectedSkinMeshIndex = EditorGUI.Popup(new Rect(3, height, position.width - 6, 20), selectedSkinMeshIndex, skinMeshNames);
                height += 25;
            }

            if (string.IsNullOrEmpty(folderPath))
            {
                var assetPath = AssetDatabase.GetAssetPath(selectedObject);
                folderPath  = System.IO.Path.GetDirectoryName(assetPath);
            }

            GUI.TextArea(new Rect(3, height, 350, 20), folderPath);

            if (GUI.Button(new Rect(360, height, 150, 20), "Set Character's Path"))
            {
                var openedPath = EditorUtility.OpenFolderPanel("Select Target Path", folderPath, "");
                if (!string.IsNullOrEmpty(openedPath))
                {
                    folderPath = "Assets"  + openedPath.Substring(Application.dataPath.Length);
                }
            }

            height += 25;
        }

        // select parts
        EditorGUI.BeginChangeCheck();
        selectedParts = (GameObject)EditorGUI.ObjectField(new Rect(3, height, position.width - 6, 20), "Select Parts", selectedParts, typeof(GameObject));
        height += 25;

        // End the code block and reset folderpath if a change occurred.
        if (EditorGUI.EndChangeCheck())
        {
            partsFolderPath = "";
        }

        if (selectedParts != null)
        {
            onlyExportParts = GUI.Toggle(new Rect(3, height, 300, 20), onlyExportParts, "Only export parts' data");
            height += 25;

            if (string.IsNullOrEmpty(partsFolderPath))
            {
                var assetPath = AssetDatabase.GetAssetPath(selectedParts);
                partsFolderPath  = System.IO.Path.GetDirectoryName(assetPath);
            }

            GUI.TextArea(new Rect(3, height, 350, 20), partsFolderPath);

            if (GUI.Button(new Rect(360, height, 150, 20), "Set Parts' Path"))
            {
                var openedPath = EditorUtility.OpenFolderPanel("Select Target Path", partsFolderPath, "");
                if (!string.IsNullOrEmpty(openedPath))
                {
                    partsFolderPath = "Assets"  + openedPath.Substring(Application.dataPath.Length);
                }
            }

            height += 25;
        }

        if (GUI.Button(new Rect(3, height, position.width - 6, 20), "Bake"))
        {
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("Notice", "Charactor is not selected.", "Yes");
                return;
            }

            var animator = selectedObject.GetComponent<Animator>();
            if (animator == null)
            {
                EditorUtility.DisplayDialog("Notice", "There is not \"Animator\" component in the charactor which you want to bake.", "Yes");
                return;
            }

            var renderers = selectedObject.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (renderers == null || renderers.Length <= 0)
            {
                EditorUtility.DisplayDialog("Notice", "There are not any \"SkinnedMeshRenderer\" components in the charactor which you want to bake.", "Yes");
                return;
            }

            DestroyInstObject();
            QualitySettings.skinWeights = SkinWeights.TwoBones;

            instObject = GameObject.Instantiate(selectedObject);
            sampler = instObject.GetComponent<GPUSkinningSampler>();
            if (sampler == null)
                sampler = instObject.AddComponent<GPUSkinningSampler>();

            sampler.Init(onlyExportSelecctedMesh, selectedSkinMeshIndex,folderPath,selectedObject.name);
            sampler.BeginSample();
            sampler.StartSample();

            if (selectedParts != null)
            {
                sampler.ExportOnlyMeshAndMaterial(selectedParts, partsFolderPath);

                if (onlyExportParts)
                {
                    sampler = null;
                    DestroyInstObject();
                }
            }
        }
    }

    void Update()
    {
        if (sampler == null)
            return;

        if (!sampler.IsSampling() && sampler.IsSamplingProgress())
        {
            if (++sampler.samplingClipIndex < sampler.GetAllClips().Length)
            {
                sampler.StartSample();
            }
            else
            {
                OnFinish();
                sampler.EndSample();
                EditorUtility.ClearProgressBar();
            }
        }

        sampler.CustomUpdate();

        if (sampler.IsSampling())
        {
            AnimationClip animClip = sampler.GetAllClips()[sampler.samplingClipIndex];
            string msg = animClip.name + "(" + (sampler.samplingClipIndex + 1) + "/" +
                         sampler.GetAllClips().Length + ")";

            int totalFrams = (int)(animClip.length * animClip.frameRate);
            EditorUtility.DisplayProgressBar("Sampling, DONOT stop playing", msg, (float) (sampler.samplingFrameIndex + 1) / totalFrams);
        }
    }

    private void OnDestroy()
    {
        EditorUtility.ClearProgressBar();
    }

    private void OnFinish()
    {
        QualitySettings.skinWeights = SkinWeights.TwoBones;
        DestroyInstObject();
    }

    private void DestroyInstObject()
    {
        if (instObject != null)
        {
            GameObject.DestroyImmediate(instObject, true);
            instObject = null;
        }
    }
}