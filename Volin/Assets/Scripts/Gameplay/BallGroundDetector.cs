using Unity.Netcode;
using UnityEngine;

namespace ArcadeVolley.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class BallGroundDetector : NetworkBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!IsServer || scoreManager == null) return;

            if (!collision.collider.CompareTag("Ground")) return;

            int side = collision.collider.TryGetComponent<CourtSideTrigger>(out var sideMarker)
                ? sideMarker.Side
                : (collision.transform.position.x < 0f ? -1 : 1);

            scoreManager.OnBallHitGroundServer(side);
        }
    }
}
