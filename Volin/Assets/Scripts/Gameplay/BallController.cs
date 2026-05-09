using ArcadeVolley.Data;
using Unity.Netcode;
using UnityEngine;

namespace ArcadeVolley.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(NetworkObject))]
    public class BallController : NetworkBehaviour
    {
        [SerializeField] private MatchConfig config;
        [SerializeField] private float touchImpulse = 6f;
        [SerializeField] private float spikeExtraY = 3f;

        private Rigidbody2D _rb;

        private readonly NetworkVariable<Vector2> _netPosition = new(
            Vector2.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector2> _netVelocity = new(
            Vector2.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (IsServer)
            {
                _netPosition.Value = _rb.position;
                _netVelocity.Value = _rb.velocity;
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (!IsServer)
            {
                transform.position = Vector2.Lerp(transform.position, _netPosition.Value, Time.deltaTime * config.remotePositionLerp);
                _rb.velocity = Vector2.Lerp(_rb.velocity, _netVelocity.Value, Time.deltaTime * config.remotePositionLerp);
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;

            _rb.velocity = Vector2.ClampMagnitude(_rb.velocity, config.maxBallSpeed);
            _netPosition.Value = _rb.position;
            _netVelocity.Value = _rb.velocity;
        }

        [ServerRpc(RequireOwnership = false)]
        public void TouchBallServerRpc(Vector2 impulse, bool isSpike)
        {
            Vector2 finalImpulse = impulse;
            if (isSpike) finalImpulse += Vector2.down * spikeExtraY;

            _rb.AddForce(finalImpulse.normalized * touchImpulse, ForceMode2D.Impulse);
        }

        [ServerRpc(RequireOwnership = false)]
        public void ResetAndServeServerRpc(Vector2 spawnPos, int serveDirection)
        {
            ResetAndServeServer(spawnPos, serveDirection);
        }

        // Usado pelo servidor para evitar RPC servidor->servidor.
        public void ResetAndServeServer(Vector2 spawnPos, int serveDirection)
        {
            if (!IsServer) return;

            _rb.position = spawnPos;
            _rb.velocity = Vector2.zero;
            Vector2 serve = new(serveDirection * config.serveImpulseX, config.serveImpulseY);
            _rb.AddForce(serve, ForceMode2D.Impulse);

            _netPosition.Value = _rb.position;
            _netVelocity.Value = _rb.velocity;
        }
    }
}
