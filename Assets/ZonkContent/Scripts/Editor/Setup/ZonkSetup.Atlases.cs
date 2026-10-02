using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Атласы спрайтов интерфейса (Sprite Atlas V2): картинки, которые рисуются вместе, — в одной текстуре. Меньше вызовов
    /// отрисовки (сетка аватаров, ступени сезона, узлы талантов) и лучше сжатие: много мелких картинок вместе с Crunch
    /// весят меньше, чем по отдельности.
    /// - Atlas_UI: значки из Art/Sprites (монета, звёзды, сундук, кубок, грани костей, книга правил) и иконки талантов.
    /// - Atlas_Look: аватары и рамки игрока.
    /// Портреты соперников и текстуры 3D-моделей в атласы не входят. Новые картинки этих папок попадают в атлас сами
    /// (папка — целиком; картинки в корне Sprites — добавляются при каждом запуске генератора).
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string AtlasesFolder = Root + "/Art/Atlases";
        private const int AtlasMaxSize = 2048;

        private static void BuildSpriteAtlases()
        {
            // Без режима V2 атласы не собираются в сборку и не используются в редакторе.
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2;

            EnsureFolder(AtlasesFolder);

            var ui = new List<Object>();
            foreach (var path in Directory.GetFiles(Root + "/Art/Sprites", "*.png", SearchOption.TopDirectoryOnly))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path.Replace('\\', '/'));
                if (sprite != null)
                    ui.Add(sprite);
            }

            AddFolder(ui, Root + "/Art/Sprites/Talents");
            Atlas("Atlas_UI", ui);

            var look = new List<Object>();
            AddFolder(look, AvatarsFolder);
            AddFolder(look, FramesFolder);
            Atlas("Atlas_Look", look);
        }

        /// <summary>
        /// Надписи интерфейса без шрифта игры (стандартный LiberationSans у TMP — без кириллицы: в браузере русский текст
        /// пропадает) получают шрифт Noto Sans из UiConfig. Проходит все префабы Prefabs/UI и детали; заблокированные — нет.
        /// </summary>
        private static void FixUiFonts()
        {
            if (_ui == null || _ui.Font == null)
                return;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs + "/UI" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (IsLocked(path))
                    continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var changed = false;
                    foreach (var text in root.GetComponentsInChildren<TMPro.TMP_Text>(true))
                    {
                        if (text.font == _ui.Font || text.font == _ui.BoldFont)
                            continue;

                        text.font = _ui.Font;
                        text.fontSharedMaterial = _ui.Font.material;
                        changed = true;
                    }

                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        Debug.Log("[Zonk] " + path + ": text font set to " + _ui.Font.name);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void AddFolder(List<Object> list, string folder)
        {
            var asset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(folder);
            if (asset != null)
                list.Add(asset);
        }

        /// <summary>
        /// Атлас собирается из своих папок заново при каждом запуске (руками добавленное в него — потеряется: класть картинки
        /// в папки). Настройки упаковки и сжатия ставятся при создании и живут в .meta — повторная запись их не трогает.
        /// </summary>
        private static void Atlas(string name, List<Object> packables)
        {
            var path = AtlasesFolder + "/" + name + ".spriteatlasv2";
            var exists = File.Exists(path);
            var atlas = new SpriteAtlasAsset();
            atlas.Add(packables.ToArray());
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path);

            if (!exists && AssetImporter.GetAtPath(path) is SpriteAtlasImporter importer)
            {
                // Без поворота и плотной упаковки: картинки интерфейса — прямоугольники Image.
                importer.packingSettings = new SpriteAtlasPackingSettings
                {
                    padding = 4,
                    enableRotation = false,
                    enableTightPacking = false,
                    enableAlphaDilation = true,
                };
                importer.textureSettings = new SpriteAtlasTextureSettings
                {
                    generateMipMaps = false,
                    sRGB = true,
                    filterMode = FilterMode.Bilinear,
                    readable = false,
                };
                var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
                platform.maxTextureSize = AtlasMaxSize;
                platform.textureCompression = TextureImporterCompression.Compressed;
                platform.crunchedCompression = true;
                platform.compressionQuality = 75;
                importer.SetPlatformSettings(platform);
                importer.includeInBuild = true;
                importer.SaveAndReimport();
            }
        }
    }
}
