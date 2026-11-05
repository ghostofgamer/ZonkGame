using UnityEditor;
using UnityEngine;

using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Пляж со светом, запечённым в Tools/Blender/zonk_beach.py: модель Room_Beach, текстуры Beach_Room, Beach_Props, Beach_Sky.
    /// Живые части поверх: вода (шейдер Zonk/Water, материал M_Water), дымок от костра; листья пальм качает Zonk/BakedSway.
    /// Попугай и краб — отдельные модели со скелетом (Tools/Blender/zonk_beach_life.py), двигает их код.
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

        // Жёрдочка на стойке бара (perch в zonk_beach.py): лапы на её перекладине, птица повёрнута к камере партии.
        private static readonly Vector3 ParrotSpot = new Vector3(-5.63f, 0.026f, 3.15f);
        private const float ParrotYaw = -32f;
        private const string ParrotModel = Root + "/Art/Models/Environments/Beach_Parrot.fbx";
        private const string CrabModel = Root + "/Art/Models/Environments/Beach_Crab.fbx";

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
            var creatures = KitMaterial();
            ApplyBakedRoom(Prefabs + "/Environments/Env_Beach.prefab", BeachModel, "zonk_beach.py", room, bar, root =>
            {
                AddWater(root, water);
                AddSmoke(root, smoke, BeachFireX, BeachFireZ);
                AddParrot(root, creatures);
                AddCrab(root, creatures);
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

        private static void AddParrot(Transform root, Material material)
        {
            var model = CreatureInstance("Parrot", ParrotModel, root, material);
            if (model == null)
                return;

            var body = FindDeep(model, "Body");
            var head = FindDeep(model, "Head");
            var tail = FindDeep(model, "Tail");
            FaceCamera(model, FindDeep(model, "Root"), body, head.position - tail.position);

            var holder = model.parent;
            holder.localPosition = ParrotSpot;
            holder.localRotation = Quaternion.Euler(0f, ParrotYaw, 0f);
            holder.gameObject.AddComponent<ParrotPerch>().EditorSetup(body, head, FindDeep(model, "WingL"), FindDeep(model, "WingR"), tail);
        }

        private static void AddCrab(Transform root, Material material)
        {
            var model = CreatureInstance("Crab", CrabModel, root, material);
            if (model == null)
                return;

            var body = FindDeep(model, "Body");
            var clawLeft = FindDeep(model, "ClawL");
            var clawRight = FindDeep(model, "ClawR");
            FaceCamera(model, FindDeep(model, "Root"), body, (clawLeft.position + clawRight.position) * 0.5f - body.position);

            var holder = model.parent;
            holder.localPosition = new Vector3(1f, RoomFloor, 4.8f);
            // Через одну в разных группах шага: L0 R1 L2 ступают вместе, R0 L1 R2 вместе.
            var legs = new[]
            {
                FindDeep(model, "LegL0"), FindDeep(model, "LegR0"),
                FindDeep(model, "LegR1"), FindDeep(model, "LegL1"),
                FindDeep(model, "LegL2"), FindDeep(model, "LegR2"),
            };

            holder.gameObject.AddComponent<CrabScuttle>().EditorSetup(body, clawLeft, clawRight, FindDeep(model, "PincerL"), FindDeep(model, "PincerR"), legs);

            // Песок запечён и живых теней не принимает: под крабом мягкое пятно, оно бегает вместе с ним.
            var sprite = SpriteAt(Root + "/Art/Textures/Particle_SoftDot.png");
            if (sprite == null)
                return;

            var shadow = SpriteObject("CrabShadow", holder, new Vector3(0f, 0.035f, 0f), 1f, sprite);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var size = sprite.bounds.size;
            shadow.transform.localScale = new Vector3(1.05f / Mathf.Max(size.x, 0.01f), 0.62f / Mathf.Max(size.y, 0.01f), 1f);
            shadow.GetComponent<SpriteRenderer>().color = new Color(0.16f, 0.09f, 0.03f, 0.55f);
        }

        /// <summary>Модель со скелетом внутри пустого объекта: объект двигает код, модель разворачивается внутри него по костям.</summary>
        private static Transform CreatureInstance(string name, string modelPath, Transform root, Material material)
        {
            if (AssetImporter.GetAtPath(modelPath) is ModelImporter importer)
                ConfigureHandModel(importer);

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (asset == null)
            {
                Debug.LogError("[Zonk] No model " + modelPath + ": run Tools/Blender/zonk_beach_life.py");
                return null;
            }

            var holder = new GameObject(name).transform;
            holder.SetParent(root, false);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, holder);
            instance.name = "Model";
            var renderer = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                // Границы взяты с позы покоя: раскрытые крылья и клешни выходят за них.
                var bounds = renderer.localBounds;
                bounds.Expand(0.6f);
                renderer.localBounds = bounds;
            }
            else
            {
                Debug.LogError("[Zonk] " + modelPath + ": no SkinnedMeshRenderer. Check Rig: Generic");
            }

            return instance.transform;
        }

        /// <summary>Развернуть модель по костям: у модели со скелетом оси из Blender приходят иначе, чем у обычной. Вверх — +Y, лицом — −Z.</summary>
        private static void FaceCamera(Transform model, Transform rootBone, Transform upperBone, Vector3 forward)
        {
            var up = (upperBone.position - rootBone.position).normalized;
            var facing = Vector3.ProjectOnPlane(forward, up).normalized;
            var turn = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(facing, up));
            model.rotation = turn * model.rotation;
            model.position += model.parent.position - rootBone.position;
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
