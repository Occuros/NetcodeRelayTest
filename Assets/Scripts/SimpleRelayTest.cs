using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Multiplayer.Samples.BossRoom;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class SimpleRelayTest : MonoBehaviour
{
    [SerializeField] private UnityTransport _transport;
    [SerializeField] private string _joincode;
    [SerializeField] private bool _isHosting;

    static readonly char[] k_InputFieldIncludeChars = new[] {'.', '_'};

    private async void Start()
    {
        if (_isHosting)
        {
            HostRelay();
        }
        else
        {
            ClientRelay();
        }
    }

    private async void ClientRelay()
    {
        Debug.Log($"trying to join host with join-code {_joincode}");

        _joincode = Sanitize(_joincode, k_InputFieldIncludeChars);
        var allocation = await RelayUtility.JoinRelayServerFromJoinCode(_joincode);

        Debug.Log($"We joined host with join-code {_joincode}");

        _transport.SetRelayServerData(
            allocation.ipv4address,
            allocation.port,
            allocation.allocationIdBytes,
            allocation.key,
            allocation.connectionData,
            allocation.hostConnectionData
        );

        NetworkManager.Singleton.StartClient();
    }

    private async void HostRelay()
    {
        try
        {
            var allocation = await RelayUtility.AllocateRelayServerAndGetJoinCode(8);

            _joincode = allocation.joinCode;

            Debug.Log($"We created host with join-code {_joincode}");

            _transport.SetRelayServerData(
                allocation.ipv4address,
                allocation.port,
                allocation.allocationIdBytes,
                allocation.key,
                allocation.connectionData
            );

            NetworkManager.Singleton.StartHost();
        }
        catch (Exception e)
        {
            Debug.LogError($"Something went terribly wrong {e.Message}" +
                           $"\n{e.StackTrace}");
            throw;
        }
    }

    static string Sanitize(string dirtyString, char[] includeChars = null)
    {
        var result = new StringBuilder(dirtyString.Length);
        foreach (char c in dirtyString)
        {
            if (char.IsLetterOrDigit(c) ||
                (includeChars != null && Array.Exists(includeChars, includeChar => includeChar == c)))
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }
}