using UnityEditor;
using UnityEngine;

using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Розарий графини Розы со светом, запечённым в Tools/Blender/zonk_garden.py: модель Room_Garden, текстуры Garden_Room, Arbor, Beds, Bush, Crown, Rig, Far, Sky.
    /// Живые части поверх: рябь в прудике, три бабочки, лягушка (Tools/Blender/zonk_garden_life.py).
    /// Листву, яблоки, качели, цветы, камыш и бельё качает Zonk/BakedSway. Префаб Env_Garden остаётся тем же файлом.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string GardenModel = Root + "/Art/Models/Environments/Room_Garden.fbx";
        private const string GardenPondModel = Root + "/Art/Models/Environments/Garden_Pond.fbx";
        private const string GardenFrogModel = Root + "/Art/Models/Environments/Garden_Frog.fbx";
        private const string PondMaterialPath = Root + "/Art/Shaders/M_Water_Pond.mat";

        // Положения те же, что в zonk_garden.py: прудик, камень лягушки, кувшинки.
        private const float PondX = -6.9f;
        private const float PondZ = 0.65f;
        private const float PondHeight = 0.034f;

        private static readonly Vector3[] FrogSpots =
        {
            new Vector3(-5.09f, RoomFloor + 0.4f, 0.37f),
            new Vector3(-6.15f, RoomFloor + 0.05f, 0.35f),
            new Vector3(-7.5f, RoomFloor + 0.05f, 0.45f),
        };

        // Бабочки: над клумбой (видно в партии справа), над розами за столом, у прудика и беседки.
        private static readonly (string Letter, Vector3 Center, Vector3 Range, float Speed, float Offset)[] Butterflies =
        {
            ("A", new Vector3(6.1f, -0.75f, 0.6f), new Vector3(1.15f, 0.28f, 1f), 0.55f, 0f),
            ("B", new Vector3(-1.6f, -0.55f, 5.5f), new Vector3(1.3f, 0.3f, 0.45f), 0.48f, 2.1f),
            ("C", new Vector3(3.3f, -0.6f, 5.55f), new Vector3(1.25f, 0.28f, 0.42f), 0.6f, 4.3f),
        };

        [MenuItem("Zonk/Art/Apply Garden Room", priority = 50)]
        public static void ApplyGardenRoomMenu()
        {
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(GardenModel) == null)
            {
                Debug.LogError("[Zonk] No model " + GardenModel + ": run Tools/Blender/zonk_garden.py -- --bake");
                return;
            }

            var room = BakedMaterial("M_Garden_Room", "Garden_Room", 2048);
            var arbor = BakedMaterial("M_Garden_Arbor", "Garden_Arbor", 1024);
            var beds = BakedMaterial("M_Garden_Beds", "Garden_Beds", 1024);
            var bush = BakedMaterial("M_Garden_Bush", "Garden_Bush", 1024);
            var far = BakedMaterial("M_Garden_Far", "Garden_Far", 128);
            // Качающееся рисуют M_Garden_Sway и M_Garden_Crown, BakedMaterial нужен ради настроек импорта текстуры.
            BakedMaterial("M_Garden_Rig", "Garden_Rig", 512);
            BakedMaterial("M_Garden_CrownBaked", "Garden_Crown", 1024);
            var sky = BakedMaterial("M_Garden_Sky", "Garden_Sky", 1024);
            var sway = SwayMaterial("M_Garden_Sway", "Garden_Rig");
            var crown = SwayMaterial("M_Garden_Crown", "Garden_Crown");
            var pond = PondMaterial();
            var creatures = KitMaterial();
            ApplyBakedRoom(Prefabs + "/Environments/Env_Garden.prefab", GardenModel, "zonk_garden.py", room, arbor, root =>
            {
                AddPondWater(root, pond);
                foreach (var butterfly in Butterflies)
                    AddButterfly(root, creatures, butterfly.Letter, butterfly.Center, butterfly.Range, butterfly.Speed, butterfly.Offset);
                AddFrog(root, creatures);
            }, sway, sky, meshName =>
            {
                if (meshName.Contains("SwayCrown"))
                    return crown;
                if (meshName.Contains("Bush"))
                    return bush;
                if (meshName.Contains("Arbor"))
                    return arbor;
                if (meshName.Contains("Beds"))
                    return beds;
                return meshName.Contains("Far") ? far : null;
            });

            var profile = AssetDatabase.LoadAssetAtPath<LightingProfileConfig>(ConfigsFolder + "/Lighting/Lighting_Garden.asset");
            if (profile != null)
            {
                // Запечённый сад тумана не принимает: с ним стол выцветал бы отдельно.
                profile.Fog = false;
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Zonk] Garden room applied");
        }

        private static Material PondMaterial()
        {
            var beach = WaterMaterial();
            var material = AssetDatabase.LoadAssetAtPath<Material>(PondMaterialPath);
            if (material == null)
            {
                material = new Material(beach) { name = "M_Water_Pond" };
                AssetDatabase.CreateAsset(material, PondMaterialPath);
            }

            material.SetFloat("_OpenSea", 1f);
            material.SetFloat("_OpenDepth", 2.2f);
            material.SetFloat("_Drift", 2f);
            material.SetFloat("_Swell", 0f);
            material.SetFloat("_HazeFrom", 200f);
            material.SetFloat("_HazeTo", 300f);
            material.SetFloat("_ScaleA", 0.75f);
            material.SetFloat("_ScaleB", 0.45f);
            material.SetColor("_Shallow", new Color(0.36f, 0.78f, 0.84f));
            material.SetColor("_Middle", new Color(0.26f, 0.68f, 0.78f));
            material.SetColor("_Deep", new Color(0.2f, 0.58f, 0.72f));
            material.SetColor("_Lines", new Color(0.72f, 0.94f, 0.96f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AddPondWater(Transform root, Material material)
        {
            if (AssetImporter.GetAtPath(GardenPondModel) is ModelImporter importer && importer.materialImportMode != ModelImporterMaterialImportMode.None)
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
            }

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(GardenPondModel);
            if (asset == null)
            {
                Debug.LogError("[Zonk] No model " + GardenPondModel + ": run Tools/Blender/zonk_garden_life.py");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, root);
            instance.name = "PondWater";
            instance.transform.localPosition = new Vector3(PondX, RoomFloor + PondHeight, PondZ);
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private static void AddButterfly(Transform root, Material material, string letter, Vector3 center, Vector3 range, float speed, float offset)
        {
            var model = CreatureInstance("Butterfly" + letter, Root + "/Art/Models/Environments/Garden_Butterfly" + letter + ".fbx", root, material);
            if (model == null)
                return;

            var body = FindDeep(model, "Body");
            var wingLeft = FindDeep(model, "WingL");
            var wingRight = FindDeep(model, "WingR");

            // У бабочки нет костей головы и хвоста: «вперёд» считаем из осей крыльев и верха самой модели, а не мира.
            var rootBone = FindDeep(model, "Root");
            var modelUp = body.position - rootBone.position;
            FaceCamera(model, rootBone, body, -Vector3.Cross(wingRight.position - wingLeft.position, modelUp));
            var holder = model.parent;
            holder.localPosition = center;
            holder.gameObject.AddComponent<ButterflyFlutter>().EditorSetup(wingLeft, wingRight, center, range, speed, offset);
        }

        private static void AddFrog(Transform root, Material material)
        {
            var model = CreatureInstance("Frog", GardenFrogModel, root, material);
            if (model == null)
                return;

            var body = FindDeep(model, "Body");
            var throat = FindDeep(model, "Throat");
            var back = FindDeep(model, "LegBL");
            var front = FindDeep(model, "LegFL");
            FaceCamera(model, FindDeep(model, "Root"), body, front.position - back.position);

            var holder = model.parent;
            holder.localPosition = FrogSpots[0];
            holder.gameObject.AddComponent<FrogHop>().EditorSetup(body, throat, FindDeep(model, "Lids"),
                new[] { back, FindDeep(model, "LegBR") }, new[] { front, FindDeep(model, "LegFR") }, FrogSpots);
        }
    }
}
