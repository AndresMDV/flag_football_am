using System;
using System.IO;
using FlagFootball.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FlagFootball.Editor
{
    public static class ProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/Bootstrap.unity";
        private const string SettingsPath = "Assets/Settings/";

        public static void CreateBaseline()
        {
            var paths = new[] { ScenePath, SettingsPath + "PrototypeRenderer.asset",
                SettingsPath + "PrototypePipeline.asset", SettingsPath + "Ground.mat",
                SettingsPath + "Placeholder.mat" };
            foreach (var path in paths)
                if (File.Exists(path))
                    throw new InvalidOperationException("Refusing to overwrite an existing baseline asset: " + path);

            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.Refresh();
            EditorSettings.serializationMode = SerializationMode.ForceText;

            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, SettingsPath + "PrototypeRenderer.asset");
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, SettingsPath + "PrototypePipeline.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bootstrap").AddComponent<SceneBootstrap>();
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 4f, -7f);
            camera.transform.LookAt(new Vector3(0f, 0.5f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.12f, 0.18f);
            camera.farClipPlane = 100f;

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 2f;
            CreatePrimitive("Ground", PrimitiveType.Plane, Vector3.zero,
                CreateMaterial("Ground", new Color(0.15f, 0.2f, 0.17f)));
            CreatePrimitive("Placeholder", PrimitiveType.Cube, new Vector3(0f, 0.5f, 0f),
                CreateMaterial("Placeholder", new Color(0.9f, 0.45f, 0.1f)));

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save the baseline scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("BASELINE_READY: URP pipeline and Bootstrap scene created through Editor APIs.");
        }

        public static void ConfigurePlayerMovement()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var placeholder = GameObject.Find("Placeholder");
            if (!placeholder) throw new InvalidOperationException("The baseline placeholder is missing.");
            placeholder.transform.localScale = new Vector3(0.6f, 1f, 1f);
            var player = placeholder.GetComponent<PlayerMovement>();
            if (!player) player = placeholder.AddComponent<PlayerMovement>();
            var serialized = new SerializedObject(player);
            serialized.FindProperty("movementView").objectReferenceValue = Camera.main;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (!placeholder.transform.Find("FacingMarker"))
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                marker.name = "FacingMarker";
                marker.transform.SetParent(placeholder.transform, false);
                marker.transform.localPosition = new Vector3(0f, 0.25f, 0.6f);
                marker.transform.localScale = Vector3.one * 0.25f;
                marker.GetComponent<Renderer>().sharedMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(SettingsPath + "Ground.mat");
                UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
            }
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save the movement scene.");
            Debug.Log("MOVEMENT_SCENE_READY");
        }

        public static void ConfigureRunningCamera()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var player = GameObject.Find("Placeholder").GetComponent<PlayerMovement>();
            var ground = GameObject.Find("Ground").GetComponent<Renderer>();
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            var movement = new SerializedObject(player);
            movement.FindProperty("fieldMinimum").vector2Value = new Vector2(-10f, -10f);
            movement.FindProperty("fieldMaximum").vector2Value = new Vector2(10f, 10f);
            movement.FindProperty("footprintRadius").floatValue = 2f;
            movement.ApplyModifiedPropertiesWithoutUndo();
            var view = Camera.main;
            var follower = view.GetComponent<RunningCamera>();
            if (!follower) follower = view.gameObject.AddComponent<RunningCamera>();
            var cameraSettings = new SerializedObject(follower);
            cameraSettings.FindProperty("target").objectReferenceValue = player;
            cameraSettings.FindProperty("field").objectReferenceValue = ground;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();
            view.orthographic = true;
            view.orthographicSize = 2.2f;
            view.transform.rotation = Quaternion.Euler(60f, 0f, 0f);
            view.transform.position = player.transform.position + new Vector3(0f, 7f, -7f / Mathf.Tan(60f * Mathf.Deg2Rad));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save the running camera scene.");
            Debug.Log("RUNNING_CAMERA_SCENE_READY");
        }

        public static void ConfigureAttemptQuarterback()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var bootstrap = GameObject.Find("Bootstrap").GetComponent<SceneBootstrap>();
            var settings = new SerializedObject(bootstrap);
            settings.FindProperty("quarterback").objectReferenceValue =
                GameObject.Find("Placeholder").GetComponent<PlayerMovement>();
            settings.ApplyModifiedPropertiesWithoutUndo();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new InvalidOperationException("Failed to save the quarterback binding.");
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader)
                throw new InvalidOperationException("The URP Lit shader is unavailable.");
            var material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, SettingsPath + name + ".mat");
            return material;
        }

        private static void CreatePrimitive(string name, PrimitiveType type, Vector3 position, Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.position = position;
            instance.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
