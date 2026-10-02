using System.IO;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Progress;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Расширение магазина до 10–15 вещей во вкладке (02.10.2026): сукна с узором (бесшовные текстуры, нарисованные кодом,
    /// с повтором по столу). Цены — раскладкой v5 (ZonkSetup.ShopLayout). Уже созданные текстуры и предметы не трогаются.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string FeltTexturesFolder = Root + "/Art/Textures/Felt";
        private const int FeltTextureSize = 256;

        /// <summary>Повтор узора по сукну 3.2 × 2: клетки одинакового размера по обеим осям.</summary>
        private static readonly Vector2 FeltTiling = new Vector2(8f, 5f);

        private enum FeltPattern
        {
            Checker,
            Stars,
            Waves,
            Harlequin,
            Dots,
        }

        private static readonly (string Id, FeltPattern Pattern, Color Base, Color Ink, float Smoothness)[] PatternFelts =
        {
            ("felt_checker", FeltPattern.Checker, new Color(0.14f, 0.36f, 0.2f), new Color(0.09f, 0.26f, 0.14f), 0.05f),
            ("felt_stars", FeltPattern.Stars, new Color(0.08f, 0.1f, 0.26f), new Color(0.95f, 0.78f, 0.3f), 0.1f),
            ("felt_waves", FeltPattern.Waves, new Color(0.07f, 0.34f, 0.4f), new Color(0.14f, 0.5f, 0.55f), 0.1f),
            ("felt_harlequin", FeltPattern.Harlequin, new Color(0.45f, 0.08f, 0.1f), new Color(0.12f, 0.05f, 0.06f), 0.15f),
            ("felt_dots", FeltPattern.Dots, new Color(0.3f, 0.12f, 0.36f), new Color(0.5f, 0.28f, 0.55f), 0.05f),
        };

        /// <summary>Способ получения в раскладке v5: к монетам, рекламе и покупке добавилось «за игру».</summary>
        private enum Get
        {
            Coins,
            Ads,
            Purchase,

            /// <summary>Не продаётся: сундук (Source = FromChest), уровень (Source > 0) или награда, выданная в другом месте.</summary>
            Play,
        }

        /// <summary>
        /// v5 (02.10.2026): в каждой вкладке по 3 вещи за монеты, рекламу, деньги и игру. Таблица дописывается по мере появления
        /// вещей; применяется один раз (GameConfig.ShopLayoutVersion), к бесплатным — всегда.
        /// Play: value > 0 — награда за уровень value; FromChest — в сундуке; FromAchievement — подсказка, выдача в достижении.
        /// </summary>
        private static readonly (string id, Get how, int value)[] ShopLayoutV5 =
        {
            // Сукно: к 2 / 2 / 2 добавлено по одному и два «за игру».
            ("felt_checker", Get.Coins, 1500),
            ("felt_stars", Get.Ads, 5),
            ("felt_harlequin", Get.Purchase, 0),
            ("felt_waves", Get.Play, FromChest),
            ("felt_dots", Get.Play, 35),

            // Столы: свои модели. Королевский (table_royal) остался покупкой — теперь это резной стол, а не перекраска.
            ("table_tavern", Get.Coins, 3000),
            ("table_barrel", Get.Coins, 4500),
            ("table_stone", Get.Ads, 8),
            ("table_gambling", Get.Purchase, 0),
            ("table_deck", Get.Play, FromChest),
            ("table_stump", Get.Play, 20),
            ("table_pirate", Get.Play, 40),

            // Лампы.
            ("lamp_lantern6", Get.Coins, 3500),
            ("lamp_oil", Get.Ads, 6),
            ("lamp_chandelier", Get.Purchase, 0),
            ("lamp_candle", Get.Play, 10),
            ("lamp_skull", Get.Play, FromChest),
            ("lamp_orb", Get.Play, 50),

            // Стаканы: три новых — за игру.
            ("cup_wicker", Get.Play, 15),
            ("cup_horn", Get.Play, FromChest),
            ("cup_crystal", Get.Play, 30),

            // Безделушки (базовая — свеча).
            ("decor_ale", Get.Coins, 800),
            ("decor_cards", Get.Coins, 1200),
            ("decor_coins", Get.Coins, 2000),
            ("decor_skull", Get.Ads, 3),
            ("decor_hourglass", Get.Ads, 4),
            ("decor_map", Get.Ads, 5),
            ("decor_compass", Get.Purchase, 0),
            ("decor_spyglass", Get.Purchase, 0),
            ("decor_dagger", Get.Purchase, 0),
            ("decor_pouch", Get.Play, 5),
            ("decor_rum", Get.Play, FromChest),
        };

        private static void BuildShopExpansion(GameConfig config)
        {
            EnsureFolder(Prefabs + "/Decor");
            EnsureFolder(Prefabs + "/Tables");
            BuildShopModels();
            BuildPatternFelts();
            ApplyShopLayoutV5(config);

            // Волна 2 (02.10.2026, по отзыву пользователя): каждая часть — свой файл генератора и своя таблица цен.
            BuildDiceV2(config);
            BuildTablesV2(config);
            BuildLampsFeltV2(config);
            BuildDecorSpots(config);
            if (config.ShopLayoutVersion < 6)
            {
                config.ShopLayoutVersion = 6;
                EditorUtility.SetDirty(config);
            }
        }

        // Части волны 2 — реализуются в своих файлах (ZonkSetup.DiceV2, TablesV2, LampsFeltV2, DecorSpots).
        static partial void BuildDiceV2(GameConfig config);
        static partial void BuildTablesV2(GameConfig config);
        static partial void BuildLampsFeltV2(GameConfig config);
        static partial void BuildDecorSpots(GameConfig config);

        /// <summary>
        /// Цены волны 2: таблица применяется один раз (ShopLayoutVersion < 6) ко всем своим вещам, к бесплатным — всегда.
        /// Вызывать из своей части волны 2 после создания вещей.
        /// </summary>
        private static void ApplyShopLayoutV6((string id, Get how, int value)[] table, GameConfig config)
        {
            ApplyLayoutV5Table(table, config, config.ShopLayoutVersion < 6);
        }

        private static void ApplyShopLayoutV5(GameConfig config)
        {
            ApplyLayoutV5Table(ShopLayoutV5, config, config.ShopLayoutVersion < 5);
            if (config.ShopLayoutVersion < 5)
            {
                config.ShopLayoutVersion = 5;
                EditorUtility.SetDirty(config);
                Debug.Log("[Setup] Shop layout v5 applied: 3 coins / 3 ads / 3 money / 3 play per tab");
            }
        }

        private static void ApplyLayoutV5Table((string id, Get how, int value)[] table, GameConfig config, bool firstTime)
        {
            var content = ContentById<ContentConfig>();
            foreach (var (id, how, value) in table)
            {
                if (!content.TryGetValue(id, out var item) || !(Pricing.PriceOf(item) is Price price))
                    continue;
                if (!firstTime && !price.IsFree)
                    continue;

                price.Options.Clear();
                switch (how)
                {
                    case Get.Coins:
                        price.Options.Add(Coins(config.Coins, value));
                        break;
                    case Get.Ads:
                        price.Options.Add(new RewardedAdPriceOption { AdsRequired = value });
                        break;
                    case Get.Purchase:
                        price.Options.Add(new PurchasePriceOption { ProductId = id });
                        break;
                    default:
                        price.Options.Add(new ProgressPriceOption
                        {
                            HintKey = value > 0 ? "hint.level" + value : value == FromChest ? "hint.chest" : "hint.achievement",
                        });
                        if (value > 0 && item is CosmeticItemConfig levelItem)
                            GiveAtLevel(config, value, levelItem);
                        if (value == FromChest && item is CosmeticItemConfig chestItem && config.Chest != null &&
                            !config.Chest.Items.Contains(chestItem))
                        {
                            config.Chest.Items.Add(chestItem);
                            EditorUtility.SetDirty(config.Chest);
                        }

                        break;
                }

                EditorUtility.SetDirty(item);
            }
        }

        private static void BuildPatternFelts()
        {
            var traySlot = SlotAt("tray");
            if (traySlot == null)
                return;

            EnsureFolder(FeltTexturesFolder);
            for (var i = 0; i < PatternFelts.Length; i++)
            {
                var (id, pattern, baseColor, ink, smoothness) = PatternFelts[i];
                var name = "Felt_" + char.ToUpperInvariant(id[5]) + id.Substring(6);
                var path = FeltTexturesFolder + "/" + name + ".png";
                if (!File.Exists(path))
                    SavePng(path, PaintFelt(pattern, baseColor, ink));
                ConfigureFeltTexture(path);

                var material = Mat(null, name, Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(path), smoothness);
                if (material.GetTextureScale("_BaseMap") != FeltTiling)
                {
                    material.SetTextureScale("_BaseMap", FeltTiling);
                    EditorUtility.SetDirty(material);
                }

                Item(id, traySlot, 7 + i, new MaterialPayload { Material = material });
            }
        }

        private static void ConfigureFeltTexture(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer) || importer.wrapMode == TextureWrapMode.Repeat)
                return;

            importer.wrapMode = TextureWrapMode.Repeat;
            importer.maxTextureSize = FeltTextureSize;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.SaveAndReimport();
        }

        /// <summary>Бесшовный узор: ровные пятна под комикс, лёгкая ворсистость полосами, без мелкого шума.</summary>
        private static Texture2D PaintFelt(FeltPattern pattern, Color baseColor, Color ink)
        {
            var size = FeltTextureSize;
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var u = x / (float)size;
                var v = y / (float)size;
                var color = baseColor;
                switch (pattern)
                {
                    case FeltPattern.Checker:
                    {
                        // Шотландка: полосы двух ширин накладываются, на пересечении темнее.
                        var a = Stripe(u, 0.5f, 0.22f) ? 1 : 0;
                        var b = Stripe(v, 0.5f, 0.22f) ? 1 : 0;
                        color = Color.Lerp(baseColor, ink, (a + b) * 0.5f);
                        if (Stripe(u, 0.02f, 0.012f) || Stripe(v, 0.02f, 0.012f))
                            color = Color.Lerp(color, new Color(0.9f, 0.82f, 0.5f), 0.55f);
                        break;
                    }
                    case FeltPattern.Stars:
                    {
                        color = Paint(color, ink, StarCoverage((u - 0.25f) * size, (v - 0.25f) * size, 22f, 9f));
                        color = Paint(color, ink * 0.85f, StarCoverage((u - 0.75f) * size, (v - 0.75f) * size, 14f, 6f));
                        color = Paint(color, ink * 0.7f, Ellipse(x, y, 0.75f * size, 0.22f * size, 3f, 3f));
                        color = Paint(color, ink * 0.7f, Ellipse(x, y, 0.2f * size, 0.78f * size, 3f, 3f));
                        break;
                    }
                    case FeltPattern.Waves:
                    {
                        // Два ряда гребней волн: синус по u, повтор по v.
                        for (var row = 0; row < 2; row++)
                        {
                            var crest = (row + 0.5f) / 2f + Mathf.Sin(u * Mathf.PI * 4f + row * Mathf.PI) * 0.06f;
                            var distance = Mathf.Abs(Mathf.Repeat(v - crest + 0.5f, 1f) - 0.5f) * size;
                            color = Paint(color, ink, Mathf.Clamp01(5f - distance));
                            color = Paint(color, Color.Lerp(ink, Color.white, 0.4f), Mathf.Clamp01(1.5f - Mathf.Abs(distance - 3f)) * 0.6f);
                        }

                        break;
                    }
                    case FeltPattern.Harlequin:
                    {
                        // Ромбы шахматкой и тонкая золотая обводка.
                        var du = Mathf.Abs(Mathf.Repeat(u * 2f, 1f) - 0.5f);
                        var dv = Mathf.Abs(Mathf.Repeat(v * 2f, 1f) - 0.5f);
                        var inside = du + dv < 0.5f;
                        var cell = (Mathf.FloorToInt(u * 2f) + Mathf.FloorToInt(v * 2f)) % 2 == 0;
                        color = inside == cell ? baseColor : ink;
                        if (Mathf.Abs(du + dv - 0.5f) < 0.012f)
                            color = new Color(0.85f, 0.65f, 0.25f);
                        break;
                    }
                    case FeltPattern.Dots:
                    {
                        color = Paint(color, ink, Ellipse(x, y, 0.25f * size, 0.25f * size, 18f, 18f));
                        color = Paint(color, ink, Ellipse(x, y, 0.75f * size, 0.75f * size, 18f, 18f));
                        break;
                    }
                }

                // Ворс: едва заметные полосы по одной оси, чтобы сукно не было пластиком.
                var nap = 0.97f + 0.03f * Mathf.Sin(v * Mathf.PI * 2f * 16f);
                color = new Color(color.r * nap, color.g * nap, color.b * nap, 1f);
                pixels[y * size + x] = color;
            }

            return TextureFrom(size, size, pixels);
        }

        /// <summary>Попадает ли t (0..1, повтор) в полосу ширины width с центром center.</summary>
        private static bool Stripe(float t, float center, float width)
        {
            return Mathf.Abs(Mathf.Repeat(t - center + 0.5f, 1f) - 0.5f) < width * 0.5f;
        }
    }
}
