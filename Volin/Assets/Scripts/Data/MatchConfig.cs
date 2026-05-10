using UnityEngine;

namespace ArcadeVolley.Data
{
    [CreateAssetMenu(menuName = "ArcadeVolley/MatchConfig", fileName = "MatchConfig")]
    public class MatchConfig : ScriptableObject
    {
        [Header("Rules")]
        public int pointsToWinSet = 7;
        public int winBy = 2;

        [Header("Player")]
        public float moveSpeed = 8f;
        public float jumpImpulse = 14f;

        [Header("Ball")]
        public float serveImpulseX = 8f;
        public float serveImpulseY = 10f;
        public float maxBallSpeed = 18f;

        [Header("Networking")]
        public float remotePositionLerp = 12f;
    }
}
