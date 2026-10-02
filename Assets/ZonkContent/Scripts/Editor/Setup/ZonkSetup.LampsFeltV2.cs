using System.IO;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Волна 2, лампы и сукно (02.10.2026, по отзыву: «однотонные белые лампы странные, изумрудная сливается, у сукна хочется
    /// больше идей»). Перекраски абажура и заглушки из примитивов заменены своими моделями (Tools/Blender/zonk_lamps.py,
    /// LAMPS_V2), ID и товары те же; плюс три новые лампы. Сукна-перекраски переведены на узоры (Tools/Blender/zonk_felt.py):
    /// плитки с повтором и целые полотна во всё сукно; плюс два новых сукна.
    /// Замена у существующих вещей — один раз (ShopLayoutVersion &lt; 6): ручная правка после этого не затирается.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string FeltV2Folder = Root + "/Art/Textures/Felt";
        private const int WideFeltTextureSize = 1024;

        /// <summary>Лампы: модель, ID вещи (старая — заменить вид, новая — создать), гладкость, металл, свет.</summary>
        private static readonly (string Name, string Id, float Smoothness, float Metallic, Color Light, float Intensity, float Range)[]
            LampsV2 =
            {
                ("Lamp_Shade", "lamp_basic", 0.55f, 0.1f, new Color(1f, 0.86f, 0.66f), 3.4f, 9f),
                ("Lamp_Storm", "lamp_lantern", 0.4f, 0.4f, new Color(1f, 0.72f, 0.42f), 3.2f, 8f),
                ("Lamp_Brass", "lamp_brass", 0.6f, 0.8f, new Color(1f, 0.8f, 0.55f), 3.6f, 9f),
                ("Lamp_Crystal", "lamp_crystal", 0.7f, 0.5f, new Color(0.85f, 0.92f, 1f), 3.6f, 9f),
                ("Lamp_Paper", "lamp_paper", 0.2f, 0f, new Color(1f, 0.5f, 0.32f), 3.0f, 8f),
                ("Lamp_Emerald", "lamp_glass", 0.4f, 0.4f, new Color(0.55f, 1f, 0.62f), 3.2f, 8f),
                ("Lamp_Fringe", "lamp_fringe", 0.3f, 0.2f, new Color(1f, 0.7f, 0.5f), 3.2f, 8f),
                ("Lamp_Ship", "lamp_ship", 0.5f, 0.6f, new Color(1f, 0.74f, 0.44f), 3.4f, 9f),
                ("Lamp_Fireflies", "lamp_fireflies", 0.4f, 0.4f, new Color(0.85f, 1f, 0.5f), 2.8f, 7f),
            };

        /// <summary>Новое название старой вещи, если старое не подходит к новому виду («Лампа зелёного стекла» → изумрудный фонарь).</summary>
        private static readonly (string Id, string NameKey)[] RenamedV2 =
        {
            ("lamp_glass", "item.lamp_emerald"),
            ("felt_red", "item.felt_leather"),
            ("felt_blue", "item.felt_constellations"),
            ("felt_teal", "item.felt_map"),
            ("felt_purple", "item.felt_rug"),
            ("felt_gold", "item.felt_board"),
        };

        /// <summary>Сукно: текстура (zonk_felt.py), ID вещи, плитка с повтором или полотно во всё сукно, гладкость.</summary>
        private static readonly (string Texture, string Id, bool Tiled, float Smoothness)[] FeltsV2 =
        {
            ("Felt_Leather", "felt_red", true, 0.35f),
            ("Felt_Constellations", "felt_blue", true, 0.1f),
            ("Felt_TreasureMap", "felt_teal", true, 0.1f),
            ("Felt_Rug", "felt_purple", false, 0.05f),
            ("Felt_Velvet", "felt_royal", false, 0.3f),
            ("Felt_Board", "felt_gold", false, 0.15f),
            ("Felt_Club", "felt_club", true, 0.05f),
            ("Felt_Suits", "felt_suits", true, 0.05f),
        };

        /// <summary>Цены новых вещей волны 2 (у заменённых цена прежняя).</summary>
        private static readonly (string id, Get how, int value)[] LampsFeltLayoutV6 =
        {
            ("lamp_fringe", Get.Coins, 2500),
            ("lamp_ship", Get.Ads, 6),
            ("lamp_fireflies", Get.Play, FromChest),
            ("felt_club", Get.Coins, 1800),
            ("felt_suits", Get.Ads, 4),
        };

        static partial void BuildLampsFeltV2(GameConfig config)
        {
            var firstTime = config.ShopLayoutVersion < 6;
            BuildLampsV2(firstTime);
            BuildFeltsV2(firstTime);

            if (firstTime)
            {
                foreach (var (id, nameKey) in RenamedV2)
                {
                    var item = ItemAt(id);
                    if (item == null || item.NameKey == nameKey)
                        continue;
                    item.NameKey = nameKey;
                    EditorUtility.SetDirty(item);
                }
            }

            ApplyShopLayoutV6(LampsFeltLayoutV6, config);
        }

        private static void BuildLampsV2(bool firstTime)
        {
            var lampSlot = SlotAt("lamp");
            if (lampSlot == null)
                return;

            var order = 40;
            foreach (var (name, id, smoothness, metallic, light, intensity, range) in LampsV2)
            {
                var prefab = ImportedPrefab("Lamps", name, smoothness, metallic, LampTextureSize, light);
                if (prefab == null)
                    continue;
                EnsureLampLight(prefab, name, light, intensity, range);

                var item = ItemAt(id);
                if (item == null)
                {
                    Item(id, lampSlot, order++, new PrefabPayload { Prefab = prefab });
                    continue;
                }

                // Перекраска абажура или заглушка из примитивов → своя модель (один раз).
                if (firstTime && !(item.Payload is PrefabPayload own && own.Prefab == prefab))
                {
                    item.Payload = new PrefabPayload { Prefab = prefab };
                    EditorUtility.SetDirty(item);
                }
            }
        }

        private static void BuildFeltsV2(bool firstTime)
        {
            var traySlot = SlotAt("tray");
            if (traySlot == null)
                return;

            var order = 20;
            foreach (var (textureName, id, tiled, smoothness) in FeltsV2)
            {
                var path = FeltV2Folder + "/" + textureName + ".png";
                if (!File.Exists(path))
                    continue;

                if (tiled)
                    ConfigureFeltTexture(path);
                else
                    ConfigureWideFeltTexture(path);

                var material = Mat(null, textureName, Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(path), smoothness);
                var tiling = tiled ? FeltTiling : Vector2.one;
                if (material.GetTextureScale("_BaseMap") != tiling)
                {
                    material.SetTextureScale("_BaseMap", tiling);
                    EditorUtility.SetDirty(material);
                }

                var item = ItemAt(id);
                if (item == null)
                {
                    Item(id, traySlot, order++, new MaterialPayload { Material = material });
                    continue;
                }

                if (firstTime && !(item.Payload is MaterialPayload same && same.Material == material))
                {
                    item.Payload = new MaterialPayload { Material = material };
                    EditorUtility.SetDirty(item);
                }
            }
        }

        /// <summary>Полотно во всё сукно: без повтора (Clamp), до 1024, Crunch.</summary>
        private static void ConfigureWideFeltTexture(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
                return;
            if (importer.wrapMode == TextureWrapMode.Clamp && importer.maxTextureSize == WideFeltTextureSize && importer.crunchedCompression)
                return;

            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = WideFeltTextureSize;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.SaveAndReimport();
        }
    }
}
