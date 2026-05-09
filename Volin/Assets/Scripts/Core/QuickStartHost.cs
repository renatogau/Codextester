using Unity.Netcode;
using UnityEngine;

namespace ArcadeVolley.Core
{
    // Para testes locais rápidos: sobe Host automaticamente ao dar Play.
    public class QuickStartHost : MonoBehaviour
    {
        [SerializeField] private bool autoStartHost = true;

        private void Start()
        {
            if (!autoStartHost) return;
            if (NetworkManager.Singleton == null) return;
            if (NetworkManager.Singleton.IsListening) return;

            NetworkManager.Singleton.StartHost();
        }
    }
}
