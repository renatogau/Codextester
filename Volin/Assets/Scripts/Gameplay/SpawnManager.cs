using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace ArcadeVolley.Gameplay
{
    public class SpawnManager : NetworkBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private BallController ballPrefab;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] teamASpawns;
        [SerializeField] private Transform[] teamBSpawns;
        [SerializeField] private Transform ballSpawn;

        [Header("References")]
        [SerializeField] private ScoreManager scoreManager;

        private readonly List<PlayerController> _players = new();
        private BallController _ball;

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.OnClientConnectedCallback += OnClientConnected;

            if (_ball == null)
            {
                _ball = Instantiate(ballPrefab, ballSpawn.position, Quaternion.identity);
                _ball.NetworkObject.Spawn(true);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (NetworkManager != null)
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!IsServer) return;
            SpawnPlayer(clientId);
        }

        private void SpawnPlayer(ulong clientId)
        {
            int slot = _players.Count;
            bool isTeamA = slot % 2 == 0;

            Transform[] points = isTeamA ? teamASpawns : teamBSpawns;
            Transform point = points[Mathf.Min(slot / 2, points.Length - 1)];

            NetworkObject playerObj = Instantiate(playerPrefab, point.position, Quaternion.identity);
            playerObj.SpawnAsPlayerObject(clientId, true);

            var controller = playerObj.GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.AssignTeamServer(isTeamA ? 0 : 1);
                _players.Add(controller);
            }

            if (scoreManager != null && _ball != null)
            {
                _ball.ResetAndServeServer(ballSpawn.position, 1);
            }
        }
    }
}
