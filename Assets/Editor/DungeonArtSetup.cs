using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DungeonArtSetup
{
    private const string Root = "Assets/Resources/DungeonArt/";

    [MenuItem("Tools/Dungeon Art/Configure SampleScene")]
    public static void Configure()
    {
        AssetDatabase.Refresh();
        var importer = (TextureImporter)AssetImporter.GetAtPath(Root + "DungeonTileset.png");
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "DungeonTileset.png");
        CreateMaterial("SpriteMaterial", "Sprites/Default", atlas);
        CreateMaterial("FloorMaterial", "Universal Render Pipeline/Unlit", atlas);

        var scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var camera = Camera.main;
        camera.orthographic = true;
        camera.orthographicSize = 8.5f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.05f, 0.075f);
        camera.allowMSAA = false;
        camera.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
        var follow = camera.GetComponent<CameraFollow>();
        follow.Offset = new Vector3(0f, 18f, -10.3923f);
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (player != null) camera.transform.position = player.transform.position + follow.Offset;
        if (UnityEngine.Object.FindAnyObjectByType<DungeonArenaVisual>() == null)
            new GameObject("DungeonArenaVisual").AddComponent<DungeonArenaVisual>();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[DungeonArt] Scene and materials configured.");
    }

    private static void CreateMaterial(string name, string shaderName, Texture2D atlas)
    {
        string path = Root + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find(shaderName));
            AssetDatabase.CreateAsset(material, path);
        }
        material.mainTexture = atlas;
        EditorUtility.SetDirty(material);
    }
}
