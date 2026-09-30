using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zonk.Configs;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Ощущение партии: ассет MatchFeelConfig и два эффекта частиц — искры (горячие кости, крупные комбинации)
    /// и пыль от ударов костей о стол. Частицы дешёвые для телефонов и браузера: без столкновений и света,
    /// одна мягкая круглая текстура, выпуск по событию (Emit), а не постоянно. Уже созданное не перезаписывается.
    /// </summary>
    public static partial class ZonkSetup
    {
        private static void BuildFeel(GameConfig config)
        {
            var feel = Asset<MatchFeelConfig>(ConfigsFolder + "/Game/MatchFeel.asset", f => { });
            var changed = false;
            if (feel.SparksPrefab == null)
            {
                feel.SparksPrefab = SparksPrefab().GetComponent<ParticleSystem>();
                changed = true;
            }

            if (feel.DustPrefab == null)
            {
                feel.DustPrefab = DustPrefab().GetComponent<ParticleSystem>();
                changed = true;
            }

            if (changed)
                EditorUtility.SetDirty(feel);

            if (config.Feel == null)
            {
                config.Feel = feel;
                EditorUtility.SetDirty(config);
            }
        }

        /// <summary>Искры: яркие золотые точки, разлетаются вверх и в стороны, падают под силой тяжести и гаснут.</summary>
        private static GameObject SparksPrefab()
        {
            return Prefab(Prefabs + "/Effects/Sparks.prefab", () =>
            {
                var root = new GameObject("Sparks");
                var system = root.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = system.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 1f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 3.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.55f), new Color(1f, 0.6f, 0.15f));
                main.gravityModifier = 0.9f;
                main.maxParticles = 200;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = system.emission;
                emission.enabled = false;

                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.08f;
                shape.rotation = new Vector3(-90f, 0f, 0f);

                var fade = system.colorOverLifetime;
                fade.enabled = true;
                fade.color = FadeOut(new Color(1f, 1f, 1f), 0.6f);

                var size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

                var renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.04f;
                renderer.lengthScale = 1.5f;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = ParticleMaterial("M_Sparks", true);
                return root;
            });
        }

        /// <summary>Пыль: мягкие серо-коричневые клубочки у стола, расползаются, растут и тают.</summary>
        private static GameObject DustPrefab()
        {
            return Prefab(Prefabs + "/Effects/Dust.prefab", () =>
            {
                var root = new GameObject("Dust");
                var system = root.AddComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                var main = system.main;
                main.playOnAwake = false;
                main.loop = false;
                main.duration = 1f;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
                main.startColor = new Color(0.78f, 0.7f, 0.6f, 0.45f);
                main.gravityModifier = -0.02f;
                main.maxParticles = 150;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var emission = system.emission;
                emission.enabled = false;

                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.08f;
                shape.rotation = new Vector3(-90f, 0f, 0f);

                var fade = system.colorOverLifetime;
                fade.enabled = true;
                fade.color = FadeOut(Color.white, 0.2f);

                var size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.6f));

                var renderer = root.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = ParticleMaterial("M_Dust", false);
                return root;
            });
        }

        /// <summary>Прозрачность: полная до hold (доля жизни), затем плавно в ноль.</summary>
        private static ParticleSystem.MinMaxGradient FadeOut(Color color, float hold)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, hold), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>Материал частиц URP Unlit с мягкой круглой текстурой: additive — светится (искры), иначе — по альфе.</summary>
        private static Material ParticleMaterial(string name, bool additive)
        {
            var path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", additive ? 2f : 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive)
                material.EnableKeyword("_BLENDMODE_ADD");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", SoftDotTexture());
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Мягкая белая точка 64×64 с прозрачным краем: общая текстура частиц.</summary>
        private static Texture2D SoftDotTexture()
        {
            var path = Root + "/Art/Textures/Particle_SoftDot.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = (x + 0.5f) / size * 2f - 1f;
                    var dy = (y + 0.5f) / size * 2f - 1f;
                    var d = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * alpha));
                }
            }

            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
