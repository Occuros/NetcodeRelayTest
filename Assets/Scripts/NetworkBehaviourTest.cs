using Unity.Netcode;
using UnityEngine;


public class NetworkBehaviourTest : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        Debug.Log("We spawned");

        Hello_ServerRpc();

        if (IsServer)
        {
            HelloClient_ClientRpc();
        }
    }


    [ServerRpc]
    public void Hello_ServerRpc()
    {
        Debug.Log("Hello ServerRPC");
    }

    [ClientRpc]
    public void HelloClient_ClientRpc()
    {
        Debug.Log("Hello From ClientRPC");
    }
}