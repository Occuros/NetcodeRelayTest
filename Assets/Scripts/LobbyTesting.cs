using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;
using Random = UnityEngine.Random;

public class LobbyTesting : MonoBehaviour
{
    // Inspector properties with initial values

    /// <summary>
    /// Used to set the lobby name in this example.
    /// </summary>
    public string newLobbyName = "LobbyHelloWorld" + Guid.NewGuid();

    /// <summary>
    /// Used to set the max number of players in this example.
    /// </summary>
    public int maxPlayers = 8;

    /// <summary>
    /// Used to determine if the lobby shall be private in this example.
    /// </summary>
    public bool isPrivate = false;

    // We'll only be in one lobby at once for this demo, so let's track it here
    private Lobby currentLobby;

    async void Start()
    {
        try
        {
            await ExecuteLobbyDemoAsync();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }


        // Debug.Log("Demo complete!");
    }

    // Clean up the lobby we're in if we're the host
    async Task CleanupDemoLobbyAsync()
    {
        if (currentLobby == null) return;
        var localPlayerId = AuthenticationService.Instance.PlayerId;
        Debug.Log(
            $"Trying to delete current lobby {currentLobby.Name} ({currentLobby.Id} Host: {currentLobby.HostId})");

        try
        {
            // This is so that orphan lobbies aren't left around in case the demo fails partway through
            if (currentLobby != null && currentLobby.HostId.Equals(localPlayerId))
            {
                await Lobbies.Instance.DeleteLobbyAsync(currentLobby.Id);
                Debug.Log($"Deleted lobby {currentLobby.Name} ({currentLobby.Id})");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not delete lobby {currentLobby.Name} due to {e.Message}");
            currentLobby = null;
        }
    }

    // A basic demo of lobby functionality
    async Task ExecuteLobbyDemoAsync()
    {
        await UnityServices.InitializeAsync();

        // Log in a player for this game client
        Player loggedInPlayer = await GetPlayerFromAnonymousLoginAsync();

        // Add some data to our player
        // This data will be included in a lobby under players -> player.data
        loggedInPlayer.Data.Add("Ready", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, "No"));

        // Query for existing lobbies

        // Use filters to only return lobbies which match specific conditions
        // You can only filter on built-in properties (Ex: AvailableSlots) or indexed custom data (S1, N1, etc.)
        // Take a look at the API for other built-in fields you can filter on
        List<QueryFilter> queryFilters = new List<QueryFilter>
        {
            // Let's search for games with open slots (AvailableSlots greater than 0)
            new QueryFilter(
                field: QueryFilter.FieldOptions.AvailableSlots,
                op: QueryFilter.OpOptions.GT,
                value: "0"),
        };


        // Call the Query API
        QueryResponse response = await Lobbies.Instance.QueryLobbiesAsync(new QueryLobbiesOptions()
        {
            Count = 20, // Override default number of results to return
            Filters = queryFilters,
        });

        List<Lobby> foundLobbies = response.Results;

        if (foundLobbies.Any()) // Try to join a random lobby if one exists
        {
           await ConnectToHost(loggedInPlayer, foundLobbies[0]);
        }
        else // Didn't find any lobbies, create a new lobby
        {
           await CreateLobbyAndHostGame(loggedInPlayer);
        }

        Debug.Log($"Lobby info: {currentLobby.Name}");

        Debug.Log($"We have players {currentLobby.Players.Count} in the Lobby");

    }

    private async Task ConnectToHost(Player loggedInPlayer, Lobby lobby)
    {
        currentLobby = await Lobbies.Instance.JoinLobbyByIdAsync(
            lobbyId: lobby.Id,
            options: new JoinLobbyByIdOptions()
            {
                Player = loggedInPlayer
            });

        Debug.Log($"Joined lobby {currentLobby.Name} ({currentLobby.Id})");

        if (!currentLobby.Data.TryGetValue("JoinCode", out var joinCode))
        {
            Debug.LogError("Join-code missing");
            return;
        }

        Debug.Log($"We received join-code: {joinCode.Value} from lobby");


        var transport = NetworkManager.Singleton.gameObject.GetComponentInChildren<UnityTransport>();
        var allocation = await RelayUtility.JoinRelayServerFromJoinCode(joinCode.Value);

        transport.SetRelayServerData(
            allocation.ipv4address,
            allocation.port,
            allocation.allocationIdBytes,
            allocation.key,
            allocation.connectionData,
            allocation.hostConnectionData);

        NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;
        NetworkManager.Singleton.StartClient();
        // Debug.Log($"Started Client with ip {allocation.RelayServer.IpV4} port {allocation.RelayServer.Port}");
    }

    private async Task CreateLobbyAndHostGame(Player loggedInPlayer)
    {
        var (ipv4Address, port, allocationIdBytes, connectionData, key, joinCode) = 
            await RelayUtility.AllocateRelayServerAndGetJoinCode(8);

        Debug.Log($"We created lobby with join-code: {joinCode}");

        // Populate the new lobby with some data; use indexes so it's easy to search for
        var lobbyData = new Dictionary<string, DataObject>()
        {
            ["Test"] = new DataObject(DataObject.VisibilityOptions.Public, "true", DataObject.IndexOptions.S1),
            ["GameMode"] = new DataObject(DataObject.VisibilityOptions.Public, "ctf", DataObject.IndexOptions.S2),
            ["Skill"] = new DataObject(DataObject.VisibilityOptions.Public, Random.Range(1, 51).ToString(),
                DataObject.IndexOptions.N1),
            ["Rank"] = new DataObject(DataObject.VisibilityOptions.Public, Random.Range(1, 51).ToString()),
            ["JoinCode"] = new(DataObject.VisibilityOptions.Member, joinCode)
        };


        // Create a new lobby
        currentLobby = await Lobbies.Instance.CreateLobbyAsync(
            lobbyName: newLobbyName,
            maxPlayers: maxPlayers,
            options: new CreateLobbyOptions()
            {
                Data = lobbyData,
                IsPrivate = isPrivate,
                Player = loggedInPlayer
            });

        var transport = NetworkManager.Singleton.gameObject.GetComponentInChildren<UnityTransport>();

        transport.SetRelayServerData(
            ipv4Address,
            port,
            allocationIdBytes,
            key,
            connectionData
        );
        
        Debug.Log($"Created new lobby {currentLobby.Name} ({currentLobby.Id})");

        NetworkManager.Singleton.NetworkConfig.NetworkTransport = transport;
        
        NetworkManager.Singleton.StartHost();
        Debug.Log($"Started Host with ip {ipv4Address} port: {port}");
    }

    // Log in a player using Unity's "Anonymous Login" API and construct a Player object for use with the Lobbies APIs
    static async Task<Player> GetPlayerFromAnonymousLoginAsync()
    {
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            Debug.Log($"Trying to log in a player ...");

            // Use Unity Authentication to log in
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                throw new InvalidOperationException(
                    "Player was not signed in successfully; unable to continue without a logged in player");
            }
        }

        Debug.Log("Player signed in as " + AuthenticationService.Instance.PlayerId);

        // Player objects have Get-only properties, so you need to initialize the data bag here if you want to use it
        return new Player(AuthenticationService.Instance.PlayerId, null, new Dictionary<string, PlayerDataObject>());
    }

    private async void OnDisable()
    {
        await CleanupDemoLobbyAsync();
    }
}