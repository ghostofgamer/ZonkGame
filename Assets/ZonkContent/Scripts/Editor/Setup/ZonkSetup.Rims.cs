using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Рамка лотка (05.10): новый слот «Рамка» — бортик вокруг сукна, куда бросают кости. Раньше это были четыре серых
    /// куба в сцене без выбора. Модели — Tools/Blender/zonk_rims.py (Art/Models/Rims, атлас 512), в сцене якорь «Rim»
    /// в лотке (ZonkSetup.Scenes). Невидимые стенки лотка для физики костей отдельные: рамка — только вид.
    /// Новая рамка — функция в zonk_rims.py и строка в Rims (имя модели, ID, гладкость, металл) и в RimLayout (цена).
    /// </summary>
    public static partial class ZonkSetup
    {
        public const string RimSlotId = "rim";
        private const int RimTextureSize = 512;

        private static readonly (string Name, string Id, float Smoothness, float Metallic)[] Rims =
        {
            ("Rim_Wood", "rim_wood", 0.35f, 0f),
            ("Rim_Rope", "rim_rope", 0.1f, 0f),
            ("Rim_Dominoes", "rim_dominoes", 0.6f, 0f),
            ("Rim_Books", "rim_books", 0.2f, 0f),
            ("Rim_Garden", "rim_garden", 0.15f, 0f),
            ("Rim_Fence", "rim_fence", 0.1f, 0f),
            ("Rim_GreatWall", "rim_greatwall", 0.1f, 0f),
            ("Rim_Castle", "rim_castle", 0.1f, 0f),
            ("Rim_Rails", "rim_rails", 0.45f, 0f),
            ("Rim_Candy", "rim_candy", 0.75f, 0f),
            ("Rim_Crystal", "rim_crystal", 0.8f, 0f),
            ("Rim_Serpent", "rim_serpent", 0.55f, 0f),
            ("Rim_Coins", "rim_coins", 0.65f, 0f),   // без металла: металл отражает тёмную комнату и гасит золото
            ("Rim_Bones", "rim_bones", 0.25f, 0f),
            ("Rim_Cards", "rim_cards", 0.4f, 0f),
        };

        /// <summary>
        /// Цены рамок: дубовая — у всех (без цены); 4 за монеты, 4 за рекламу, 3 за деньги, 3 за игру (уровни 18 и 47, сундук).
        /// Применяется к вещам без цены: ручная правка цены не затирается.
        /// </summary>
        private static readonly (string id, Get how, int value)[] RimLayout =
        {
            ("rim_rope", Get.Coins, 1000),
            ("rim_dominoes", Get.Coins, 1800),
            ("rim_books", Get.Coins, 2500),
            ("rim_garden", Get.Coins, 3200),
            ("rim_fence", Get.Ads, 3),
            ("rim_castle", Get.Ads, 5),
            ("rim_greatwall", Get.Ads, 4),
            ("rim_rails", Get.Ads, 6),
            ("rim_candy", Get.Purchase, 0),
            ("rim_crystal", Get.Purchase, 0),
            ("rim_serpent", Get.Purchase, 0),
            ("rim_coins", Get.Play, 18),
            ("rim_cards", Get.Play, 47),
            ("rim_bones", Get.Play, FromChest),
        };

        private static void BuildRims(GameConfig config)
        {
            EnsureFolder(Prefabs + "/Rims");
            var slot = Slot(RimSlotId, "slot.rim", 8, "shop_tray", new AnchorPrefabApplier());
            var order = 0;
            foreach (var (name, id, smoothness, metallic) in Rims)
            {
                // у домино мелкие точки — текстура 1024 (костяшка 256 пикселей), у остальных рамок 512
                var size = id == "rim_dominoes" ? RimTextureSize * 2 : RimTextureSize;
                var prefab = ImportedPrefab("Rims", name, smoothness, metallic, size, Color.white);
                if (prefab == null)
                {
                    Debug.LogWarning("[Zonk] Rim model not found: " + name + ". Run Tools/Blender/zonk_rims.py");
                    order++;
                    continue;
                }

                var item = Item(id, slot, order++, new PrefabPayload { Prefab = prefab });
                if (id == "rim_rails")
                    AddRimTrain(prefab);
                if (id == "rim_wood")
                    SetDefault(slot, item);
            }

            ApplyLayoutV5Table(RimLayout, config, false);
        }

        // Путь поезда — как колея в zonk_rims.py: скруглённый прямоугольник по середине бортика.
        private const float RimHalfX = 1.675f;
        private const float RimHalfZ = 1.075f;
        private const float RailRadius = 0.14f;
        private const float RailHeight = 0.06f;
        private const float TrainSpeed = 0.35f;

        /// <summary>Паровоз, тендер и вагон: имя меша в Rim_Rails_Cars.fbx и отставание от головы поезда, метры.</summary>
        private static readonly (string Mesh, float Offset)[] TrainCars =
        {
            ("Loco", 0f),
            ("Tender", 0.21f),
            ("Wagon", 0.38f),
        };

        /// <summary>Поезд в префаб железной дороги, если его там нет: вагоны — дети «Train» с RimTrain.</summary>
        private static void AddRimTrain(GameObject prefab)
        {
            var carsPath = Models + "/Rims/Rim_Rails_Cars.fbx";
            if (!(AssetImporter.GetAtPath(carsPath) is ModelImporter))
            {
                Debug.LogWarning("[Zonk] Train cars not found: " + carsPath + ". Run Tools/Blender/zonk_rims.py");
                return;
            }

            ConfigureModel(carsPath, false);
            var path = AssetDatabase.GetAssetPath(prefab);
            if (SkipIfLocked(path))
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponentInChildren<RimTrain>(true) != null)
                    return;

                var renderer = root.GetComponentInChildren<MeshRenderer>();
                var material = renderer != null ? renderer.sharedMaterial : null;
                var meshes = AssetDatabase.LoadAllAssetsAtPath(carsPath).OfType<Mesh>().ToDictionary(m => m.name);

                var train = Empty("Train", root.transform);
                var cars = new List<Transform>();
                var offsets = new List<float>();
                foreach (var (meshName, offset) in TrainCars)
                {
                    if (!meshes.TryGetValue(meshName, out var mesh))
                        continue;

                    var car = Empty(meshName, train.transform);
                    car.AddComponent<MeshFilter>().sharedMesh = mesh;
                    car.AddComponent<MeshRenderer>().sharedMaterial = material;
                    cars.Add(car.transform);
                    offsets.Add(offset);
                }

                train.AddComponent<RimTrain>().EditorSetup(RailPath(), cars.ToArray(), offsets.ToArray(), TrainSpeed);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("[Zonk] Rim_Rails: train added (" + cars.Count + " cars)");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Скруглённый прямоугольник через ~5 см, против часовой стрелки сверху.</summary>
        private static Vector3[] RailPath()
        {
            const float step = 0.05f;
            var points = new List<Vector3>();
            var corners = new[]
            {
                new Vector2(RimHalfX - RailRadius, RimHalfZ - RailRadius), new Vector2(-(RimHalfX - RailRadius), RimHalfZ - RailRadius),
                new Vector2(-(RimHalfX - RailRadius), -(RimHalfZ - RailRadius)), new Vector2(RimHalfX - RailRadius, -(RimHalfZ - RailRadius)),
            };
            for (var k = 0; k < 4; k++)
            {
                var start = k * Mathf.PI * 0.5f;
                var arc = Mathf.Max(2, Mathf.CeilToInt(Mathf.PI * 0.5f * RailRadius / step));
                for (var j = 0; j <= arc; j++)
                {
                    var a = start + Mathf.PI * 0.5f * j / arc;
                    points.Add(new Vector3(corners[k].x + RailRadius * Mathf.Cos(a), RailHeight, corners[k].y + RailRadius * Mathf.Sin(a)));
                }

                var next = corners[(k + 1) % 4];
                var end = start + Mathf.PI * 0.5f;
                var from = points[points.Count - 1];
                var to = new Vector3(next.x + RailRadius * Mathf.Cos(end), RailHeight, next.y + RailRadius * Mathf.Sin(end));
                var count = Mathf.Max(1, Mathf.RoundToInt(Vector3.Distance(from, to) / step));
                for (var j = 1; j < count; j++)
                    points.Add(Vector3.Lerp(from, to, (float)j / count));
            }

            return points.ToArray();
        }
    }
}
