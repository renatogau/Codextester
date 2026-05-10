using ArcadeVolley.Gameplay;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.UI;
using UnityEngine;

namespace ArcadeVolley.UI
{
    public class HudUI : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text pingText;

        private void Update()
        {
            if (scoreManager != null)
            {
                scoreText.text = $"{scoreManager.TeamAScore.Value} x {scoreManager.TeamBScore.Value}";
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                if (NetworkManager.Singleton.NetworkConfig.NetworkTransport is UnityTransport transport)
                {
                    // RTT em ms (aprox.)
                    float rtt = transport.GetCurrentRtt(clientId);
                    pingText.text = $"Ping: {rtt:0} ms";
                }
            }
        }
    }
}
