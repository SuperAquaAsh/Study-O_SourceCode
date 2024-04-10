using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ConnectionsListManager : MonoBehaviour
{
    #region Singleton
    public static ConnectionsListManager instance;

    void SetSingleton(){
        if(instance == null){
            instance = this;
        }else{
            Debug.LogWarning("Two instances of ConnectionsListManager found! Destroying this gameobject!");
            Destroy(gameObject);
        }
    }
    #endregion
    [SerializeField] Color backgroundColor;
    [SerializeField] Image image;
    [SerializeField] GameObject playerConnection;
    [SerializeField] Transform connectionList;

    [SerializeField] LagImage[] lagImages;

    List<PlayerConnection> playerConnections = new List<PlayerConnection>();

    public bool isVisable {get; private set;}

    void Awake(){
        SetSingleton();
    }

    public void SpwanConnection(Player player){
        GameObject go = Instantiate(playerConnection, connectionList);
        PlayerConnection con = go.GetComponent<PlayerConnection>();

        con.SetFollowPlayer(player);
        playerConnections.Add(con);
    }
    public void DestroyConnection(Player player){
        for (int i = 0; i < playerConnections.Count; i++)
        {
            if(playerConnections[i].IsConnectedToPlayer(player)){
                Destroy(playerConnections[i].gameObject);
                playerConnections.Remove(playerConnections[i]);
                return;
            }
        }

        Debug.LogError("No playerConnection found for Id: " + player.OwnerClientId);
    }
    public void DestroyConnection(ulong id){
        if(!NetworkPlayerObject.playerObjectDictionary.ContainsKey(id)) return;
        if(playerConnections[0] == null) return;

        Player player = NetworkPlayerObject.playerObjectDictionary[id];
        for (int i = 0; i < playerConnections.Count; i++)
        {
            if(playerConnections[i].IsConnectedToPlayer(player) && playerConnections[i] != null){
                Destroy(playerConnections[i].gameObject);
                playerConnections.Remove(playerConnections[i]);
                return;
            }
        }

        Debug.LogError("No playerConnection found for Id: " + player.OwnerClientId);
    }

    public Sprite GetLagImage(ushort lag){
        for(int i = 0; i < lagImages.Length; i++){
            if(lag >= lagImages[i].minLag && lag < lagImages[i].maxLag){
                return lagImages[i].image;
            }
        }

        Debug.LogWarning("No lag image found for lag value: " + lag);
        return lagImages.Last().image;
    }

    void Update(){
        if(Input.GetKeyDown(KeyCode.Tab)) isVisable = !isVisable;
        
        if(isVisable) image.color = backgroundColor;
        else image.color = Color.clear;
    }
}

[Serializable]
public class LagImage{
    public Sprite image;

    public ushort minLag;
    public ushort maxLag;
}