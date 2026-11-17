using System.IO;
using UnityEditor;
using UnityEngine;

namespace PartyGame.EditorTools
{
    /// <summary>
    /// Menu: Party Game > Create Content Assets.
    /// Saves the built-in default content as editable assets so designers can tune guests,
    /// activities, occasions and dialogue in the Inspector without touching code.
    /// </summary>
    public static class ContentAssetGenerator
    {
        const string Root = "Assets/PartyGame/Content";
        const string ResourcesDir = "Assets/PartyGame/Resources";

        [MenuItem("Party Game/Create Content Assets")]
        public static void Create()
        {
            string mainPath = $"{ResourcesDir}/{GameContent.ResourcePath}.asset";
            if (File.Exists(mainPath) &&
                !EditorUtility.DisplayDialog("Overwrite content?",
                    "Content assets already exist. Overwrite them with the built-in defaults? Your edits will be lost.",
                    "Overwrite", "Cancel"))
                return;

            var content = DefaultContent.Build();

            foreach (var o in content.occasions) Save(o, "Occasions", o.id);
            foreach (var g in content.guests) Save(g, "Guests", g.id);
            foreach (var a in content.activities) Save(a, "Activities", a.id);
            Save(content.dialogue, "", "Dialogue");

            EnsureFolder(ResourcesDir);
            AssetDatabase.DeleteAsset(mainPath);
            content.name = GameContent.ResourcePath;
            AssetDatabase.CreateAsset(content, mainPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = content;
            EditorGUIUtility.PingObject(content);
            Debug.Log($"[Party Game] Content assets created. Edit them in {Root}, tuning numbers on {mainPath}.");
        }

        static void Save(ScriptableObject so, string sub, string fileName)
        {
            string dir = string.IsNullOrEmpty(sub) ? Root : $"{Root}/{sub}";
            EnsureFolder(dir);
            string path = $"{dir}/{fileName}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(so, path);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
