# Arcade Beach Volley (Unity + NGO) — MVP

## 1) Arquitetura de pastas (ordem solicitada)

```text
Assets/
  Art/
    Sprites/
    Materials/
  Audio/
  Prefabs/
    Network/
      NetworkPlayer.prefab
      NetworkBall.prefab
    Environment/
      Court.prefab
      Net.prefab
  Scenes/
    Boot.unity
    MainMenu.unity
    Game.unity
  Scripts/
    Core/
      Bootstrap.cs
      Constants.cs
    Networking/
      NetworkBootstrap.cs
      LobbyRelayService.cs
      NetworkTimePing.cs
    Gameplay/
      PlayerController.cs
      BallController.cs
      ScoreManager.cs
      SpawnManager.cs
      CourtZones.cs
    UI/
      MainMenuUI.cs
      HudUI.cs
    Data/
      MatchConfig.cs
  Settings/
    Input/
      PlayerInputActions.inputactions
```

### Decisão de física: **2D (Rigidbody2D)**
Para um arcade inspirado em Beach Volley Folly, 2D entrega:
- iteração mais rápida no MVP;
- colisões mais previsíveis e “limpas” (menos variáveis que 3D);
- menor custo de sincronização de estado em rede.

Se quiser profundidade visual depois, usar arte 2.5D com gameplay 2D.

---

## 2) Scripts base do MVP

> Abaixo estão scripts iniciais completos e focados em MVP. Eles já usam NGO (`NetworkObject`, `NetworkVariable`, `ServerRpc`, `ClientRpc`) com autoridade no servidor para bola e pontuação.

### `Assets/Scripts/Core/Constants.cs`
```csharp
namespace ArcadeVolley.Core
{
    public static class Constants
    {
        public const int TeamA = 0;
        public const int TeamB = 1;

        public const int LeftSide = -1;
        public const int RightSide = 1;
    }
}
```

### `Assets/Scripts/Data/MatchConfig.cs`
```csharp
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
```

### `Assets/Scripts/Gameplay/PlayerController.cs`
```csharp
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

            if (IsServer)
                _netPosition.Value = transform.position;

            if (!IsOwner)
            {
                DisableLocalInputComponents();
            }
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
            _rb.linearVelocity = new Vector2(vx, _rb.linearVelocity.y);

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
```

### `Assets/Scripts/Gameplay/BallController.cs`
```csharp
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
                _netVelocity.Value = _rb.linearVelocity;
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (!IsServer)
            {
                transform.position = Vector2.Lerp(transform.position, _netPosition.Value, Time.deltaTime * config.remotePositionLerp);
                _rb.linearVelocity = Vector2.Lerp(_rb.linearVelocity, _netVelocity.Value, Time.deltaTime * config.remotePositionLerp);
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;

            _rb.linearVelocity = Vector2.ClampMagnitude(_rb.linearVelocity, config.maxBallSpeed);
            _netPosition.Value = _rb.position;
            _netVelocity.Value = _rb.linearVelocity;
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
            _rb.position = spawnPos;
            _rb.linearVelocity = Vector2.zero;

            Vector2 serve = new(serveDirection * config.serveImpulseX, config.serveImpulseY);
            _rb.AddForce(serve, ForceMode2D.Impulse);

            _netPosition.Value = _rb.position;
            _netVelocity.Value = _rb.linearVelocity;
        }
    }
}
```

### `Assets/Scripts/Gameplay/ScoreManager.cs`
```csharp
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
            ball.ResetAndServeServerRpc(ballSpawn, serveDirection);
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
```

### `Assets/Scripts/Networking/NetworkBootstrap.cs`
```csharp
using Unity.Netcode;
using UnityEngine;

namespace ArcadeVolley.Networking
{
    public class NetworkBootstrap : MonoBehaviour
    {
        public void StartHost()
        {
            NetworkManager.Singleton.StartHost();
        }

        public void StartClient()
        {
            NetworkManager.Singleton.StartClient();
        }

        public void StartServer()
        {
            NetworkManager.Singleton.StartServer();
        }

        public void Shutdown()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }
    }
}
```

### `Assets/Scripts/Networking/LobbyRelayService.cs`
```csharp
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace ArcadeVolley.Networking
{
    public class LobbyRelayService : MonoBehaviour
    {
        private const string JoinCodeKey = "joinCode";
        private Lobby _currentLobby;

        public async Task InitializeAsync()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async Task<string> CreateLobbyAndStartHostAsync(string lobbyName, int maxPlayers = 4)
        {
            await InitializeAsync();

            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetHostRelayData(
                alloc.RelayServer.IpV4,
                (ushort)alloc.RelayServer.Port,
                alloc.AllocationIdBytes,
                alloc.Key,
                alloc.ConnectionData);

            var lobbyOptions = new CreateLobbyOptions
            {
                Data = new System.Collections.Generic.Dictionary<string, DataObject>
                {
                    { JoinCodeKey, new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                }
            };

            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, lobbyOptions);
            NetworkManager.Singleton.StartHost();
            return _currentLobby.LobbyCode;
        }

        public async Task JoinLobbyAndStartClientAsync(string lobbyCode)
        {
            await InitializeAsync();

            _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            string joinCode = _currentLobby.Data[JoinCodeKey].Value;

            JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetClientRelayData(
                joinAlloc.RelayServer.IpV4,
                (ushort)joinAlloc.RelayServer.Port,
                joinAlloc.AllocationIdBytes,
                joinAlloc.Key,
                joinAlloc.ConnectionData,
                joinAlloc.HostConnectionData);

            NetworkManager.Singleton.StartClient();
        }

        public async Task LeaveLobbyAsync()
        {
            if (_currentLobby == null) return;

            try
            {
                await LobbyService.Instance.RemovePlayerAsync(_currentLobby.Id, AuthenticationService.Instance.PlayerId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"LeaveLobby falhou: {e.Message}");
            }

            _currentLobby = null;
        }
    }
}
```

