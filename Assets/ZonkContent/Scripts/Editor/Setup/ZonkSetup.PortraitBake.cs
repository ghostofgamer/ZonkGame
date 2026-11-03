using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zonk.Configs;
using Zonk.Presentation;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Портреты соперников снимаются с настоящей фигуры: дым, голова, грудь, тем же взглядом сверху, что в партии.
    /// Файл заменяется на месте (Portrait_id.png), поэтому ссылки в конфигах остаются. Ручной портрет из другой папки не трогаем.
    /// Съёмка идёт в отдельной сцене редактора, открытая сцена не меняется.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const int PortraitRender = 384;
        private const int PortraitMaxSize = 192;
        private const int PortraitFrame = 6;
        private const int BossPortraitFrame = 12;
        private const float PortraitFov = 13.6f;
        private const float PortraitDistance = 8.4f;

        private static readonly Vector3 PortraitLookAt = new Vector3(0f, 0.72f, 0f);

        // Направление на камеру партии от головы соперника: под него наклонён дым.
        private static readonly Vector3 PortraitViewDirection = new Vector3(0f, 3.2f, -7.8f).normalized;
        private static readonly Color PortraitBackground = new Color(0.72f, 0.55f, 0.36f);
        private static readonly Color BossPortraitBackground = new Color(0.62f, 0.16f, 0.12f);
        private static readonly Color PortraitFrameColor = new Color(0.1f, 0.08f, 0.06f);
        private static readonly Color BossPortraitFrameColor = new Color(1f, 0.8f, 0.3f);

        [MenuItem("Zonk/Art/Bake Opponent Portraits", priority = 48)]
        public static void BakeOpponentPortraitsMenu()
        {
            ApplyOpponentKitMenu();
            EnsureFolder(PortraitsFolder);

            var scene = EditorSceneManager.NewPreviewScene();
            var target = RenderTexture.GetTemporary(PortraitRender, PortraitRender, 24, RenderTextureFormat.ARGB32);
            var baked = 0;
            try
            {
                var camera = IconCamera(scene, target);
                camera.fieldOfView = PortraitFov;
                camera.transform.position = PortraitLookAt + PortraitViewDirection * PortraitDistance;
                camera.transform.rotation = Quaternion.LookRotation(-PortraitViewDirection, Vector3.up);
                IconLight(scene);

                var root = new GameObject("PortraitFigure");
                var smoke = BuildShadowSmoke(root.transform);
                BuildOpponentAnchors(root.transform, out _, out var accessoryAnchor, out var chestAnchor);
                SceneManager.MoveGameObjectToScene(root, scene);

                var block = new MaterialPropertyBlock();
                foreach (var opponent in FindAll<OpponentConfig>(ConfigsFolder))
                {
                    if (string.IsNullOrEmpty(opponent.Id) || opponent.DreadOf != null || !IsBakedPortrait(opponent))
                        continue;

                    camera.backgroundColor = opponent.IsBoss ? BossPortraitBackground : PortraitBackground;
                    smoke.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", opponent.BodyColor);
                    block.SetColor("_EyeColor", opponent.IsBoss ? OpponentAvatarView.DefaultBossEyes : opponent.EyeColor);
                    smoke.SetPropertyBlock(block);

                    var head = PortraitPart(opponent.Accessory, accessoryAnchor.transform);
                    var chest = PortraitPart(opponent.ChestPiece, chestAnchor.transform);
                    try
                    {
                        camera.Render();
                        WritePortrait(target, PortraitsFolder + "/Portrait_" + opponent.Id + ".png", opponent.IsBoss);
                        baked++;
                    }
                    finally
                    {
                        if (head != null)
                            Object.DestroyImmediate(head);
                        if (chest != null)
                            Object.DestroyImmediate(chest);
                    }
                }
            }
            finally
            {
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }

            AssetDatabase.Refresh();
            foreach (var opponent in FindAll<OpponentConfig>(ConfigsFolder))
            {
                if (string.IsNullOrEmpty(opponent.Id) || opponent.DreadOf != null || !IsBakedPortrait(opponent))
                    continue;

                AssignPortrait(opponent, PortraitsFolder + "/Portrait_" + opponent.Id + ".png");
            }

            LinkDreadPortraits();
            BuildSpriteAtlases();
            AssetDatabase.SaveAssets();
            Debug.Log("[Zonk] Opponent portraits baked: " + baked);
        }

        private static bool IsBakedPortrait(OpponentConfig opponent)
        {
            return opponent.Portrait == null || AssetDatabase.GetAssetPath(opponent.Portrait).StartsWith(PortraitsFolder);
        }

        private static GameObject PortraitPart(GameObject prefab, Transform anchor)
        {
            return prefab != null ? Object.Instantiate(prefab, anchor, false) : null;
        }

        private static void WritePortrait(RenderTexture source, string path, bool boss)
        {
            RenderTexture.active = source;
            var image = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);

            // Рамка как у прежних портретов: у босса золотая и толще. Картинка непрозрачная.
            var frame = boss ? BossPortraitFrame : PortraitFrame;
            var frameColor = boss ? BossPortraitFrameColor : PortraitFrameColor;
            var pixels = image.GetPixels();
            for (var y = 0; y < source.height; y++)
            for (var x = 0; x < source.width; x++)
            {
                var edge = Mathf.Min(Mathf.Min(x, source.width - 1 - x), Mathf.Min(y, source.height - 1 - y));
                var color = edge < frame ? frameColor : pixels[y * source.width + x];
                color.a = 1f;
                pixels[y * source.width + x] = color;
            }

            image.SetPixels(pixels);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
        }

        private static void AssignPortrait(OpponentConfig opponent, string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = PortraitMaxSize;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.crunchedCompression = true;
                importer.compressionQuality = 75;
                importer.SaveAndReimport();
            }

            opponent.Portrait = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            EditorUtility.SetDirty(opponent);
        }
    }
}
