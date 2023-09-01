using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Runtime.Serialization.Json;

[ScriptedImporter( 1, "desc" )]
public class AnimationTextureDescImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        var jsonText = File.ReadAllText(ctx.assetPath);

        var textureDescAsset = ScriptableObject.CreateInstance<AnimationTextureDescAsset>();
        string error = "";
        try
        {
            textureDescAsset.desc = JsonUtility.FromJson<AnimationTextureDesc>(jsonText);
        }
        catch (Exception e)
        {
            error = $"AnimationTextureDescImporter error: {e.Message}";
        }

        if (!string.IsNullOrEmpty(error))
            textureDescAsset.parseError = error;

        ctx.AddObjectToAsset("mainObj", textureDescAsset);
        ctx.SetMainObject(textureDescAsset);
    }
}

[CustomEditor(typeof(AnimationTextureDescAsset))]
internal class DeviceInfoAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var asset = serializedObject.targetObject as AnimationTextureDescAsset;
        if (!string.IsNullOrEmpty(asset.parseError))
        {
            EditorGUILayout.HelpBox(asset.parseError, MessageType.Error);
        }
        else
        {
            EditorGUILayout.LabelField("texturePath: " + asset.desc.texturePath);
            EditorGUILayout.LabelField("contentHash: "+asset.desc.contentHash);
        }
    }
}