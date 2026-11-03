using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Детали соперника-тени: голова, грудь, предмет. Модели — Tools/Blender/zonk_oppkit.py, одна палитра и один материал
    /// Zonk/Toon на все. Новый соперник — строка в таблице, новая деталь — функция в скрипте и префаб сам появится здесь.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string KitModels = Root + "/Art/Models/OppKit";
        private const string KitPrefabs = Prefabs + "/OppKit";

        // Профессия соперника: голова, грудь, предмет на столе. Пустая строка — детали нет. Грозная версия — как обычная.
        private static readonly (string id, string head, string chest, string prop)[] OpponentKit =
        {
            ("Opp_vitya", "Head_FlatCap", "", "Prop_Die"),
            ("Opp_klava", "Head_Scarf", "Chest_Apron", "Prop_RollingPin"),
            ("Opp_petrovich", "Head_Ushanka", "", "Prop_Mug"),
            ("Opp_semenych", "Head_Postman", "Chest_Strap", "Prop_Letter"),
            ("Opp_agafya", "Head_ScarfDark", "Chest_KeyChain", "Prop_BigKey"),
            ("Opp_agafya_dread", "Head_ScarfDark", "Chest_KeyChain", "Prop_BigKey"),
            ("Opp_arkhip", "Head_FlatCap", "Chest_WatchChain", "Prop_Pipe"),
            ("Opp_sudba", "Head_ScarfDark", "Chest_Beads", "Prop_Yarn"),
            ("Opp_gordeev", "Head_Officer", "Chest_Medal", "Prop_Pipe"),
            ("Opp_nina", "Head_Bun", "Chest_Pearls", "Prop_Abacus"),
            ("Opp_gustav", "Head_Bandana", "Chest_Apron", "Prop_Coins"),
            ("Opp_glyba", "Head_Bowler", "Chest_BowBlack", "Prop_Club"),
            ("Opp_zhora", "Head_Fedora", "Chest_Tie", "Prop_Cards"),
            ("Opp_irma", "Head_BandanaDark", "Chest_Bandolier", "Prop_Dagger"),
            ("Opp_lutik", "Head_BardBeret", "Chest_Ribbon", "Prop_Lute"),
            ("Opp_bo", "Head_EyePatch", "Chest_Apron", "Prop_Mug"),
            ("Opp_bo_dread", "Head_EyePatch", "Chest_Apron", "Prop_Mug"),
            ("Opp_efim", "Head_Bucket", "", "Prop_Fish"),
            ("Opp_max", "Head_SurfBand", "Chest_Shells", "Prop_Surfboard"),
            ("Opp_zina", "Head_StrawHat", "Chest_Apron", "Prop_Corn"),
            ("Opp_stepan", "Head_Visor", "Chest_Whistle", "Prop_Lifebuoy"),
            ("Opp_lili", "Head_SunHat", "Chest_Pearls", "Prop_Teacup"),
            ("Opp_claw", "Head_Crab", "Chest_Shells", "Prop_Claw"),
            ("Opp_claw_dread", "Head_Crab", "Chest_Shells", "Prop_Claw"),
            ("Opp_pit", "Head_SailorCap", "Chest_SailorCollar", "Prop_Mop"),
            ("Opp_bart", "Head_Chef", "Chest_Apron", "Prop_Ladle"),
            ("Opp_greta", "Head_Bicorn", "Chest_Ribbon", "Prop_Compass"),
            ("Opp_marta", "Head_PirateBandana", "Chest_Bandolier", "Prop_Cutlass"),
            ("Opp_hook", "Head_Tricorn", "Chest_SailorCollar", "Prop_Hook"),
            ("Opp_captain", "Head_Captain", "Chest_Medal", "Prop_Parrot"),
            ("Opp_captain_dread", "Head_Captain", "Chest_Medal", "Prop_Parrot"),
            ("Opp_vetrov", "Head_SailorCap", "Chest_Ribbon", "Prop_Compass"),
            ("Opp_foma", "Head_StrawHat", "Chest_Apron", "Prop_Trowel"),
            ("Opp_miron", "Head_BeeHat", "", "Prop_Honey"),
            ("Opp_karl", "Head_Loupe", "Chest_WatchChain", "Prop_Clock"),
            ("Opp_alfred", "Head_Bowler", "Chest_BowBlack", "Prop_Tray"),
            ("Opp_stavkin", "Head_TopHat", "Chest_Medal", "Prop_Coins"),
            ("Opp_rose", "Head_RoseTiara", "Chest_Pearls", "Prop_Rose"),
            ("Opp_rose_dread", "Head_RoseTiara", "Chest_Pearls", "Prop_Rose"),
            ("Opp_barsik", "Head_CatEars", "Chest_Bell", "Prop_Yarn"),
            ("Opp_voldemar", "Head_MagicHat", "Chest_BowTie", "Prop_Wand"),
            ("Opp_agrippina", "Head_Ghost", "Chest_Pearls", "Prop_Candle"),
            ("Opp_serafim", "Head_Beret", "Chest_Ribbon", "Prop_Puppet"),
            ("Opp_tikhon", "Head_WornHat", "Chest_WatchChain", "Prop_OilLamp"),
            ("Opp_tikhon_dread", "Head_WornHat", "Chest_WatchChain", "Prop_OilLamp"),
            ("Opp_erofey", "Head_WatchCap", "Chest_Whistle", "Prop_Lantern"),
            ("Opp_strogiy", "Head_Customs", "Chest_Medal", "Prop_Stamp"),
            ("Opp_zhemchugov", "Head_Merchant", "Chest_Pearls", "Prop_Shell"),
            ("Opp_karr", "Head_Raven", "", "Prop_Feather"),
            ("Opp_sych", "Head_BandanaDark", "Chest_Bandolier", "Prop_Crate"),
            ("Opp_sych_dread", "Head_BandanaDark", "Chest_Bandolier", "Prop_Crate"),
            ("Opp_krot", "Head_Miner", "Chest_Strap", "Prop_Pickaxe"),
            ("Opp_krot_dread", "Head_Miner", "Chest_Strap", "Prop_Pickaxe"),
            ("Opp_madame", "Head_Midnight", "Chest_Pearls", "Prop_Fan"),
            ("Opp_madame_dread", "Head_Midnight", "Chest_Pearls", "Prop_Fan"),
            ("Opp_aglaya", "Head_Turban", "Chest_Beads", "Prop_Crystal"),
            ("Opp_jester", "Head_Jester", "Chest_JesterCollar", "Prop_Cards"),
            ("Opp_twins", "Head_TwinCaps", "Chest_BowTie", "Prop_Die"),
            ("Opp_fortuna", "Head_Laurel", "Chest_Medal", "Prop_GoldDie"),
            ("Opp_fortuna_dread", "Head_Laurel", "Chest_Medal", "Prop_GoldDie"),
        };

        [MenuItem("Zonk/Art/Apply Opponent Kit", priority = 47)]
        public static void ApplyOpponentKitMenu()
        {
            AssetDatabase.Refresh();
            var parts = BuildKitPrefabs();
            foreach (var entry in OpponentKit)
            {
                var path = ConfigsFolder + "/Opponents/" + entry.id + ".asset";
                var opponent = AssetDatabase.LoadAssetAtPath<OpponentConfig>(path);
                if (opponent == null)
                {
                    Debug.LogError("[Zonk] No opponent " + path);
                    continue;
                }

                opponent.Accessory = KitPart(parts, entry.head);
                opponent.ChestPiece = KitPart(parts, entry.chest);
                opponent.HeldProp = KitPart(parts, entry.prop);
                EditorUtility.SetDirty(opponent);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Zonk] Opponent kit applied: " + parts.Count + " parts");
        }

        private static GameObject KitPart(Dictionary<string, GameObject> parts, string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (parts.TryGetValue(name, out var prefab))
                return prefab;

            Debug.LogError("[Zonk] No opponent part " + name + ": run Tools/Blender/zonk_oppkit.py");
            return null;
        }

        private static Dictionary<string, GameObject> BuildKitPrefabs()
        {
            EnsureFolder(KitPrefabs);
            var material = KitMaterial();
            var parts = new Dictionary<string, GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { KitModels }))
            {
                var modelPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(modelPath) is ModelImporter importer)
                {
                    importer.materialImportMode = ModelImporterMaterialImportMode.None;
                    importer.importAnimation = false;
                    importer.importBlendShapes = false;
                    importer.importCameras = false;
                    importer.importLights = false;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.None;
                    importer.SaveAndReimport();
                }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var path = KitPrefabs + "/" + model.name + ".prefab";
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                parts[model.name] = PrefabUtility.SaveAsPrefabAsset(instance, path);
                Object.DestroyImmediate(instance);
            }

            return parts;
        }

        private static Material KitMaterial()
        {
            var texturePath = Root + "/Art/Textures/OppKit_Palette.png";
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
            {
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            var path = Root + "/Art/Textures/M_OppKit.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(ToonShader)) { name = "M_OppKit" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
