using UnityEditor;
using UnityEngine;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Дымная тень соперника: текстуры из Tools/Blender/zonk_smoke.py, материал шейдера Zonk/ShadowSmoke (все числа дыма —
    /// в материале), плоскость в сцене. Единица шейдера — 0.9 метра, так задан размер плоскости.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string SmokeFolder = Root + "/Art/Textures/Smoke";
        private const float SmokeUnit = 0.9f;

        // Наклон к камере партии, чтобы голова не сплющивалась: камера смотрит сверху.
        private static readonly Quaternion SmokeTilt = Quaternion.Euler(20f, 0f, 0f);

        // Голова в локальных координатах соперника: так она в кадре партии над столом и ниже верхнего края экрана.
        private static readonly Vector3 ShadowHeadPoint = new Vector3(0f, 0.63f, -0.3f);

        private static Material ShadowSmokeMaterial()
        {
            AssetDatabase.Refresh();
            var noisePath = SmokeFolder + "/Shadow_Noise.png";
            var maskPath = SmokeFolder + "/Shadow_Body.png";
            ImportSmokeTexture(noisePath, TextureWrapMode.Repeat, true, TextureImporterCompression.Compressed);
            ImportSmokeTexture(maskPath, TextureWrapMode.Clamp, false, TextureImporterCompression.Uncompressed);

            var path = SmokeFolder + "/M_ShadowSmoke.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find("Zonk/ShadowSmoke");
            if (shader == null)
            {
                Debug.LogError("[Zonk] Shader Zonk/ShadowSmoke not found");
                return null;
            }

            material = new Material(shader) { name = "M_ShadowSmoke" };
            material.SetTexture("_Noise", AssetDatabase.LoadAssetAtPath<Texture2D>(noisePath));
            material.SetTexture("_Mask", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ImportSmokeTexture(string path, TextureWrapMode wrap, bool mipmaps, TextureImporterCompression compression)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                Debug.LogError("[Zonk] No texture " + path + ": run Tools/Blender/zonk_smoke.py");
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = wrap;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = mipmaps;
            importer.maxTextureSize = 256;
            importer.textureCompression = compression;
            importer.crunchedCompression = false;
            importer.SaveAndReimport();
        }

        /// <summary>Плоскость дыма: тот же размер, что в шейдере (7.2 × 5 единиц), голова на ней в единицах (0, 0).</summary>
        private static Renderer BuildShadowSmoke(Transform parent)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Smoke";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localScale = new Vector3(7.2f * SmokeUnit, 5f * SmokeUnit, 1f);
            quad.transform.localRotation = SmokeTilt;

            // От головы вниз по плоскости: голова на единицу выше центра плоскости.
            quad.transform.localPosition = ShadowHeadPoint + new Vector3(0f, 0f, 0.3f) - SmokeTilt * new Vector3(0f, SmokeUnit, 0f);

            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ShadowSmokeMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }
    }
}
