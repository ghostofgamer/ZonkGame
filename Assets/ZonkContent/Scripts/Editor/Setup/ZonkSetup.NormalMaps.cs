using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Карты нормалей (05.10): стаканы, столы, безделушки. Карту рисует Tools/Blender/zonk_normals.py из текстуры цвета
    /// (тёмные линии — канавки, средний рельеф — слабее) и кладёт рядом: Cup_Wicker.png → Cup_Wicker_N.png, вдвое меньше.
    /// Здесь: импорт как Normal Map (Crunch, мип-уровни) и подключение к материалу, у которого цвет — эта текстура
    /// (_BumpMap, ключ _NORMALMAP — его понимают и Zonk/Toon, и URP Lit). Сила — _BumpScale материала: ставится один раз,
    /// дальше правится в материале. Новая вещь получает рельеф сама, если у её текстуры есть _N.png.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const float DefaultNormalStrength = 1f;
        // Только стаканы: на столах и безделушках (05.10) разницы на глаз нет, а вес растёт.
        private static readonly string[] NormalMapFolders = { "Cups" };

        private static void ApplyNormalMaps()
        {
            var count = 0;
            foreach (var material in FindAll<Material>(Root + "/Art"))
            {
                if (!material.HasProperty("_BaseMap") || !material.HasProperty("_BumpMap"))
                    continue;

                var albedo = material.GetTexture("_BaseMap");
                var albedoPath = albedo != null ? AssetDatabase.GetAssetPath(albedo) : null;
                if (string.IsNullOrEmpty(albedoPath) || !InNormalMapFolder(albedoPath))
                    continue;

                var normalPath = albedoPath.Substring(0, albedoPath.Length - Path.GetExtension(albedoPath).Length) + "_N.png";
                if (!File.Exists(normalPath))
                    continue;

                ConfigureNormalMap(normalPath);
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normal == null || (material.GetTexture("_BumpMap") == normal && material.IsKeywordEnabled("_NORMALMAP")))
                    continue;

                var first = material.GetTexture("_BumpMap") == null;
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                if (material.HasProperty("_UseNormalMap"))
                    material.SetFloat("_UseNormalMap", 1f);
                if (first && material.HasProperty("_BumpScale"))
                    material.SetFloat("_BumpScale", DefaultNormalStrength);
                EditorUtility.SetDirty(material);
                count++;
            }

            if (count > 0)
                Debug.Log("[Zonk] Normal maps connected: " + count);
        }

        private static bool InNormalMapFolder(string path)
        {
            foreach (var folder in NormalMapFolders)
            {
                if (path.StartsWith(Textures + "/" + folder + "/"))
                    return true;
            }

            return false;
        }

        /// <summary>Карта нормалей: тип Normal Map, размер как у файла (он уже вдвое меньше цвета), Crunch, мип-уровни.</summary>
        private static void ConfigureNormalMap(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;

            if (importer.textureType == TextureImporterType.NormalMap && importer.crunchedCompression && importer.mipmapEnabled &&
                importer.maxTextureSize == 512)
                return;

            importer.textureType = TextureImporterType.NormalMap;
            importer.convertToNormalmap = false;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.compressionQuality = TextureCrunchQuality;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }
}
