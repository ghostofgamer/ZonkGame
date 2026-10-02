using System.Linq;
using Base.Services.Quality;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Модели магазина из скриптов Blender (02.10.2026): лампы (zonk_lamps.py), безделушки на столе (zonk_decor.py),
    /// новые стаканы (zonk_cups.py). Префаб: модель с основным материалом (Toon) и, если есть второй слот, светящимся
    /// (Glow_*, остаётся URP Lit с эмиссией); у ламп — точечный свет в маркере LightPoint (без теней, SceneLight).
    /// Предметы магазина — здесь же, цены — раскладкой v5. Нет FBX — предмет не создаётся (скрипт Blender не запускали).
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string DecorSlotId = "decor";
        private const int LampTextureSize = 1024;
        private const int DecorTextureSize = 512;

        private static readonly (string Name, string Id, float Smoothness, float Metallic, Color Light, float Intensity, float Range)[]
            GeneratedLamps =
            {
                ("Lamp_Candle", "lamp_candle", 0.4f, 0.5f, new Color(1f, 0.68f, 0.38f), 3.2f, 8f),
                ("Lamp_Oil", "lamp_oil", 0.5f, 0.6f, new Color(1f, 0.7f, 0.42f), 3.6f, 9f),
                ("Lamp_Lantern6", "lamp_lantern6", 0.4f, 0.5f, new Color(1f, 0.68f, 0.38f), 3.4f, 9f),
                ("Lamp_Skull", "lamp_skull", 0.3f, 0f, new Color(1f, 0.64f, 0.34f), 3.0f, 8f),
                ("Lamp_Chandelier", "lamp_chandelier", 0.4f, 0.5f, new Color(1f, 0.7f, 0.4f), 3.8f, 10f),
                ("Lamp_Orb", "lamp_orb", 0.6f, 0.3f, new Color(0.6f, 0.65f, 1f), 3.4f, 9f),
            };

        private static readonly (string Name, string Id, float Smoothness, float Metallic)[] GeneratedDecor =
        {
            ("Decor_Candle", "decor_candle", 0.4f, 0f),
            ("Decor_AleMug", "decor_ale", 0.35f, 0f),
            ("Decor_CoinStack", "decor_coins", 0.65f, 0.85f),
            ("Decor_Skull", "decor_skull", 0.3f, 0f),
            ("Decor_Cards", "decor_cards", 0.5f, 0f),
            ("Decor_Hourglass", "decor_hourglass", 0.6f, 0f),
            ("Decor_RumBottle", "decor_rum", 0.8f, 0f),
            ("Decor_MapScroll", "decor_map", 0.2f, 0f),
            ("Decor_Compass", "decor_compass", 0.65f, 0.8f),
            ("Decor_Dagger", "decor_dagger", 0.7f, 0.7f),
            ("Decor_CoinPouch", "decor_pouch", 0.15f, 0f),
            ("Decor_Spyglass", "decor_spyglass", 0.6f, 0.6f),
        };

        /// <summary>Новые стаканы v5: имя модели в Art/Models/Cups, ID предмета, гладкость, металл.</summary>
        private static readonly (string Name, string Id, float Smoothness, float Metallic)[] GeneratedCupsV5 =
        {
            ("Cup_Horn", "cup_horn", 0.6f, 0f),
            ("Cup_Wicker", "cup_wicker", 0.15f, 0f),
            ("Cup_Crystal", "cup_crystal", 0.75f, 0f),
        };

        /// <summary>Столы v5 (zonk_tables.py): та же посадка, что у дубового (верх столешницы в 0).</summary>
        private static readonly (string Name, string Id, float Smoothness)[] GeneratedTables =
        {
            ("Table_Tavern", "table_tavern", 0.3f),
            ("Table_Gambling", "table_gambling", 0.55f),
            ("Table_Barrel", "table_barrel", 0.25f),
            ("Table_Stone", "table_stone", 0.1f),
            ("Table_Deck", "table_deck", 0.25f),
            ("Table_Royal", "table_royal", 0.6f),
            ("Table_Stump", "table_stump", 0.15f),
            ("Table_Pirate", "table_pirate", 0.35f),
        };

        private static void BuildShopModels()
        {
            var tableSlot = SlotAt("table");
            var tableOrder = 20;
            foreach (var (name, id, smoothness) in GeneratedTables)
            {
                var prefab = ImportedPrefab("Tables", name, smoothness, 0f, LampTextureSize, Color.white);
                if (prefab == null || tableSlot == null)
                    continue;

                var item = Item(id, tableSlot, tableOrder++, new PrefabPayload { Prefab = prefab });
                // Королевский стол был перекраской дубового (товар table_royal): теперь своя модель, товар тот же.
                if (item.Payload is MaterialPayload)
                {
                    item.Payload = new PrefabPayload { Prefab = prefab };
                    EditorUtility.SetDirty(item);
                }
            }

            var lampSlot = SlotAt("lamp");
            var cupSlot = SlotAt("cup");
            var decorSlot = Slot(DecorSlotId, "slot.decor", 7, "shop_decor", new AnchorPrefabApplier());

            var order = 20;
            foreach (var (name, id, smoothness, metallic, light, intensity, range) in GeneratedLamps)
            {
                var prefab = ImportedPrefab("Lamps", name, smoothness, metallic, LampTextureSize, light);
                if (prefab == null)
                    continue;
                EnsureLampLight(prefab, name, light, intensity, range);
                if (lampSlot != null)
                    Item(id, lampSlot, order, new PrefabPayload { Prefab = prefab });
                order++;
            }

            order = 20;
            foreach (var (name, id, smoothness, metallic) in GeneratedCupsV5)
            {
                var path = Models + "/Cups/" + name + ".fbx";
                if (!(AssetImporter.GetAtPath(path) is ModelImporter))
                    continue;
                ConfigureModel(path, false);
                var texturePath = Textures + "/Cups/" + name + ".png";
                ConfigureTexture(texturePath, CupTextureSize);
                var material = Mat(null, name, Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath), smoothness, metallic);
                var prefab = BuildCupPrefab(name, path, material);
                if (cupSlot != null && prefab != null)
                    Item(id, cupSlot, order, new PrefabPayload { Prefab = prefab });
                order++;
            }

            order = 0;
            foreach (var (name, id, smoothness, metallic) in GeneratedDecor)
            {
                var prefab = ImportedPrefab("Decor", name, smoothness, metallic, DecorTextureSize, new Color(1f, 0.7f, 0.4f));
                if (prefab == null)
                    continue;
                var item = Item(id, decorSlot, order, new PrefabPayload { Prefab = prefab });
                // Базовая безделушка — свеча: бесплатная, стоит на столе у всех.
                if (order == 0)
                    SetDefault(decorSlot, item);
                order++;
            }
        }

        /// <summary>
        /// Префаб из FBX: модель, основной материал с текстурой и, если у меша второй слот, светящийся Glow_*.
        /// Существующий префаб не пересоздаётся.
        /// </summary>
        private static GameObject ImportedPrefab(string kind, string name, float smoothness, float metallic, int textureSize, Color glow)
        {
            var modelPath = Models + "/" + kind + "/" + name + ".fbx";
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter))
                return null;

            ConfigureModel(modelPath, false);
            var texturePath = Textures + "/" + kind + "/" + name + ".png";
            ConfigureTexture(texturePath, textureSize);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            // Своё имя (Model_*), чтобы не взять материал старой перекраски с тем же именем (M_Table_Royal). Без «Candle» и «Deck»:
            // по ним комиксовый шейдер пропускает светящиеся и плоские материалы (ZonkSetup.Toon).
            var material = Mat(null, "Model_" + name.Replace("Candle", "Wax").Replace("Deck", "Ship"), Color.white, texture, smoothness, metallic);

            return Prefab(Prefabs + "/" + kind + "/" + name + ".prefab", () =>
            {
                var root = new GameObject(name);
                var meshes = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Mesh>().ToArray();
                if (meshes.Length > 1)
                    Debug.LogWarning($"[Zonk] {modelPath}: {meshes.Length} meshes, only the first is used — join objects in Blender");
                var mesh = meshes.Length > 0 ? meshes[0] : null;

                var model = Empty("Model", root.transform);
                model.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = model.AddComponent<MeshRenderer>();
                if (mesh != null && mesh.subMeshCount > 1)
                {
                    var glowMaterial = Mat(null, "Glow_" + name, Color.white, texture, 0.5f, 0f, glow * 1.6f);
                    renderer.sharedMaterials = new[] { material, glowMaterial };
                }
                else
                {
                    renderer.sharedMaterial = material;
                }

                return root;
            });
        }

        /// <summary>Точечный свет лампы в маркере LightPoint модели (нет маркера — чуть ниже начала), без теней.</summary>
        private static void EnsureLampLight(GameObject prefab, string name, Color color, float intensity, float range)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponentInChildren<Light>(true) != null)
                    return;

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/Lamps/" + name + ".fbx");
                var marker = source != null ? source.transform.Find("LightPoint") : null;
                var position = marker != null ? marker.localPosition : new Vector3(0f, -0.2f, 0f);
                var go = Empty("Light", root.transform, position);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.intensity = intensity;
                light.range = range;
                light.shadows = LightShadows.None;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            EnsureSceneLights(path, QualityTier.Low);
        }
    }
}
