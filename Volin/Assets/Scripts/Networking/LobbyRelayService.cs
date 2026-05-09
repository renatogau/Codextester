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
