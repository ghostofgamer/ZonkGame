using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Zonk.Editor.Setup
{
    /// <summary>
    /// Снимки интерфейса для проверки вида без запуска игры: каждое окно (Prefabs/UI) и каждая деталь (Prefabs/UI/Parts)
    /// снимаются камерой в отдельной сцене редактора в Test/ui/. Тексты, которые окно ставит в игре, пустые — смотреть
    /// спрайты, значки, рамки и вёрстку. Открытая сцена не меняется, префабы не сохраняются.
    /// </summary>
    public static partial class ZonkSetup
    {
        private const string GalleryFolder = "Test/ui";

        [MenuItem("Zonk/Debug/Render UI Gallery", priority = 60)]
        public static void RenderUiGallery()
        {
            Directory.CreateDirectory(GalleryFolder);
            var windows = RenderPrefabs(Prefabs + "/UI", new Vector2Int(1920, 1080), true);
            var parts = RenderPrefabs(Prefabs + "/UI/Parts", new Vector2Int(960, 540), false);
            Debug.Log("[Zonk] UI gallery: " + windows + " windows, " + parts + " parts → " + Path.GetFullPath(GalleryFolder));
        }

        private static int RenderPrefabs(string folder, Vector2Int size, bool stretch)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            var count = 0;
            try
            {
                var cameraObject = new GameObject("GalleryCamera");
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>();
                camera.scene = scene;
                camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.3f, 0.36f, 0.42f, 1f);
                camera.orthographic = true;

                var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = stretch ? new Vector2(1920, 1080) : new Vector2(960, 540);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                foreach (var file in Directory.GetFiles(folder, "*.prefab", SearchOption.TopDirectoryOnly))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                    if (prefab == null || prefab.GetComponent<RectTransform>() == null)
                        continue;

                    var instance = Object.Instantiate(prefab, canvas.transform, false);
                    instance.SetActive(true);
                    var rect = (RectTransform)instance.transform;
                    if (stretch)
                    {
                        rect.anchorMin = Vector2.zero;
                        rect.anchorMax = Vector2.one;
                        rect.offsetMin = Vector2.zero;
                        rect.offsetMax = Vector2.zero;
                    }
                    else
                    {
                        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                        rect.anchoredPosition = Vector2.zero;
                        if (rect.sizeDelta.x < 1f || rect.sizeDelta.y < 1f)
                            rect.sizeDelta = new Vector2(400f, 120f);
                    }

                    foreach (var group in instance.GetComponentsInChildren<CanvasGroup>(true))
                        group.alpha = 1f;

                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    WriteRender(target, GalleryFolder + "/" + (stretch ? "" : "part_") + prefab.name + ".png");
                    Object.DestroyImmediate(instance);
                    count++;
                }
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }

            return count;
        }
    }
}
