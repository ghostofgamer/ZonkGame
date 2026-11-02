using UnityEditor;
using UnityEngine;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Волна 3 особых костей (05.10.2026): у каждой своя идея и накладки другим материалом (Tools/Blender/zonk_dice.py,
    /// вторая часть меша) — оковка, камни, кайма. Материал накладок — DieConfig.LookAccent; ставится, пока поле пустое
    /// (свой материал, назначенный руками, не заменяется). Модели перечитываются (ConfigureModel в AssignSpecialDiceModels).
    /// </summary>
    public static partial class ZonkSetup
    {
        /// <summary>Материалы накладок: цвет, гладкость, металл.</summary>
        private static readonly (string Name, Color Color, float Smoothness, float Metallic)[] DieAccents =
        {
            ("Emerald", new Color(0.05f, 0.62f, 0.32f), 0.9f, 0.05f),
            ("Cobalt", new Color(0.08f, 0.2f, 0.7f), 0.85f, 0f),
            ("Silver", new Color(0.85f, 0.87f, 0.9f), 0.8f, 0.9f),
            ("Gold", new Color(1f, 0.74f, 0.25f), 0.8f, 0.9f),
            ("Iron", new Color(0.16f, 0.15f, 0.15f), 0.45f, 0.7f),
            ("Steel", new Color(0.42f, 0.44f, 0.47f), 0.7f, 0.9f),
            ("Worn", Color.white, 0.2f, 0f),          // текстура потёртости DiceLooks/DieAccent_Worn.png (zonk_dice.py)
        };

        /// <summary>Чьи накладки каким материалом. Костяная — без накладок (мослы того же вида, что и тело).</summary>
        private static readonly (string DieId, string Accent)[] DiceAccentsV3 =
        {
            ("die_lucky", "Emerald"), ("die_even", "Cobalt"), ("die_odd", "Silver"), ("die_sixes", "Gold"),
            ("die_fives", "Gold"), ("die_middle", "Iron"), ("die_edges", "Gold"), ("die_worn", "Worn"),
            ("die_sharper", "Steel"),
        };

        private static void BuildDiceV3()
        {
            var materials = new System.Collections.Generic.Dictionary<string, Material>();
            foreach (var (name, color, smoothness, metallic) in DieAccents)
            {
                var texturePath = DieLooksFolder + "/DieAccent_" + name + ".png";
                Texture2D texture = null;
                if (System.IO.File.Exists(texturePath))
                {
                    AssetDatabase.ImportAsset(texturePath);   // файл пишет Blender: Unity мог ещё не увидеть его
                    ConfigureTexture(texturePath, DieLookTextureSize);
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                }

                materials[name] = Mat(null, "DieAccent_" + name, color, texture, smoothness, metallic);
            }

            foreach (var (dieId, accent) in DiceAccentsV3)
            {
                var die = FindDie(dieId);
                // Потёртая в первом варианте волны 3 была с золотом — однотонная заливка сколов не понравилась (05.10).
                var replace = dieId == "die_worn" && die != null && die.LookAccent == materials["Gold"];
                if (die == null || (die.LookAccent != null && !replace))
                    continue;

                die.LookAccent = materials[accent];
                EditorUtility.SetDirty(die);
            }
        }
    }
}
