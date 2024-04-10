using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class PlayerConnection : MonoBehaviour
{
    Player followPlayer;

    [SerializeField] Image image;
    [SerializeField] TextMeshProUGUI text;

    Coroutine setDataCor;

    public void SetFollowPlayer(Player player){
        //If we are still waiting to set data for a client then we stop
        if(setDataCor != null) StopCoroutine(setDataCor);

        //If we are already following a player then we should change
        if(followPlayer != null) followPlayer.playerLag.networkPing.OnValueChanged -= OnLagChange;
        
        followPlayer = player;
        
        if(followPlayer.playerLag == null || !PlayerNickname.playerIdName.ContainsKey(followPlayer.OwnerClientId)){
            setDataCor = StartCoroutine(SetDataWhenPlayerIsReady());
            return;
        }

        text.text = PlayerNickname.playerIdName[followPlayer.OwnerClientId];
        print("Set the connection name to: " + text.text);

        followPlayer.playerLag.networkPing.OnValueChanged += OnLagChange;

        OnLagChange(0, followPlayer.playerLag.networkPing.Value);
    }
    IEnumerator SetDataWhenPlayerIsReady(){
        while(followPlayer.playerLag == null) yield return null;
        while(!PlayerNickname.playerIdName.ContainsKey(followPlayer.OwnerClientId)) yield return null;
        while(string.IsNullOrEmpty(PlayerNickname.playerIdName[followPlayer.OwnerClientId])) yield return null;



        text.text = PlayerNickname.playerIdName[followPlayer.OwnerClientId];
        print("Set the connection name to: " + PlayerNickname.playerIdName[followPlayer.OwnerClientId]);

        followPlayer.playerLag.networkPing.OnValueChanged += OnLagChange;

        OnLagChange(0, followPlayer.playerLag.networkPing.Value);
    }

    void OnLagChange(ushort p, ushort c){
        image.sprite = ConnectionsListManager.instance.GetLagImage(c);
    }

    public bool IsConnectedToPlayer(Player player){
        return player.OwnerClientId == followPlayer.OwnerClientId;
    }

    public bool IsFollowPlayerHost(){
        return followPlayer.IsOwnedByServer;
    }
    void Update(){
        SetVisablity(ConnectionsListManager.instance.isVisable);
    }
    
    void SetVisablity(bool isVisable){
        if(isVisable){
            image.color = Color.white;
            text.color = Color.white;
        }else{
            image.color = Color.clear;
            text.color = Color.clear;
        }
    }

    #region Kick button stuff
    public void KickPlayer(){
        if(followPlayer == null) return;

        print("Attepted kick");

        followPlayer.HostForceDisconnect();
    }
    #endregion
}
