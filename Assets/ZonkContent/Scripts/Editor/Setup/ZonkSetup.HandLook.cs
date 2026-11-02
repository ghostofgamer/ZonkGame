using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;
using Zonk.Presentation;
using Zonk.Table;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Облик руки (07.10.2026, вторая версия): рука собирается из слотов, вкладки магазина по 15 вещей.
    ///   «Кисть» (hand_look)     — без перчатки, перчатки и особые руки (Tools/Blender/zonk_hands.py, меш на скелете руки)
    ///   «Рукав» (hand_sleeve)   — одежда на предплечье (zonk_sleeves.py)
    ///   «Запястье» (hand_wrist) — ничего, часы, браслеты (zonk_wrist.py)
    ///   «Кожа» (hand_skin)      — шесть тонов бесплатно, только в профиле (вкладка «Облик»), в магазине нет
    /// Во вкладке все способы получения; вещи боссов — награда за первую победу над ними. Соперники одеты по таблице
    /// OpponentLooks (только пустые поля — ручные правки остаются). Вещи первой версии (без метки HandLookLabel)
    /// удаляются один раз вместе со ссылками на них. Уже созданные вещи не перезаписываются.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string HandSkinSlotId = SlotIds.HandSkin;
        private const string HandSleeveSlotId = SlotIds.HandSleeve;
        private const string HandWristSlotId = SlotIds.HandWrist;
        private const string HandLookSlotId = SlotIds.HandLook;
        private const string HandShot = "shop_hand";
        private const string HandLookLabel = "zonk-handlook-v2";
        private const string HandSkinIconsFolder = Root + "/Art/Sprites/HandSkins";
        private const int HandSkinIconSize = 128;
        private const int HandPaletteSize = 256;

        /// <summary>Тона кожи: второй — материал руки по умолчанию (M_Hand_Skin).</summary>
        private static readonly Color[] HandTones =
        {
            new Color(0.98f, 0.84f, 0.74f),
            new Color(0.93f, 0.72f, 0.58f), // = HandSkinColor (ZonkSetup.Hands): поля разных файлов инициализируются в любом порядке
            new Color(0.84f, 0.62f, 0.44f),
            new Color(0.7f, 0.48f, 0.32f),
            new Color(0.5f, 0.32f, 0.2f),
            new Color(0.33f, 0.2f, 0.13f),
        };

        private const int DefaultHandTone = 1;

        private enum HandPrice
        {
            Free,
            Coins,
            Ads,
            Purchase,
            Boss,
        }

        /// <summary>Вещь облика руки: ID, модель (null — «ничего»), редкость, цена. Hides — кисть прячет руку.</summary>
        private sealed class LookDef
        {
            public string Id;
            public string Model;
            public Rarity Rarity;
            public HandPrice Price;
            public int Amount;
            public string Boss;
            public float Smoothness = 0.3f;
            public float Metallic;
            public bool Hides;

            public LookDef(string id, string model, Rarity rarity, HandPrice price, int amount = 0, string boss = null)
            {
                Id = id;
                Model = model;
                Rarity = rarity;
                Price = price;
                Amount = amount;
                Boss = boss;
            }
        }

        private static readonly LookDef[] Sleeves =
        {
            new LookDef("sleeve_knit", "Sleeve_Knit", Rarity.Common, HandPrice.Free),
            new LookDef("sleeve_shirt", "Sleeve_Shirt", Rarity.Common, HandPrice.Coins, 1500),
            new LookDef("sleeve_cowboy", "Sleeve_Cowboy", Rarity.Common, HandPrice.Coins, 2500),
            new LookDef("sleeve_denim", "Sleeve_Denim", Rarity.Rare, HandPrice.Coins, 3500),
            new LookDef("sleeve_croupier", "Sleeve_Croupier", Rarity.Rare, HandPrice.Coins, 4500),
            new LookDef("sleeve_sailor", "Sleeve_Sailor", Rarity.Common, HandPrice.Ads, 3),
            new LookDef("sleeve_kimono", "Sleeve_Kimono", Rarity.Rare, HandPrice.Ads, 5),
            new LookDef("sleeve_mage", "Sleeve_Mage", Rarity.Epic, HandPrice.Ads, 7),
            new LookDef("sleeve_biker", "Sleeve_Biker", Rarity.Rare, HandPrice.Purchase),
            new LookDef("sleeve_mafia", "Sleeve_Mafia", Rarity.Epic, HandPrice.Purchase),
            new LookDef("sleeve_ace", "Sleeve_Ace", Rarity.Epic, HandPrice.Purchase),
            new LookDef("sleeve_pirate", "Sleeve_Pirate", Rarity.Legendary, HandPrice.Boss, boss: "captain"),
            new LookDef("sleeve_fortune", "Sleeve_Fortune", Rarity.Legendary, HandPrice.Boss, boss: "madame"),
            new LookDef("sleeve_robe", "Sleeve_Robe", Rarity.Epic, HandPrice.Boss, boss: "agafya"),
            new LookDef("sleeve_tavern", "Sleeve_Tavern", Rarity.Rare, HandPrice.Boss, boss: "bo"),
        };

        private static readonly LookDef[] Wrists =
        {
            new LookDef("wrist_none", null, Rarity.Common, HandPrice.Free),
            new LookDef("wrist_watch", "Wrist_Watch", Rarity.Common, HandPrice.Coins, 1500) { Metallic = 0.3f },
            new LookDef("wrist_thread", "Wrist_RedThread", Rarity.Common, HandPrice.Coins, 800),
            new LookDef("wrist_wood", "Wrist_WoodBeads", Rarity.Common, HandPrice.Coins, 1200),
            new LookDef("wrist_dice", "Wrist_DiceBeads", Rarity.Rare, HandPrice.Coins, 2500),
            new LookDef("wrist_studs", "Wrist_Studs", Rarity.Rare, HandPrice.Coins, 3500) { Smoothness = 0.5f },
            new LookDef("wrist_chips", "Wrist_Chips", Rarity.Rare, HandPrice.Ads, 3),
            new LookDef("wrist_smart", "Wrist_Smart", Rarity.Rare, HandPrice.Ads, 5) { Smoothness = 0.6f },
            new LookDef("wrist_anchor", "Wrist_Anchor", Rarity.Common, HandPrice.Ads, 4),
            new LookDef("wrist_goldwatch", "Wrist_GoldWatch", Rarity.Epic, HandPrice.Purchase) { Smoothness = 0.6f, Metallic = 0.6f },
            new LookDef("wrist_chain", "Wrist_Chain", Rarity.Epic, HandPrice.Purchase) { Smoothness = 0.6f, Metallic = 0.7f },
            new LookDef("wrist_gems", "Wrist_Gems", Rarity.Legendary, HandPrice.Purchase) { Smoothness = 0.7f, Metallic = 0.4f },
            new LookDef("wrist_compass", "Wrist_Compass", Rarity.Epic, HandPrice.Boss, boss: "sych") { Metallic = 0.3f },
            new LookDef("wrist_horseshoe", "Wrist_Horseshoe", Rarity.Rare, HandPrice.Boss, boss: "tikhon") { Metallic = 0.5f },
            new LookDef("wrist_skull", "Wrist_Skull", Rarity.Epic, HandPrice.Boss, boss: "krot"),
        };

        private static readonly LookDef[] HandLooks =
        {
            new LookDef("hand_none", null, Rarity.Common, HandPrice.Free),
            new LookDef("hand_leather", "Hand_Leather", Rarity.Common, HandPrice.Coins, 2000),
            new LookDef("hand_fingerless", "Hand_Fingerless", Rarity.Common, HandPrice.Coins, 1500),
            new LookDef("hand_butler", "Hand_Butler", Rarity.Rare, HandPrice.Coins, 3000),
            new LookDef("hand_hook", "Hand_Hook", Rarity.Rare, HandPrice.Coins, 4000) { Hides = true },
            new LookDef("hand_gauntlet", "Hand_Gauntlet", Rarity.Epic, HandPrice.Ads, 6) { Hides = true, Metallic = 0.7f, Smoothness = 0.5f },
            new LookDef("hand_skeleton", "Hand_Skeleton", Rarity.Epic, HandPrice.Ads, 7) { Hides = true },
            new LookDef("hand_zombie", "Hand_Zombie", Rarity.Rare, HandPrice.Ads, 5) { Hides = true },
            new LookDef("hand_catpaw", "Hand_CatPaw", Rarity.Rare, HandPrice.Ads, 4) { Hides = true, Smoothness = 0.1f },
            new LookDef("hand_robot", "Hand_Robot", Rarity.Epic, HandPrice.Purchase) { Hides = true, Metallic = 0.6f, Smoothness = 0.5f },
            new LookDef("hand_gold", "Hand_Gold", Rarity.Legendary, HandPrice.Purchase) { Hides = true, Metallic = 0.8f, Smoothness = 0.65f },
            new LookDef("hand_demon", "Hand_Demon", Rarity.Epic, HandPrice.Purchase) { Hides = true },
            new LookDef("hand_claw", "Hand_Claw", Rarity.Legendary, HandPrice.Boss, boss: "claw") { Hides = true, Smoothness = 0.5f },
            new LookDef("hand_fortune", "Hand_Fortune", Rarity.Legendary, HandPrice.Boss, boss: "fortuna") { Hides = true, Smoothness = 0.6f },
            new LookDef("hand_gardener", "Hand_Gardener", Rarity.Epic, HandPrice.Boss, boss: "rose"),
        };

        /// <summary>Облик соперников: рукав, запястье, кисть (null — не задавать). Грозные версии — в образе пострашнее.</summary>
        private static readonly (string Opponent, string Sleeve, string Wrist, string Hand)[] OpponentLooks =
        {
            ("captain", "sleeve_pirate", "wrist_compass", "hand_hook"), ("captain_dread", "sleeve_pirate", "wrist_skull", "hand_skeleton"),
            ("madame", "sleeve_fortune", "wrist_gems", null), ("madame_dread", "sleeve_fortune", "wrist_gems", "hand_demon"),
            ("agafya", "sleeve_robe", "wrist_thread", null), ("agafya_dread", "sleeve_robe", "wrist_skull", "hand_skeleton"),
            ("bo", "sleeve_tavern", "wrist_wood", null), ("bo_dread", "sleeve_tavern", "wrist_studs", "hand_zombie"),
            ("sych", "sleeve_sailor", "wrist_compass", "hand_fingerless"), ("sych_dread", "sleeve_sailor", "wrist_anchor", "hand_hook"),
            ("tikhon", "sleeve_denim", "wrist_horseshoe", "hand_fingerless"), ("tikhon_dread", "sleeve_denim", "wrist_horseshoe", "hand_zombie"),
            ("krot", "sleeve_mafia", "wrist_skull", "hand_leather"), ("krot_dread", "sleeve_mafia", "wrist_chain", "hand_robot"),
            ("claw", "sleeve_sailor", "wrist_anchor", "hand_claw"), ("claw_dread", "sleeve_kimono", "wrist_anchor", "hand_claw"),
            ("rose", "sleeve_shirt", "wrist_gems", "hand_gardener"), ("rose_dread", "sleeve_mage", "wrist_gems", "hand_gauntlet"),
            ("fortuna", "sleeve_mage", "wrist_goldwatch", "hand_fortune"), ("fortuna_dread", "sleeve_mage", "wrist_gems", "hand_gold"),
        };

        /// <summary>Обычные соперники по кругу: только вещи за монеты и рекламу (не награды и не покупки).</summary>
        private static readonly string[] CommonSleeves = { "sleeve_knit", "sleeve_shirt", "sleeve_cowboy", "sleeve_denim", "sleeve_croupier", "sleeve_sailor" };
        private static readonly string[] CommonWrists = { "wrist_none", "wrist_watch", "wrist_thread", "wrist_wood", "wrist_none", "wrist_smart", "wrist_dice" };
        private static readonly string[] CommonHands = { "hand_none", "hand_none", "hand_none", "hand_fingerless", "hand_none", "hand_leather", "hand_none", "hand_butler" };

        private static void BuildHandLook(ContentSet content)
        {
            var coins = AssetDatabase.LoadAssetAtPath<CurrencyConfig>(ConfigsFolder + "/Currencies/Coins.asset");
            var lookSlot = Slot(HandLookSlotId, "slot.hand_look", 8, HandShot, new AnchorPrefabApplier());
            var sleeveSlot = Slot(HandSleeveSlotId, "slot.hand_sleeve", 9, HandShot, new AnchorPrefabApplier());
            var wristSlot = Slot(HandWristSlotId, "slot.hand_wrist", 10, HandShot, new AnchorPrefabApplier());
            var skinSlot = Slot(HandSkinSlotId, "slot.hand_skin", 11, HandShot, new HandSkinApplier());
            if (skinSlot.ShowInShop)
            {
                skinSlot.ShowInShop = false;
                EditorUtility.SetDirty(skinSlot);
            }

            foreach (var slot in new[] { lookSlot, sleeveSlot, wristSlot, skinSlot })
            {
                if (!content.Slots.Contains(slot))
                    content.Slots.Add(slot);
            }

            RemoveFirstHandLook(skinSlot, sleeveSlot);

            var items = new Dictionary<string, CosmeticItemConfig>();
            EnsureFolder(HandSkinIconsFolder);
            for (var i = 0; i < HandTones.Length; i++)
            {
                var id = "hand_tone_" + (i + 1);
                var material = i == DefaultHandTone
                    ? Mat(null, "Hand_Skin", HandTones[i], smoothness: 0.25f)
                    : Mat(null, "Hand_Skin_" + (i + 1), HandTones[i], smoothness: 0.25f);
                var item = Item(id, skinSlot, i, new MaterialPayload { Material = material });
                SetHandIcon(item, HandTones[i]);
                if (i == DefaultHandTone)
                    SetDefault(skinSlot, item);
                items[id] = item;
            }

            AddLooks(Sleeves, sleeveSlot, "Sleeves", coins, items);
            AddLooks(Wrists, wristSlot, "Wrist", coins, items);
            AddLooks(HandLooks, lookSlot, "HandLooks", coins, items);
            DressOpponents(items);
        }

        private static void AddLooks(LookDef[] defs, CosmeticSlotConfig slot, string kind, CurrencyConfig coins,
            Dictionary<string, CosmeticItemConfig> items)
        {
            for (var order = 0; order < defs.Length; order++)
            {
                var def = defs[order];
                CosmeticPayload payload;
                if (def.Model == null)
                {
                    payload = new EmptyPayload();
                }
                else
                {
                    var prefab = kind == "HandLooks"
                        ? HandLookPrefab(def)
                        : ImportedPrefab(kind, def.Model, def.Smoothness, def.Metallic, HandPaletteSize, Color.white);
                    if (prefab == null)
                    {
                        Debug.LogWarning("[Zonk] " + Models + "/" + kind + "/" + def.Model + ".fbx not found — run Tools/Blender (" +
                                         kind + ")");
                        continue;
                    }

                    payload = new PrefabPayload { Prefab = prefab };
                }

                var item = Item(def.Id, slot, order, payload, HandPriceOf(def, coins));
                SetRarity(item, def.Rarity);
                if (System.Array.IndexOf(AssetDatabase.GetLabels(item), HandLookLabel) < 0)
                    AssetDatabase.SetLabels(item, new[] { HandLookLabel });
                if (def.Price == HandPrice.Free)
                    SetDefault(slot, item);
                if (def.Price == HandPrice.Boss)
                    EnsureContentReward(OpponentAt(def.Boss), item);
                items[def.Id] = item;
            }
        }

        /// <summary>
        /// Префаб кисти: модель на скелете (импорт как у руки — Generic, без аниматора), материалы — кожа (её заменит
        /// HandView) и палитра вещи, HandLookPart. Существующий не пересоздаётся.
        /// </summary>
        private static GameObject HandLookPrefab(LookDef def)
        {
            var modelPath = Models + "/HandLooks/" + def.Model + ".fbx";
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer))
                return null;

            ConfigureHandModel(importer);
            var texturePath = Textures + "/HandLooks/" + def.Model + ".png";
            ConfigureTexture(texturePath, HandPaletteSize);
            var palette = Mat(null, "Model_" + def.Model, Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath),
                def.Smoothness, def.Metallic);
            var skin = AssetDatabase.LoadAssetAtPath<Material>(Materials + "/M_Hand_Skin.mat");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            return Prefab(Prefabs + "/HandLooks/" + def.Model + ".prefab", () =>
            {
                var root = new GameObject(def.Model);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                instance.name = "Model";
                instance.transform.SetParent(root.transform, false);
                foreach (var animator in instance.GetComponentsInChildren<Animator>(true))
                {
                    animator.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
                }

                var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var twoMaterials = renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.subMeshCount > 1;
                if (renderer != null)
                {
                    renderer.sharedMaterials = twoMaterials ? new[] { skin, palette } : new[] { palette };
                    renderer.updateWhenOffscreen = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }

                var part = root.AddComponent<HandLookPart>();
                part.HidesHand = def.Hides;
                part.SkinSlot = twoMaterials;
                return root;
            });
        }

        private static PriceOption[] HandPriceOf(LookDef def, CurrencyConfig coins)
        {
            switch (def.Price)
            {
                case HandPrice.Coins: return new PriceOption[] { Coins(coins, def.Amount) };
                case HandPrice.Ads: return new PriceOption[] { new RewardedAdPriceOption { AdsRequired = def.Amount } };
                case HandPrice.Purchase: return new PriceOption[] { new PurchasePriceOption { ProductId = def.Id } };
                case HandPrice.Boss: return new PriceOption[] { new ProgressPriceOption { HintKey = "hint.beat_" + def.Boss } };
                default: return new PriceOption[0];
            }
        }

        private static void SetRarity(CosmeticItemConfig item, Rarity rarity)
        {
            if (item.Rarity == rarity || item.Rarity != Rarity.Common)
                return;
            item.Rarity = rarity;
            EditorUtility.SetDirty(item);
        }

        /// <summary>
        /// Первая версия облика руки (утро 07.10): рукава-образы с часами и фантазийная кожа. Удаляются вещи без метки
        /// HandLookLabel (тона кожи остаются), их префабы и материалы, ссылки соперников и награды. Один раз: у новых метка есть.
        /// </summary>
        private static void RemoveFirstHandLook(CosmeticSlotConfig skinSlot, CosmeticSlotConfig sleeveSlot)
        {
            var old = FindAll<CosmeticItemConfig>(ConfigsFolder + "/Cosmetics/Items")
                .Where(i => (i.Slot == sleeveSlot || (i.Slot == skinSlot && !i.Id.StartsWith("hand_tone_"))) &&
                            System.Array.IndexOf(AssetDatabase.GetLabels(i), HandLookLabel) < 0)
                .ToList();
            if (old.Count == 0)
                return;

            foreach (var opponent in FindAll<OpponentConfig>(ConfigsFolder + "/Opponents"))
            {
                var changed = opponent.FirstWinRewards.RemoveAll(r => r is ContentReward c && old.Contains(c.Item as CosmeticItemConfig)) > 0;
                changed |= opponent.RepeatWinRewards.RemoveAll(r => r is ContentReward c && old.Contains(c.Item as CosmeticItemConfig)) > 0;
                if (old.Contains(opponent.HandSleeve))
                {
                    opponent.HandSleeve = null;
                    changed = true;
                }

                if (old.Contains(opponent.HandSkin))
                {
                    opponent.HandSkin = null;
                    changed = true;
                }

                if (changed)
                    EditorUtility.SetDirty(opponent);
            }

            if (old.Contains(sleeveSlot.DefaultItem))
            {
                sleeveSlot.DefaultItem = null;
                EditorUtility.SetDirty(sleeveSlot);
            }

            var keep = new HashSet<string>(Sleeves.Where(d => d.Model != null).Select(d => d.Model));
            foreach (var item in old)
            {
                // Сравнение с null — юнитиевское: префаб без модели (FBX удалён) бывает уже уничтожен, «is» этого не видит.
                var prefab = item.Payload is PrefabPayload payload ? EditorPrefabOf(payload) : null;
                var prefabName = prefab != null ? prefab.name : null;
                if (prefabName != null && !keep.Contains(prefabName))
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(prefab));
                    AssetDatabase.DeleteAsset(Materials + "/M_Model_" + prefabName + ".mat");
                }

                Debug.Log("[Zonk] Hand look v1 removed: " + item.Id);
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(item));
            }

            foreach (var id in new[] { "orc", "stone", "gold", "frost" })
                AssetDatabase.DeleteAsset(Materials + "/M_Hand_Skin_" + id + ".mat");
            foreach (var id in new[] { "hand_orc", "hand_stone", "hand_gold", "hand_frost" })
                AssetDatabase.DeleteAsset(HandSkinIconsFolder + "/" + id + ".png");
        }

        /// <summary>
        /// Облик соперников: боссы по таблице OpponentLooks, обычные — тон кожи, рукав, запястье и кисть по кругу,
        /// чтобы за столом сидели разные люди. Только пустые поля.
        /// </summary>
        private static void DressOpponents(Dictionary<string, CosmeticItemConfig> items)
        {
            CosmeticItemConfig At(string id) => id != null && items.TryGetValue(id, out var item) ? item : null;

            var dressed = new HashSet<OpponentConfig>();
            foreach (var (id, sleeve, wrist, hand) in OpponentLooks)
            {
                var opponent = OpponentAt(id);
                if (opponent == null)
                    continue;

                dressed.Add(opponent);
                Dress(opponent, null, At(sleeve), At(wrist), At(hand));
            }

            var all = FindAll<OpponentConfig>(ConfigsFolder + "/Opponents");
            all.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            for (var i = 0; i < all.Count; i++)
            {
                var opponent = all[i];
                var tone = At("hand_tone_" + (1 + i % HandTones.Length));
                if (dressed.Contains(opponent))
                    Dress(opponent, tone, null, null, null);
                else
                    Dress(opponent, tone, At(CommonSleeves[i % CommonSleeves.Length]), At(CommonWrists[i % CommonWrists.Length]),
                        At(CommonHands[i % CommonHands.Length]));
            }
        }

        private static void Dress(OpponentConfig opponent, CosmeticItemConfig skin, CosmeticItemConfig sleeve, CosmeticItemConfig wrist,
            CosmeticItemConfig hand)
        {
            var changed = false;
            if (opponent.HandSkin == null && skin != null)
            {
                opponent.HandSkin = skin;
                changed = true;
            }

            if (opponent.HandSleeve == null && sleeve != null)
            {
                opponent.HandSleeve = sleeve;
                changed = true;
            }

            if (opponent.HandWrist == null && wrist != null)
            {
                opponent.HandWrist = wrist;
                changed = true;
            }

            if (opponent.HandLook == null && hand != null)
            {
                opponent.HandLook = hand;
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(opponent);
        }

        /// <summary>Значок тона кожи: кружок цвета с тёмной обводкой. Есть файл — не перерисовывается.</summary>
        private static void SetHandIcon(CosmeticItemConfig item, Color color)
        {
            var path = HandSkinIconsFolder + "/" + item.Id + ".png";
            if (!File.Exists(path))
            {
                var size = HandSkinIconSize;
                var pixels = new Color[size * size];
                var c = size * 0.5f;
                var ink = Color.Lerp(color, Color.black, 0.6f);
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var outer = Ellipse(x, y, c, c, c - 4f, c - 4f);
                    var inner = Ellipse(x, y, c, c, c - 12f, c - 12f);
                    var fill = Color.Lerp(color, Color.white, 0.12f * (y / (float)size));
                    var pixel = Paint(ink, fill, inner);
                    pixel.a = outer;
                    pixels[y * size + x] = pixel;
                }

                SavePng(path, TextureFrom(size, size, pixels));
                AssetDatabase.ImportAsset(path);
            }

            var sprite = HandIconSprite(path);
            if (item.Icon == null && sprite != null)
            {
                item.Icon = sprite;
                EditorUtility.SetDirty(item);
            }
        }

        /// <summary>Заголовок сетки на странице «Облик» (как «Аватары», «Рамки»).</summary>
        private static TMPro.TMP_Text LookTitle(string name, RectTransform parent)
        {
            var title = Text(name, parent, _ui.BoldFont, 30, _ui.Palette.Gold);
            title.alignment = TMPro.TextAlignmentOptions.Left;
            Height(title, 44);
            return title;
        }

        /// <summary>Кожа руки в профиле, созданном раньше: заголовок и сетка под рамками (если их ещё нет).</summary>
        private static void AddHandSkinsToProfile()
        {
            EditWindowPrefab<Zonk.UI.Windows.ProfileWindow>("ProfileWindow", "_skinsGrid", (root, window) =>
            {
                var frames = FindDeep(root, "Frames") as RectTransform;
                if (frames == null || !(frames.parent is RectTransform content))
                    return null;

                var title = LookTitle("SkinsTitle", content);
                var grid = LookGrid("Skins", content);
                window.EditorSetupSkins(title, grid);
                return grid;
            });
        }

        private static Sprite HandIconSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = HandSkinIconSize;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
