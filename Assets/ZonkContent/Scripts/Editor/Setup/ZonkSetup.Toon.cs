using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Комиксовый стиль: ассет ToonStyleConfig и перевод материалов игры между URP Lit и Zonk/Toon.
    /// Свойства у шейдеров названы одинаково, поэтому перевод туда и обратно настроек не теряет.
    /// Переводятся только непрозрачные материалы на URP Lit (или Zonk/Toon при возврате): ручной выбор другого
    /// шейдера не трогается. Светящиеся (лампочки, окна, небо, метки) остаются на Lit.
    /// </summary>
    public static partial class ZonkSetup
    {
        public const string ToonShader = "Zonk/Toon";

        /// <summary>Светящиеся и фоновые материалы: на Toon не переводятся.</summary>
        private static readonly string[] ToonSkip = { "Bulb", "Window", "Sky", "Sun", "Eyes", "Ring", "Candle", "Glow" };

        /// <summary>Большие поверхности: без контура и кромки (кромка на плоскости вдали — светлая полоса).</summary>
        private static readonly string[] ToonFlat = { "Felt", "Floor", "Wall", "Rug", "Sand", "Sea", "Deck", "Sail" };

        private static void BuildToonStyle(GameConfig config)
        {
            var style = Asset<ToonStyleConfig>(ConfigsFolder + "/Game/ToonStyle.asset", s => { });
            if (config.ToonStyle == null)
            {
                config.ToonStyle = style;
                EditorUtility.SetDirty(config);
            }
        }

        /// <summary>Генератор: материалы на Toon, если это включено в стиле.</summary>
        private static void ApplyToonMaterials(GameConfig config)
        {
            if (config != null && config.ToonStyle != null && config.ToonStyle.ToonMaterials)
                ConvertMaterials(true);
        }

        [MenuItem("Zonk/Art/Materials: Zonk Toon", priority = 40)]
        public static void ToonMaterialsMenu()
        {
            SetToonMaterialsFlag(true);
            Debug.Log("[Zonk] Materials switched to " + ToonShader + ": " + ConvertMaterials(true));
        }

        [MenuItem("Zonk/Art/Materials: URP Lit", priority = 41)]
        public static void LitMaterialsMenu()
        {
            SetToonMaterialsFlag(false);
            Debug.Log("[Zonk] Materials switched to " + LitShader + ": " + ConvertMaterials(false));
        }

        private static void SetToonMaterialsFlag(bool toon)
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigsFolder + "/Game/GameConfig.asset");
            if (config == null || config.ToonStyle == null || config.ToonStyle.ToonMaterials == toon)
                return;

            config.ToonStyle.ToonMaterials = toon;
            EditorUtility.SetDirty(config.ToonStyle);
        }

        /// <summary>Перевести материалы из Art (вместе с видами костей); вернуть число переведённых.</summary>
        private static int ConvertMaterials(bool toon)
        {
            var toonShader = Shader.Find(ToonShader);
            var litShader = Shader.Find(LitShader);
            if (toonShader == null || litShader == null)
            {
                Debug.LogError("[Zonk] Shader not found: " + (toonShader == null ? ToonShader : LitShader));
                return 0;
            }

            var from = toon ? litShader : toonShader;
            var to = toon ? toonShader : litShader;
            var count = 0;
            foreach (var material in FindAll<Material>(Root + "/Art"))
            {
                if (material.shader != from || NameHas(material.name, ToonSkip))
                    continue;
                if (material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f)
                    continue;

                material.shader = to;
                if (toon)
                {
                    var flat = NameHas(material.name, ToonFlat);
                    material.SetFloat("_OutlineScale", flat ? 0f : 1f);
                    material.SetFloat("_RimScale", flat ? 0f : 1f);
                }

                EditorUtility.SetDirty(material);
                count++;
            }

            AssetDatabase.SaveAssets();
            return count;
        }

        private static bool NameHas(string name, string[] parts)
        {
            foreach (var part in parts)
            {
                if (name.Contains(part))
                    return true;
            }

            return false;
        }
    }
}
