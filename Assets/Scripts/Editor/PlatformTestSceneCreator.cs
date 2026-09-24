using System.Collections.Generic;
using System.IO;
using System.Linq;
using Base.Services.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Zenject;

namespace Base.Editor
{
    /// <summary>
    /// Создаёт Assets/Scenes/PlatformTest.unity: камера, Canvas, EventSystem с Input System,
    /// Zenject SceneContext и PlatformTestPanel. Ставит сцену первой в Build Settings.
    /// Меню Base/Setup/Create Platform Test Scene.
    /// </summary>
    public static class PlatformTestSceneCreator
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = ScenesFolder + "/PlatformTest.unity";

        [MenuItem("Base/Setup/Create Platform Test Scene", priority = 1)]
        public static void Create()
        {
            if (File.Exists(ScenePath))
            {
                if (!EditorUtility.DisplayDialog("Base", ScenePath + " уже существует. Пересоздать?", "Пересоздать", "Отмена"))
                    return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                Directory.CreateDirectory(ScenesFolder);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera();
            CreateEventSystem();
            var canvas = CreateCanvas();
            new GameObject("SceneContext").AddComponent<SceneContext>();

            var panel = new GameObject("PlatformTestPanel", typeof(RectTransform));
            panel.transform.SetParent(canvas.transform, false);
            panel.AddComponent<PlatformTestPanel>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();

            Debug.Log("[Base] Created " + ScenePath + " and set it as the first scene in Build Settings");
        }

        private static void CreateCamera()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var camera = go.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            camera.orthographic = true;
            go.AddComponent<AudioListener>();
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void AddToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            scenes.AddRange(EditorBuildSettings.scenes.Where(s => s.path != ScenePath));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