### `Assets/Scripts/UI/HudUI.cs`
```csharp
using ArcadeVolley.Gameplay;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace ArcadeVolley.UI
{
    public class HudUI : MonoBehaviour
    {
        [SerializeField] private ScoreManager scoreManager;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI pingText;

        private void Update()
        {
            if (scoreManager != null)
            {
                scoreText.text = $"{scoreManager.TeamAScore.Value} x {scoreManager.TeamBScore.Value}";
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
            {
                // RTT em ms (aprox.)
                ulong clientId = NetworkManager.Singleton.LocalClientId;
                float rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId);
                pingText.text = $"Ping: {rtt:0} ms";
            }
        }
    }
}
```

---

## 3) Setup no Unity (Editor)

1. **Criar projeto Unity 2022 LTS+** (2D Core).
2. Instalar pacotes:
   - Netcode for GameObjects
   - Unity Transport
   - Input System
   - (Opcional) Lobby, Relay, Authentication, Core
   - TextMeshPro
3. Em **Project Settings > Player > Active Input Handling**: `Input System Package (New)`.
4. Criar cenas `MainMenu` e `Game`.
5. Na cena `Game`:
   - Quadra (sprites/colliders), rede com collider central;
   - chão com `Layer: Ground`;
   - zonas esquerda/direita para detectar lado da queda da bola.
6. Criar prefab `NetworkPlayer`:
   - `NetworkObject`
   - `Rigidbody2D` (gravity ~3, freeze rotation Z)
   - collider
   - `PlayerController` + refs de hitboxes/ground check.
7. Criar prefab `NetworkBall`:
   - `NetworkObject`
   - `Rigidbody2D` (gravity ~1.6, collision detection: Continuous)
   - `CircleCollider2D`
   - `BallController`.
8. Criar `NetworkManager` object:
   - componente `NetworkManager`
   - componente `UnityTransport`
   - registrar `NetworkPlayer` como `Player Prefab`.
9. Adicionar `NetworkBootstrap` na cena/menu.
10. UI menu:
    - botões “Criar Sala” / “Entrar Sala”
    - input para código da sala
    - chamar `LobbyRelayService`.
11. UI HUD:
    - texto de placar e ping
    - vincular `HudUI` ao `ScoreManager`.

---

## Checklist de testes locais (MVP)

1. Rodar **duas instâncias** (Editor + Build).
2. Host cria sala; client entra por código.
3. Validar movimento/pulo dos 4 jogadores.
4. Validar toque/cortada/bloqueio simplificados.
5. Verificar **sincronização da bola** (sem teleporte severo).
6. Pontuar ao cair no chão correto.
7. Confirmar reset de rally + novo saque.
8. Confirmar fim de set (7, diferença 2).
9. Conferir HUD (placar + ping).

---

## Roadmap pós-MVP

1. Matchmaking automático (Lobby query + filas).
2. MMR/ranked + temporadas.
3. Cosméticos (skins, emotes, trilhas).
4. Anti-cheat básico:
   - validação server-side de ações;
   - limites de velocidade/impulso;
   - detecção de taxa anômala de RPC.
5. Reconexão robusta:
   - estado serializável de partida;
   - rejoin no lobby com restauração de slot/time.

---

## 4) Modo "Blobs" (jogável antes dos assets finais)

Se o foco é só provar gameplay/rede, monte tudo com placeholders:

1. **Players**: `SpriteRenderer` com círculo sólido (blob), `Rigidbody2D`, `CapsuleCollider2D`, `NetworkObject`, `PlayerController`.
2. **Bola**: círculo branco com `CircleCollider2D`, `Rigidbody2D`, `NetworkObject`, `BallController`, `BallGroundDetector`.
3. **Chão**: 2 objetos (`GroundLeft`, `GroundRight`) com `BoxCollider2D`, `Tag = Ground`, script `CourtSideTrigger` (`-1` esquerda, `+1` direita).
4. **Rede**: `BoxCollider2D` no meio da quadra.
5. **Spawns**: adicionar `SpawnManager` em objeto vazio com:
   - `playerPrefab` apontando para `NetworkPlayer`
   - `ballPrefab` apontando para `NetworkBall`
   - dois pontos para Time A e dois para Time B
   - ponto de spawn da bola
   - referência para `ScoreManager`
6. **Score**: `ScoreManager` referenciando `BallController` da bola de rede.

Scripts adicionados para essa prova rápida:
- `Assets/Scripts/Gameplay/SpawnManager.cs`
- `Assets/Scripts/Gameplay/BallGroundDetector.cs`
- `Assets/Scripts/Gameplay/CourtSideTrigger.cs`

Com isso, já dá para jogar online sem arte final.

---

## 5) Não há scenes prontas no repositório (intencional)

Para acelerar prova de funcionamento, use uma cena vazia chamada `QuickPlay.unity` e adicione:

- `QuickPlaySceneBuilder` (cria `NetworkManager`, `ScoreManager`, `SpawnManager` e HUD mínimos em runtime)
- seus prefabs básicos de `NetworkPlayer` e `NetworkBall`
- chão/rede com colliders conforme seção de Blobs

Objetivo: validar que a partida online sobe, jogadores entram e o loop de ponto funciona.
