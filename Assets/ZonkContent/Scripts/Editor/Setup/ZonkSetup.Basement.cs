using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Подпольный клуб Крота со светом, запечённым в Tools/Blender/zonk_basement.py: модель Room_Basement, текстуры Basement_Room, Wall, Club, Rig.
    /// Живые части поверх: крот (Tools/Blender/zonk_basement_life.py), мерцание неона и автомата, пар и капли из трубы.
    /// Лампочки на проводах качает Zonk/BakedSway. Префаб Env_Basement остаётся тем же файлом.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string BasementModel = Root + "/Art/Models/Environments/Room_Basement.fbx";
        private const string BasementMoleModel = Root + "/Art/Models/Environments/Basement_Mole.fbx";

        // Положения те же, что в zonk_basement.py: нора, вывеска, автомат, конец трубы.
        private static readonly Vector2 MoleBurrow = new Vector2(1.7f, 4.35f);
        private const float MoleHeight = -1.68f;
        private const float MoleYaw = 12f;
        private const float BasementWallZ = 5.28f;
        private const float BasementPipeX = 4.5f;
        private const float BasementPipeZ = 3.45f;
        private const float BasementPipeY = -0.48f;
        private const int BasementDrips = 6;

        [MenuItem("Zonk/Art/Apply Basement Room", priority = 52)]
        public static void ApplyBasementRoomMenu()
        {
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(BasementModel) == null)
            {
                Debug.LogError("[Zonk] No model " + BasementModel + ": run Tools/Blender/zonk_basement.py -- --bake");
                return;
            }

            var room = BakedMaterial("M_Basement_Room", "Basement_Room", 2048);
            var wall = BakedMaterial("M_Basement_Wall", "Basement_Wall", 1024);
            var club = BakedMaterial("M_Basement_Club", "Basement_Club", 1024);
            // Лампочки рисует M_Basement_Sway, BakedMaterial нужен ради настроек импорта текстуры.
            BakedMaterial("M_Basement_Rig", "Basement_Rig", 256);
            var sway = SwayMaterial("M_Basement_Sway", "Basement_Rig");
            var smoke = SmokeWispMaterial();
            var creatures = KitMaterial();
            ApplyBakedRoom(Prefabs + "/Environments/Env_Basement.prefab", BasementModel, "zonk_basement.py", room, wall, root =>
            {
                AddBasementGlows(root);
                AddPipeSteam(root, smoke);
                AddPipeDrips(root);
                AddMole(root, creatures);
            }, sway, null, meshName =>
            {
                if (meshName.Contains("Wall"))
                    return wall;
                return meshName.Contains("Club") ? club : null;
            });

            var profile = AssetDatabase.LoadAssetAtPath<LightingProfileConfig>(ConfigsFolder + "/Lighting/Lighting_Basement.asset");
            if (profile != null)
            {
                // Запечённый подвал тумана не принимает: с ним стол темнел бы отдельно. Рассеянный свет слабый, но руки и кости не должны тонуть.
                profile.Fog = false;
                profile.AmbientSky = new Color(0.17f, 0.13f, 0.18f);
                profile.AmbientEquator = new Color(0.16f, 0.11f, 0.13f);
                profile.AmbientGround = new Color(0.1f, 0.06f, 0.07f);
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Zonk] Basement room applied");
        }

        /// <summary>Неон неровно дышит: пятна цвета поверх запечённого, источников нет.</summary>
        private static void AddBasementGlows(Transform root)
        {
            var sprite = SpriteAt(Root + "/Art/Textures/Particle_SoftDot.png");
            if (sprite == null)
                return;

            var size = sprite.bounds.size;

            void Glow(string name, Vector3 position, Vector2 scale, Color color, float depth, float speed)
            {
                var go = SpriteObject(name, root, position, 1f, sprite);
                go.transform.localScale = new Vector3(scale.x / Mathf.Max(size.x, 0.01f), scale.y / Mathf.Max(size.y, 0.01f), 1f);
                go.AddComponent<WorldGlowPulse>().EditorSetup(go.GetComponent<SpriteRenderer>(), color, depth, speed);
            }

            Glow("NeonPink", new Vector3(0f, 2.25f, BasementWallZ), new Vector2(3.1f, 3.1f), new Color(1f, 0.25f, 0.62f, 0.3f), 0.55f, 5.2f);
            Glow("NeonCyan", new Vector3(-1.12f, 2.25f, BasementWallZ), new Vector2(1.9f, 1.9f), new Color(0.25f, 0.9f, 1f, 0.26f), 0.45f, 3.3f);
            Glow("NeonCyan", new Vector3(1.12f, 2.25f, BasementWallZ), new Vector2(1.9f, 1.9f), new Color(0.25f, 0.9f, 1f, 0.26f), 0.45f, 4.1f);
            Glow("JukeGlow", new Vector3(3.55f, -0.65f, 4.05f), new Vector2(2.4f, 2.6f), new Color(1f, 0.45f, 0.55f, 0.28f), 0.5f, 2.7f);
        }

        /// <summary>Пар с конца трубы: тот же дымок, что над огнём, плоскость с шумом.</summary>
        private static void AddPipeSteam(Transform root, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "PipeSteam";
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(root, false);
            quad.transform.localPosition = new Vector3(BasementPipeX, BasementPipeY + 0.5f, BasementPipeZ);
            quad.transform.localScale = new Vector3(0.9f, 1f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        /// <summary>Капли с трубы в лужу: редкие частицы с тяготением, без столкновений.</summary>
        private static void AddPipeDrips(Transform root)
        {
            var go = new GameObject("PipeDrips");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(BasementPipeX, BasementPipeY, BasementPipeZ);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.duration = 3f;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 0.05f;
            main.startColor = new Color(0.75f, 0.85f, 0.95f, 0.8f);
            main.gravityModifier = 1f;
            main.maxParticles = BasementDrips;

            var emission = system.emission;
            emission.rateOverTime = 1.1f;

            var shape = system.shape;
            shape.enabled = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2.2f;
            renderer.velocityScale = 0.02f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = ParticleMaterial("M_PipeDrip", false, true);
        }

        private static void AddMole(Transform root, Material material)
        {
            var model = CreatureInstance("Mole", BasementMoleModel, root, material);
            if (model == null)
                return;

            var rootBone = FindDeep(model, "Root");
            var body = FindDeep(model, "Body");
            var pawLeft = FindDeep(model, "PawL");
            var pawRight = FindDeep(model, "PawR");
            FaceCamera(model, rootBone, body, -Vector3.Cross(pawRight.position - pawLeft.position, body.position - rootBone.position));

            var holder = model.parent;
            holder.localPosition = new Vector3(MoleBurrow.x, MoleHeight, MoleBurrow.y);
            holder.localRotation = Quaternion.Euler(0f, MoleYaw, 0f);
            holder.gameObject.AddComponent<MolePeek>().EditorSetup(FindDeep(model, "Head"), pawLeft, pawRight, MoleHeight);
        }
    }
}
