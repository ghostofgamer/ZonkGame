using System.IO;
using Base.Platform.Installers;
using UnityEditor;
using UnityEngine;
using Zenject;

namespace Base.Editor
{
    /// <summary>
    /// Создаёт Assets/Resources/ProjectContext.prefab с PlatformInstaller.
    /// Zenject поднимает ProjectContext из Resources автоматически при старте любой сцены с SceneContext.
    /// </summary>
    public static class ProjectContextCreator
    {
        private const string Folder = "Assets/Resources";
        private const string Path = Folder + "/ProjectContext.prefab";

        [MenuItem("Base/Setup/Create ProjectContext", priority = 0)]
        public static void Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
            if (existing != null)
            {
                Debug.Log("[Base] ProjectContext already exists: " + Path);
                Selection.activeObject = existing;
                return;
            }

            if (!AssetDatabase.IsValidFolder(Folder))
                Directory.CreateDirectory(Folder);

            var go = new GameObject("ProjectContext");
            try
            {
                var context = go.AddComponent<ProjectContext>();
                var installer = go.AddComponent<PlatformInstaller>();
                context.Installers = new MonoInstaller[] { installer };

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, Path);
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                Debug.Log("[Base] Created " + Path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
