using UnityEditor;
using UnityEngine;

using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Корабль со светом, запечённым в Tools/Blender/zonk_ship.py: модель Room_Ship, текстуры Ship_Room, Cabin, Hull, Gear, Rig, Flag, Sky.
    /// Живые части поверх: штормовое море (Zonk/Water, материал M_Water_Storm), мерцание окон и трюма, огонь кормового фонаря, крыса.
    /// Флаг, фонари на цепях, колокол и снасти качает Zonk/BakedSway. Дождь и молнии — в профиле Lighting_Ship.
    /// Префаб Env_Ship остаётся тем же файлом, ссылки на него не меняются.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string ShipModel = Root + "/Art/Models/Environments/Room_Ship.fbx";
        private const string RatModel = Root + "/Art/Models/Environments/Ship_Rat.fbx";
        private const string StormMaterialPath = Root + "/Art/Shaders/M_Water_Storm.mat";

        // Положения те же, что в zonk_ship.py.
        private const float ShipWallZ = 6.49f;
        private const float SeaHeight = -3.6f;
        private const float SeaSize = 130f;
        private const float StormDrift = 3.5f;

        // Маршрут крысы: от укрытия за столом к двери и за мачту. Прямые между точками не идут сквозь трап и мачту.
        private static readonly Vector2[] RatRoute =
        {
            new Vector2(-1.2f, 4.15f), new Vector2(-1.1f, 5.55f), new Vector2(0.9f, 5.9f), new Vector2(3.5f, 5.95f),
        };

        [MenuItem("Zonk/Art/Apply Ship Room", priority = 49)]
        public static void ApplyShipRoomMenu()
        {
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ShipModel) == null)
            {
                Debug.LogError("[Zonk] No model " + ShipModel + ": run Tools/Blender/zonk_ship.py -- --bake");
                return;
            }

            ImportComicSprites();
            var room = BakedMaterial("M_Ship_Room", "Ship_Room", 2048);
            var cabin = BakedMaterial("M_Ship_Cabin", "Ship_Cabin", 1024);
            var hull = BakedMaterial("M_Ship_Hull", "Ship_Hull", 1024);
            var gear = BakedMaterial("M_Ship_Gear", "Ship_Gear", 1024);
            // Снасти рисует M_Ship_Sway, BakedMaterial нужен ради настроек импорта текстуры.
            BakedMaterial("M_Ship_Rig", "Ship_Rig", 512);
            BakedMaterial("M_Ship_FlagBaked", "Ship_Flag", 256);
            var sky = BakedMaterial("M_Ship_Sky", "Ship_Sky", 1024);
            var sway = SwayMaterial("M_Ship_Sway", "Ship_Rig");
            var flag = SwayMaterial("M_Ship_Flag", "Ship_Flag");
            var storm = StormMaterial();
            var creatures = KitMaterial();
            ApplyBakedRoom(Prefabs + "/Environments/Env_Ship.prefab", ShipModel, "zonk_ship.py", room, cabin, root =>
            {
                AddStormSea(root, storm);
                AddShipGlows(root);
                AddCandle(root, new Vector3(0.75f, 2.5f, 11.95f));
                AddRat(root, creatures);
            }, sway, sky, meshName =>
            {
                if (meshName.Contains("SwayFlag"))
                    return flag;
                if (meshName.Contains("Cabin"))
                    return cabin;
                if (meshName.Contains("Hull"))
                    return hull;
                return meshName.Contains("Gear") ? gear : null;
            });

            TuneShipLighting();
            // Дождь — общий префаб эффекта: без этого вызова его настройки доходят только через Build Everything.
            RainPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[Zonk] Ship room applied");
        }

        private static Material StormMaterial()
        {
            // Берём настроенные ячейки берега, свои только цвета и шторм. Числа задаёт генератор: подбирать здесь, а не в материале.
            var beach = WaterMaterial();
            var material = AssetDatabase.LoadAssetAtPath<Material>(StormMaterialPath);
            if (material == null)
            {
                material = new Material(beach) { name = "M_Water_Storm" };
                AssetDatabase.CreateAsset(material, StormMaterialPath);
            }

            material.SetFloat("_OpenSea", 1f);
            material.SetFloat("_OpenDepth", 4f);
            material.SetFloat("_Drift", StormDrift);
            material.SetFloat("_Swell", 0.42f);
            material.SetFloat("_SwellSpeed", 0.95f);
            material.SetFloat("_LineFade", 14f);
            material.SetColor("_Shallow", new Color(0.06f, 0.24f, 0.32f));
            material.SetColor("_Middle", new Color(0.04f, 0.18f, 0.28f));
            material.SetColor("_Deep", new Color(0.03f, 0.12f, 0.22f));
            // Даль в цвет неба у горизонта (SEA_FAR в zonk_ship.py): иначе виден шов.
            material.SetColor("_Far", new Color(0.07f, 0.17f, 0.25f));
            // Ячейки приглушены: светлая сетка у камеры спорила с кораблём.
            material.SetColor("_Lines", new Color(0.17f, 0.32f, 0.4f));
            material.SetFloat("_HazeFrom", 14f);
            material.SetFloat("_HazeTo", 50f);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Море вокруг всего корабля, ниже палубы: виден только край за бортом.</summary>
        private static void AddStormSea(Transform root, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Sea";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            quad.transform.localPosition = new Vector3(0f, SeaHeight, 0f);
            quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            quad.transform.localScale = new Vector3(SeaSize, SeaSize, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Огонь за окнами и в трюме неровно дышит: пятна поверх запечённого света, источников нет.</summary>
        private static void AddShipGlows(Transform root)
        {
            var sprite = SpriteAt(Root + "/Art/Textures/Particle_SoftDot.png");
            if (sprite == null)
                return;

            var size = sprite.bounds.size;

            void Glow(string name, Vector3 position, Vector3 euler, Vector2 scale, Color color, float depth, float speed)
            {
                var go = SpriteObject(name, root, position, 1f, sprite);
                go.transform.localRotation = Quaternion.Euler(euler);
                go.transform.localScale = new Vector3(scale.x / Mathf.Max(size.x, 0.01f), scale.y / Mathf.Max(size.y, 0.01f), 1f);
                go.AddComponent<WorldGlowPulse>().EditorSetup(go.GetComponent<SpriteRenderer>(), color, depth, speed);
            }

            var window = new Color(1f, 0.6f, 0.22f, 0.5f);
            Glow("WindowGlow", new Vector3(-3.4f, -0.4f, ShipWallZ - 0.1f), Vector3.zero, new Vector2(1.7f, 1.4f), window, 0.65f, 2.1f);
            Glow("WindowGlow", new Vector3(3.7f, -0.4f, ShipWallZ - 0.1f), Vector3.zero, new Vector2(1.7f, 1.4f), window, 0.65f, 2.9f);
            Glow("DoorGlow", new Vector3(-0.2f, -0.37f, ShipWallZ - 0.12f), Vector3.zero, new Vector2(0.7f, 0.7f), window, 0.6f, 3.4f);
            Glow("HoldGlow", new Vector3(-3.65f, RoomFloor + 0.19f, -3.1f), new Vector3(90f, 0f, 0f), new Vector2(2.2f, 1.8f), new Color(1f, 0.5f, 0.15f, 0.4f), 0.7f, 1.8f);
        }

        private static void AddRat(Transform root, Material material)
        {
            var model = CreatureInstance("Rat", RatModel, root, material);
            if (model == null)
                return;

            var body = FindDeep(model, "Body");
            var head = FindDeep(model, "Head");
            var tail = FindDeep(model, "TailA");
            FaceCamera(model, FindDeep(model, "Root"), body, head.position - tail.position);

            var holder = model.parent;
            holder.localPosition = new Vector3(RatRoute[0].x, RoomFloor, RatRoute[0].y);
            var legs = new[] { FindDeep(model, "LegFL"), FindDeep(model, "LegFR"), FindDeep(model, "LegBL"), FindDeep(model, "LegBR") };
            holder.gameObject.AddComponent<RatScurry>().EditorSetup(body, head, tail, FindDeep(model, "TailB"), legs, RatRoute);

            // Палуба запечена и живых теней не принимает: пятно бегает вместе с крысой.
            var sprite = SpriteAt(Root + "/Art/Textures/Particle_SoftDot.png");
            if (sprite == null)
                return;

            var shadow = SpriteObject("RatShadow", holder, new Vector3(0f, 0.03f, 0f), 1f, sprite);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var size = sprite.bounds.size;
            shadow.transform.localScale = new Vector3(0.62f / Mathf.Max(size.x, 0.01f), 1f / Mathf.Max(size.y, 0.01f), 1f);
            shadow.GetComponent<SpriteRenderer>().color = new Color(0.04f, 0.02f, 0.02f, 0.6f);
        }

        /// <summary>Стол, кости и руки живые: тёплая лампа над столом и слабая холодная луна, как запечено в комнате.</summary>
        private static void TuneShipLighting()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingProfileConfig>(ConfigsFolder + "/Lighting/Lighting_Ship.asset");
            if (profile == null)
                return;

            profile.SunColor = new Color(0.62f, 0.72f, 1f);
            profile.SunIntensity = 0.4f;
            profile.SunRotation = new Vector3(55f, 150f, 0f);
            profile.AmbientSky = new Color(0.3f, 0.32f, 0.42f);
            profile.AmbientEquator = new Color(0.3f, 0.25f, 0.22f);
            profile.AmbientGround = new Color(0.2f, 0.13f, 0.09f);
            profile.Background = new Color(0.05f, 0.07f, 0.12f);
            profile.LampIntensity = 1.3f;
            profile.TableLight = 1.1f;
            profile.TableLightColor = new Color(1f, 0.82f, 0.6f);
            // Запечённая палуба тумана не принимает: с ним стол темнел бы отдельно от неё.
            profile.Fog = false;
            EditorUtility.SetDirty(profile);
        }
    }
}
