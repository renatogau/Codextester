using ArcadeVolley.Core;
using ArcadeVolley.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ArcadeVolley.Gameplay
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private MatchConfig config;
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.15f;
        [SerializeField] private LayerMask groundMask;

        [Header("Hitboxes")]
        [SerializeField] private Collider2D receiveHitbox;
        [SerializeField] private Collider2D spikeHitbox;
        [SerializeField] private Collider2D blockHitbox;

        private Rigidbody2D _rb;
        private Vector2 _moveInput;
        private bool _jumpPressed;
        private bool _receivePressed;
        private bool _spikePressed;
        private bool _blockPressed;

        private readonly NetworkVariable<int> _team = new(
            Constants.TeamA,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<Vector2> _netPosition = new(
            Vector2.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            _rb = GetComponent<Rigidbody2D>();
            EnsureConfig();

            if (IsServer)
                _netPosition.Value = transform.position;

            if (!IsOwner)
            {
                DisableLocalInputComponents();
            }
        }

        private void EnsureConfig()
        {
            if (config != null) return;
            config = ScriptableObject.CreateInstance<MatchConfig>();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                PollInput();
                SendInputServerRpc(_moveInput, _jumpPressed, _receivePressed, _spikePressed, _blockPressed);
            }
            else
            {
                // Suaviza players remotos sem perder autoridade do servidor.
                transform.position = Vector2.Lerp(
                    transform.position,
                    _netPosition.Value,
                    Time.deltaTime * config.remotePositionLerp);
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;
            _netPosition.Value = _rb.position;
        }

        [ServerRpc]
        private void SendInputServerRpc(Vector2 moveInput, bool jump, bool receive, bool spike, bool block)
        {
            float vx = moveInput.x * config.moveSpeed;
            _rb.velocity = new Vector2(vx, _rb.velocity.y);

            bool grounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
            if (jump && grounded)
            {
                _rb.AddForce(Vector2.up * config.jumpImpulse, ForceMode2D.Impulse);
            }

            // Janela simples de ações arcade (ativação curta de hitboxes).
            receiveHitbox.enabled = receive;
            spikeHitbox.enabled = spike;
            blockHitbox.enabled = block;

            _netPosition.Value = _rb.position;
        }

        public void AssignTeamServer(int team)
        {
            if (!IsServer) return;
            _team.Value = team;
        }

        private void PollInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float x = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x = -1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x = 1f;
            _moveInput = new Vector2(x, 0f);

            _jumpPressed = kb.spaceKey.wasPressedThisFrame;
            _receivePressed = kb.jKey.isPressed;
            _spikePressed = kb.kKey.isPressed;
            _blockPressed = kb.lKey.isPressed;
        }

        private void DisableLocalInputComponents()
        {
            // Mantém render/colisão, desativa apenas leitura local de input se existir PlayerInput.
            var playerInput = GetComponent<PlayerInput>();
            if (playerInput != null) playerInput.enabled = false;
        }
    }
}
