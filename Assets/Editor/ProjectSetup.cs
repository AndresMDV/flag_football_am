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
