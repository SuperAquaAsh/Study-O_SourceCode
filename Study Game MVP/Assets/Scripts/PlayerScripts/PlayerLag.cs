using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

//This just manages the lag of one specific player. It exists so we can tie it to a player and not an ID
public class PlayerLag : NetworkBehaviour
{
    #region Singleton
    public static PlayerLag instance;
    void SetSingleton(){
        if(!IsOwner) return;

        if(instance == null){
            instance = this;
        }else{
            Debug.Log("Two correct instances of PlayerLag found! Destroying this gameobject!");
        }
    }

    public override void OnNetworkSpawn()
    {
        if(IsOwner) SetSingleton();

/*         if(playerLags.Count != 0 && IsOwner){
            Debug.LogWarning("Dictionary is still populated, you need to fix that! Count: " + playerLags.Count);
            ResetDictionary();
        } */

        Debug.Log("Added to the dictionary! ID: " + OwnerClientId);
        playerLags.Add(OwnerClientId, this);

        player = GetComponent<Player>();
        base.OnNetworkSpawn();
    }
    #endregion
    public NetworkVariable<ushort> networkPing = new NetworkVariable<ushort>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    public static Dictionary<ulong, PlayerLag> playerLags = new Dictionary<ulong, PlayerLag>();

    Player player;

    public void SetPing(float ping){
        networkPing.Value = (ushort)Mathf.Clamp(Mathf.RoundToInt(ping), 0, 65535);
    }
    public float GetPing(){
        return networkPing.Value;
    }

    public static PlayerLag Get(ulong id){
        return playerLags[id];
    }
    public void ForceDisconnect(){
        if(!IsHost) return;

        ClientRpcParams clientRpc = new ClientRpcParams(){
            Send = new ClientRpcSendParams(){
                TargetClientIds = new List<ulong>{
                    player.OwnerClientId
                }
            }
        };

        player.ForceDisconnectClientRpc(clientRpc);
    }

    public void ResetDictionary(){
        Debug.Log("WE RESET THE DICTIONARY!!");
        playerLags.Clear();
    }
}
