using ArcadeVolley.Data;
using ArcadeVolley.Gameplay;
using ArcadeVolley.Networking;
using ArcadeVolley.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

namespace ArcadeVolley.Core
{
    // Builder de cena mínima para provar gameplay online sem assets prontos.
    public class QuickPlaySceneBuilder : MonoBehaviour
    {
        [SerializeField] private MatchConfig matchConfig;

        private void Awake()
        {
            BuildIfNeeded();
        }

        private void BuildIfNeeded()
        {
            if (matchConfig == null)
                matchConfig = ScriptableObject.CreateInstance<MatchConfig>();

            if (FindFirstObjectByType<NetworkManager>() == null)
            {
                var nmGo = new GameObject("NetworkManager");
                nmGo.AddComponent<UnityTransport>();
                nmGo.AddComponent<NetworkManager>();
                nmGo.AddComponent<NetworkBootstrap>();
                nmGo.AddComponent<QuickStartHost>();
            }

            if (FindFirstObjectByType<ScoreManager>() == null)
            {
                var scoreGo = new GameObject("ScoreManager");
                var score = scoreGo.AddComponent<ScoreManager>();
                scoreGo.AddComponent<NetworkObject>();
                SetPrivateField(score, "config", matchConfig);
            }

            if (FindFirstObjectByType<SpawnManager>() == null)
            {
                var spawner = new GameObject("SpawnManager").AddComponent<SpawnManager>();
                spawner.gameObject.AddComponent<NetworkObject>();
            }

            if (FindFirstObjectByType<HudUI>() == null)
            {
                var canvas = new GameObject("HUD");
                canvas.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.AddComponent<CanvasScaler>();
                canvas.AddComponent<GraphicRaycaster>();

                var scoreText = CreateUiText("ScoreText", canvas.transform, new Vector2(0, -40));
                var pingText = CreateUiText("PingText", canvas.transform, new Vector2(0, -80));

                var hud = canvas.AddComponent<HudUI>();
                SetPrivateField(hud, "scoreText", scoreText);
                SetPrivateField(hud, "pingText", pingText);
                SetPrivateField(hud, "scoreManager", FindFirstObjectByType<ScoreManager>());
            }
        }

        private static Text CreateUiText(string name, Transform parent, Vector2 anchoredPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 32;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "...";

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(600, 60);
            return text;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null) field.SetValue(target, value);
        }
    }
}
