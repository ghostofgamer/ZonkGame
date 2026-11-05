using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Чердак старьёвщика Тихона со светом, запечённым в Tools/Blender/zonk_attic.py: модель Room_Attic, текстуры Attic_Room, Frame, Gear, Rig.
    /// Живые части поверх: кот (Tools/Blender/zonk_attic_life.py), луч из окна, парящая в нём пыль, дыхание света за стеклом.
    /// Маятник, лошадку, простыню, клетку и травы качает Zonk/BakedSway. Префаб Env_Attic остаётся тем же файлом.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string AtticModel = Root + "/Art/Models/Environments/Room_Attic.fbx";
        private const string AtticCatModel = Root + "/Art/Models/Environments/Attic_Cat.fbx";

        // Положения те же, что в zonk_attic.py: окно, сундук с котом.
        private static readonly Vector3 AtticWindow = new Vector3(0f, 2.05f, 5.55f);
        private static readonly Vector3 AtticBeamEnd = new Vector3(-1.5f, -1.55f, 2.7f);
        private static readonly Vector3 AtticCatSpot = new Vector3(-0.65f, -0.8f, 4.45f);
        private const float AtticCatYaw = -18f;
        private const float AtticBeamWidth = 1.7f;
        private const int AtticDust = 36;

        [MenuItem("Zonk/Art/Apply Attic Room", priority = 51)]
        public static void ApplyAtticRoomMenu()
        {
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(AtticModel) == null)
            {
                Debug.LogError("[Zonk] No model " + AtticModel + ": run Tools/Blender/zonk_attic.py -- --bake");
                return;
            }

            var room = BakedMaterial("M_Attic_Room", "Attic_Room", 2048);
            var frame = BakedMaterial("M_Attic_Frame", "Attic_Frame", 1024);
            var gear = BakedMaterial("M_Attic_Gear", "Attic_Gear", 1024);
            // Качающееся рисует M_Attic_Sway, BakedMaterial нужен ради настроек импорта текстуры.
            BakedMaterial("M_Attic_Rig", "Attic_Rig", 1024);
            var sway = SwayMaterial("M_Attic_Sway", "Attic_Rig");
            var creatures = KitMaterial();
            ApplyBakedRoom(Prefabs + "/Environments/Env_Attic.prefab", AtticModel, "zonk_attic.py", room, frame, root =>
            {
                AddAtticBeam(root);
                AddAtticDust(root);
                AddCat(root, creatures);
            }, sway, null, meshName =>
            {
                if (meshName.Contains("Frame"))
                    return frame;
                return meshName.Contains("Gear") ? gear : null;
            });

            var profile = AssetDatabase.LoadAssetAtPath<LightingProfileConfig>(ConfigsFolder + "/Lighting/Lighting_Attic.asset");
            if (profile != null)
            {
                // Запечённый чердак тумана не принимает: с ним стол темнел бы отдельно.
                profile.Fog = false;
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Zonk] Attic room applied");
        }

        /// <summary>Текстура луча: ярко у окна, к концу гаснет, края мягкие. Наверху полосы от рамы окна.</summary>
        private static Texture2D AtticBeamTexture()
        {
            var path = Root + "/Art/Textures/Particle_AtticBeam.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            {
                var along = (y + 0.5f) / size;
                for (var x = 0; x < size; x++)
                {
                    var across = Mathf.Abs((x + 0.5f) / size * 2f - 1f);
                    var side = Mathf.Clamp01(1f - across);
                    side = side * side * (3f - 2f * side);
                    var stripes = 0.82f + 0.18f * Mathf.Sin(((x + 0.5f) / size) * Mathf.PI * 10f);
                    var fade = along * along;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, side * stripes * fade));
                }
            }

            EnsureFolder(ParentFolder(path));
            SavePng(path, texture);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Луч из окна: две скрещённые плоскости, сложение цвета. Настоящего света нет: пол запечён, поэтому дёшево.</summary>
        private static void AddAtticBeam(Transform root)
        {
            var material = ParticleMaterial("M_AtticBeam", true, false);
            material.SetTexture("_BaseMap", AtticBeamTexture());
            material.SetColor("_BaseColor", new Color(1f, 0.93f, 0.76f, 0.2f));
            EditorUtility.SetDirty(material);

            var direction = AtticBeamEnd - AtticWindow;
            var center = (AtticWindow + AtticBeamEnd) * 0.5f;
            foreach (var twist in new[] { 0f, 90f })
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "WindowBeam";
                Object.DestroyImmediate(quad.GetComponent<Collider>());
                quad.transform.SetParent(root, false);
                quad.transform.localPosition = center;
                // Верх плоскости смотрит на окно: там текстура ярче.
                quad.transform.localRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Vector3.forward, direction).normalized, -direction.normalized) * Quaternion.Euler(0f, twist, 0f);
                quad.transform.localScale = new Vector3(AtticBeamWidth, direction.magnitude, 1f);
                var renderer = quad.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>Пылинки в луче: одна система частиц без столкновений, несколько десятков мягких точек.</summary>
        private static void AddAtticDust(Transform root)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(root, false);
            go.transform.localPosition = (AtticWindow + AtticBeamEnd) * 0.5f;
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.loop = true;
            main.prewarm = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.09f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startColor = new Color(1f, 0.93f, 0.76f, 0.55f);
            main.maxParticles = AtticDust;
            main.gravityModifier = -0.002f;

            var emission = system.emission;
            emission.rateOverTime = AtticDust / 7f;

            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.2f, 3.2f, 2.6f);

            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = ParticleMaterial("M_AtticDust", true, true);
        }

        private static void AddCat(Transform root, Material material)
        {
            var model = CreatureInstance("Cat", AtticCatModel, root, material);
            if (model == null)
                return;

            var rootBone = FindDeep(model, "Root");
            var body = FindDeep(model, "Body");
            var earLeft = FindDeep(model, "EarL");
            var earRight = FindDeep(model, "EarR");

            // Вперёд считаем из осей ушей и верха самой модели: голова у кота лежит сбоку.
            FaceCamera(model, rootBone, body, -Vector3.Cross(earRight.position - earLeft.position, body.position - rootBone.position));
            var holder = model.parent;
            holder.localPosition = AtticCatSpot;
            holder.localRotation = Quaternion.Euler(0f, AtticCatYaw, 0f);
            holder.gameObject.AddComponent<CatNap>().EditorSetup(body, FindDeep(model, "Head"), FindDeep(model, "Lids"), earLeft, earRight,
                new[] { FindDeep(model, "TailA"), FindDeep(model, "TailB"), FindDeep(model, "TailC"), FindDeep(model, "TailD") });
        }
    }
}
