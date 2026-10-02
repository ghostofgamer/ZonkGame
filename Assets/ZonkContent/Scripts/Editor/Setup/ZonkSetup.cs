using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Object = UnityEngine.Object;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Генератор стартового проекта игры: материалы, префабы-заглушки из примитивов, весь контент (кости, скины,
    /// соперники, главы), сцены Bootstrap и Table, ссылки в ProjectContext и Build Settings.
    ///
    /// Ассеты создаются, только если их ещё нет: правки в инспекторе не затираются. Чтобы пересоздать
    /// заглушку, её удаляют и запускают генератор снова. Сцены пересоздаются всегда (после вопроса), кроме заблокированных:
    /// метка ZonkLocked защищает префаб окна или сцену от любого пересоздания (ZonkSetup.Locks).
    /// Заглушки заменяются настоящими моделями в конфигах предметов, код при этом не меняется.
    /// </summary>
    public static partial class ZonkSetup
    {
        public const string Root = "Assets/ZonkContent";
        public const string Models = Root + "/Art/Models";
        public const string Textures = Root + "/Art/Textures";
        public const string Materials = Root + "/Art/Materials";
        public const string Prefabs = Root + "/Prefabs";
        public const string Localization = Root + "/Localization";
        public const string ConfigsFolder = Root + "/Configs";
        public const string ScenesFolder = Root + "/Scenes";

        [MenuItem("Zonk/Setup/Build Everything", priority = 0)]
        public static void BuildEverythingMenu()
        {
            if (!EditorUtility.DisplayDialog("Zonk",
                    "Создать недостающие материалы, префабы и контент и пересоздать сцены Bootstrap и Table?",
                    "Создать", "Отмена"))
                return;

            BuildEverything();
        }

        [MenuItem("Zonk/Setup/Rebuild Scenes", priority = 1)]
        public static void RebuildScenesMenu()
        {
            if (!EditorUtility.DisplayDialog("Zonk", "Пересоздать сцены Bootstrap и Table?", "Пересоздать", "Отмена"))
                return;

            var content = BuildContent(BuildArt());
            BuildScenes(content);
        }

        /// <summary>
        /// Пересоздать префабы окон из генератора (экран загрузки, книга правил), когда поменялась их вёрстка в коде.
        /// Ручные правки этих префабов пропадут, поэтому сначала вопрос.
        /// </summary>
        [MenuItem("Zonk/Setup/Rebuild UI Prefabs", priority = 2)]
        public static void RebuildUiPrefabsMenu()
        {
            if (!EditorUtility.DisplayDialog("Zonk",
                    "Пересоздать окна и сцены? Окна со снимком (Save UI Snapshot) вернутся к сохранённому виду, заблокированные не изменятся, остальные построит генератор.",
                    "Пересоздать", "Отмена"))
                return;

            // Окно со снимком возвращается к сохранённому виду, заблокированное не трогается, остальные строит генератор.
            foreach (var name in WindowPrefabs)
                ResetWindow(name);

            BuildEverything();
        }

        /// <summary>Точка входа и для CLI: -executeMethod Zonk.Editor.Setup.ZonkSetup.BuildEverything</summary>
        public static void BuildEverything()
        {
            // Без StartAssetEditing: в пакетном режиме только что созданная папка ещё «не существует»
            // для AssetDatabase, и вложенная папка создавала бы дубликат «Prefabs 1».
            EnsureFolders();

            var art = BuildArt();
            var content = BuildContent(art);
            BuildPlayerLevel(content.Config);
            BuildAppearance(content.Config);
            BuildMeta(content.Config);
            BuildShopExpansion(content.Config);
            MigratePrefabPayloads();
            ApplyToonMaterials(content.Config);
            BuildWindows(content.Ui);
            AddPlayerLevelToWindows();
            AddAvatarToMainMenu();
            AddMetaButtonsToMainMenu();
            AssetDatabase.SaveAssets();
            ContentDatabaseBuilder.Rebuild();
            UiWindowRegistry.Rebuild();

            BuildScenes(content);
            SetupProjectContext(content);
            SetupBuildSettings();
            AssetDatabase.SaveAssets();

            ContentValidator.ValidateMenu();
            Debug.Log("[Zonk] Setup finished. Open " + ScenesFolder + "/Bootstrap.unity and press Play.");
        }

        private static void EnsureFolders()
        {
            foreach (var folder in new[]
                     {
                         Materials, Prefabs + "/Dice", Prefabs + "/Cups", Prefabs + "/Table", Prefabs + "/Lamps", Prefabs + "/Environments",
                         Prefabs + "/Accessories", Prefabs + "/UI", ScenesFolder, Localization,
                         ConfigsFolder + "/Game", ConfigsFolder + "/Rules", ConfigsFolder + "/Dice", ConfigsFolder + "/Currencies", ConfigsFolder + "/Cosmetics/Slots",
                         ConfigsFolder + "/Cosmetics/Items", ConfigsFolder + "/Themes", ConfigsFolder + "/Ai", ConfigsFolder + "/Reactions", ConfigsFolder + "/Opponents",
                         ConfigsFolder + "/Chapters", ConfigsFolder + "/Phrases", ConfigsFolder + "/Modes",
                     })
            {
                EnsureFolder(folder);
            }
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            // Папка есть на диске, но ещё не импортирована: импортируем, а не создаём вторую с номером.
            if (Directory.Exists(path))
            {
                AssetDatabase.ImportAsset(path);
                return;
            }

            var parent = ParentFolder(path);
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string ParentFolder(string path) => Path.GetDirectoryName(path)?.Replace('\\', '/');

        /// <summary>Все ассеты типа T в папке (незагрузившиеся пропускаются), в порядке AssetDatabase.FindAssets.</summary>
        public static List<T> FindAll<T>(string folder) where T : Object
        {
            var result = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null)
                    result.Add(asset);
            }

            return result;
        }

        /// <summary>Контент из конфигов по ID (без ID пропускается): для таблиц перенастройки.</summary>
        private static Dictionary<string, T> ContentById<T>() where T : ContentConfig
        {
            var result = new Dictionary<string, T>();
            foreach (var asset in FindAll<T>(ConfigsFolder))
            {
                if (!string.IsNullOrEmpty(asset.Id))
                    result[asset.Id] = asset;
            }

            return result;
        }

        /// <summary>Записать текстуру в PNG, уничтожить её и импортировать файл.</summary>
        private static void SavePng(string path, Texture2D texture)
        {
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
        }

        /// <summary>Текстура RGBA32 без мип-уровней из готовых пикселей.</summary>
        private static Texture2D TextureFrom(int width, int height, Color[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Загружает ассет или создаёт новый с начальной настройкой. Существующий не меняется.</summary>
        public static T Asset<T>(string path, Action<T> init) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            EnsureFolder(ParentFolder(path));
            var asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>Префаб из объекта, построенного build. Существующий префаб не пересоздаётся.</summary>
        public static GameObject Prefab(string path, Func<GameObject> build)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            EnsureFolder(ParentFolder(path));
            var go = build();
            try
            {
                return PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Примитив без коллайдера: заглушка модели.</summary>
        public static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale,
            Material material, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            if (material != null)
                go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public static GameObject Empty(string name, Transform parent, Vector3 position = default, Vector3 euler = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            return go;
        }
    }
}
