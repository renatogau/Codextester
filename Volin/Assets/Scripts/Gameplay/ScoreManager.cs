using ArcadeVolley.Data;
using Unity.Netcode;
using UnityEngine;

namespace ArcadeVolley.Gameplay
{
    public class ScoreManager : NetworkBehaviour
    {
        [SerializeField] private MatchConfig config;
        [SerializeField] private BallController ball;
        [SerializeField] private Vector2 ballSpawn = new(0f, 2f);

        public readonly NetworkVariable<int> TeamAScore = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> TeamBScore = new(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<bool> MatchEnded = new(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public void OnBallHitGroundServer(int courtSide)
        {
            if (!IsServer || MatchEnded.Value) return;

            // Se caiu no lado esquerdo, ponto para Time B; no direito, ponto para Time A.
            if (courtSide < 0) TeamBScore.Value++;
            else TeamAScore.Value++;

            if (HasWinner())
            {
                MatchEnded.Value = true;
                EndMatchClientRpc(TeamAScore.Value, TeamBScore.Value);
                return;
            }

            int serveDirection = courtSide < 0 ? -1 : 1;
            ball.ResetAndServeServer(ballSpawn, serveDirection);
            OnScoreUpdatedClientRpc(TeamAScore.Value, TeamBScore.Value);
        }

        private bool HasWinner()
        {
            int a = TeamAScore.Value;
            int b = TeamBScore.Value;

            if (a >= config.pointsToWinSet || b >= config.pointsToWinSet)
            {
                return Mathf.Abs(a - b) >= config.winBy;
            }

            return false;
        }

        [ClientRpc]
        private void OnScoreUpdatedClientRpc(int scoreA, int scoreB) { }

        [ClientRpc]
        private void EndMatchClientRpc(int scoreA, int scoreB) { }
    }
}
