using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PartyGame.EditorTools
{
    /// <summary>
    /// Creates Assets/PartyGame/Scenes/PartyRoom.unity: the grey-box party room as normal scene objects
    /// (floor, walls, one prop group per activity, guest spots, camera, light) plus its materials.
    /// Run once, then edit the scene by hand. Running it again overwrites the scene.
    /// </summary>
    public static class PartyRoomSceneBuilder
    {
        public const string ScenePath = "Assets/PartyGame/Scenes/PartyRoom.unity";
        const string MaterialDir = "Assets/PartyGame/Art/Materials";

        [MenuItem("Party Game/Build Party Room Scene")]
        static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild party room?",
                    "This overwrites " + ScenePath + " and any changes made to it.", "Overwrite", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>For batch mode: Unity.exe -batchmode -executeMethod PartyGame.EditorTools.PartyRoomSceneBuilder.BuildBatch</summary>
        public static void BuildBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        static void Build()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Directory.CreateDirectory(MaterialDir);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();

            var stage = RoomBuilder.Build(cam);
            SaveMaterials(stage.gameObject);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            BuildSettingsHelper.EnsureFirst(ScenePath);
            Debug.Log("[Party Game] Built " + ScenePath);
        }

        /// <summary>Turns the in-memory room materials into assets so the scene can reference them.</summary>
        static void SaveMaterials(GameObject root)
        {
            var saved = new Dictionary<Material, Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || EditorUtility.IsPersistent(m)) continue;
                if (!saved.TryGetValue(m, out var asset))
                {
                    string path = $"{MaterialDir}/{m.name}.mat";
                    asset = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (asset == null)
                    {
                        AssetDatabase.CreateAsset(m, path);
                        asset = m;
                    }
                    else asset.CopyPropertiesFromMaterial(m);
                    saved[m] = asset;
                }
                r.sharedMaterial = asset;
            }
        }
    }

    /// <summary>Keeps the party room scene first in Build Settings, so Play / builds start in the room.</summary>
    [InitializeOnLoad]
    static class BuildSettingsHelper
    {
        static BuildSettingsHelper()
        {
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(PartyRoomSceneBuilder.ScenePath)) EnsureFirst(PartyRoomSceneBuilder.ScenePath);
            };
        }

        public static void EnsureFirst(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(s => s.path == path);
            if (index == 0 && scenes[0].enabled) return;
            if (index > 0) scenes.RemoveAt(index);
            if (index != 0) scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            else scenes[0] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
