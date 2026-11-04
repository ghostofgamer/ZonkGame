using UnityEditor;
using UnityEngine;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Пляж со светом, запечённым в Tools/Blender/zonk_beach.py: модель Room_Beach, текстуры Beach_Room, Beach_Props, Beach_Sky.
    /// Живые части поверх: вода (шейдер Zonk/Water, материал M_Water), дымок от костра; листья и зонт качает Zonk/BakedSway.
    /// Префаб Env_Beach остаётся тем же файлом. «Бухта на закате» — та же модель, тёплее (BakedTint профиля).
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string WaterMaterialPath = Root + "/Art/Shaders/M_Water.mat";
        private const string BeachModel = Root + "/Art/Models/Environments/Room_Beach.fbx";

        // Плоскость воды: от песка перед столом до задника локации. Положения те же, что в скрипте.
        private const float WaterHeight = -1.59f;
        private const float WaterNear = 3.2f;
        private const float WaterFar = 49f;
        private const float WaterWidth = 90f;
        private const float BeachFireX = 5f;
        private const float BeachFireZ = 1.55f;

        [MenuItem("Zonk/Art/Apply Beach Room", priority = 48)]
        public static void ApplyBeachRoomMenu()
        {
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BeachModel) == null)
                return;

            var room = BakedMaterial("M_Beach_Room", "Beach_Room", 2048);
            // Предметы в нескольких текстурах: бар, костёр с рыбой, лодка, пальмы. Имя сетки = имя группы в zonk_beach.py.
            var bar = BakedMaterial("M_Beach_Bar", "Beach_Bar", 1024);
            var camp = BakedMaterial("M_Beach_Camp", "Beach_Camp", 1024);
            var boat = BakedMaterial("M_Beach_Boat", "Beach_Boat", 512);
            var palm = BakedMaterial("M_Beach_Palm", "Beach_Palm", 512);
            var far = BakedMaterial("M_Beach_Far", "Beach_Far", 128);
            var sky = BakedMaterial("M_Beach_Sky", "Beach_Sky", 1024);
            var sway = SwayMaterial("M_Beach_Sway", "Beach_Palm");
            var water = WaterMaterial();
            var smoke = SmokeWispMaterial();
            ApplyBakedRoom(Prefabs + "/Environments/Env_Beach.prefab", BeachModel, "zonk_beach.py", room, bar, root =>
            {
                AddWater(root, water);
                AddSmoke(root, smoke, BeachFireX, BeachFireZ);
            }, sway, sky, meshName =>
            {
                if (meshName.Contains("Camp"))
                    return camp;
                if (meshName.Contains("Boat"))
                    return boat;
                if (meshName.Contains("Palm"))
                    return palm;
                if (meshName.Contains("Far"))
                    return far;
                return meshName.Contains("Bar") ? bar : null;
            });

            SetBakedTint("Lighting_BeachSunset", new Color(1f, 0.62f, 0.46f));
            AssetDatabase.SaveAssets();
            Debug.Log("[Zonk] Beach room applied");
        }

        private static Material SwayMaterial(string name, string texture)
        {
            var path = RoomTextures + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Zonk/BakedSway")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(RoomTextures + "/" + texture + ".png"));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material WaterMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Zonk/Water")) { name = "M_Water" };
                AssetDatabase.CreateAsset(material, WaterMaterialPath);
            }

            // Ячейки маленькие и повторяются: своя текстура, по одной плитке на несколько метров.
            var cellsPath = Root + "/Art/Textures/Water_Cells.png";
            ConfigureTexture(cellsPath, 256, 75);
            if (AssetImporter.GetAtPath(cellsPath) is TextureImporter cells && cells.wrapMode != TextureWrapMode.Repeat)
            {
                cells.wrapMode = TextureWrapMode.Repeat;
                cells.sRGBTexture = false;
                cells.SaveAndReimport();
            }

            material.SetTexture("_Cells", AssetDatabase.LoadAssetAtPath<Texture2D>(cellsPath));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AddWater(Transform root, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Water";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            quad.transform.localPosition = new Vector3(0f, WaterHeight, (WaterNear + WaterFar) * 0.5f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(WaterWidth, WaterFar - WaterNear, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
