using UnityEngine;

namespace ArcadeVolley.Gameplay
{
    // Trigger invisível para marcar lado da quadra (-1 esquerda / +1 direita).
    public class CourtSideTrigger : MonoBehaviour
    {
        [SerializeField] private int side = -1;

        public int Side => side;
    }
}
