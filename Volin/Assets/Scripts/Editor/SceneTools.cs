#if UNITY_EDITOR
using ArcadeVolley.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArcadeVolley.EditorTools
{
    public static class SceneTools
    {
        [MenuItem("ArcadeVolley/Create QuickPlay Scene")]
        public static void CreateQuickPlayScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var bootstrap = new GameObject("QuickPlayBootstrap");
            bootstrap.AddComponent<QuickPlaySceneBuilder>();

            const string scenePath = "Assets/Scenes/QuickPlay.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"QuickPlay scene criada em: {scenePath}");
        }
    }
}
#endif
