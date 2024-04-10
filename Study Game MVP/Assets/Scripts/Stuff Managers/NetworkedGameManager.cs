using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Threading.Tasks;
using System.Linq;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using System;

public class NetworkedGameManager : NetworkBehaviour
{

    #region Singleton

    public static NetworkedGameManager instance;

    void SetSingleton(){
        if(instance == null){
            instance = this;
        }else{
            Debug.LogWarning("Two instaces of NetworkedGameManager found. Deleting this gameobject");
            Destroy(gameObject);
        }
    }

    #endregion


    NetworkVariable<bool> GameHasStarted = new NetworkVariable<bool>();

    //USE THIS
    NetworkVariable<ushort> timeLeftInGame = new NetworkVariable<ushort>(){
        Value = 0
    };


    //This is generic for anything we need to keep track of 
    List<ulong> clientsWithTask;

    int numOfClientsWithTask;

    ClientRpcParams clientRpc = new ClientRpcParams();

    Scene loadedScene;
    bool sceneHasLoaded;

    Coroutine CheckTeamsCor;
    const float TimeBetweenTeamChecks = 1f;

    //THIS IS IMPORTANT (measured in ms)
    const float PingThreshold = 1000f;

    bool canAutoKick = false;


    void Awake() {
        SetSingleton();
    }

    public override void OnNetworkSpawn()
    {
        timeLeftInGame.OnValueChanged += Timer.instance.OnNetworkTimerChange;
        Timer.instance.OnNetworkTimerChange(0, timeLeftInGame.Value);

        GameHasStarted.Value = false;

        StartCoroutine(CheckLag());

        base.OnNetworkSpawn();
    }


    public void SetGameStarted(bool v){
        print("Got here in starting game on network!");
        if(!IsServer) {
            Debug.LogWarning("Trying to set game start state without being a host");
            return;
        }

        print("setting game start to: " + v);

        GameHasStarted.Value = v;

        if(!v && CheckTeamsCor != null) StopCoroutine(CheckTeamsCor);
        else if(v && CheckTeamsCor == null) StartCoroutine(CheckTeams());
    }
    public bool HasGameStarted(){
        return GameHasStarted.Value;
    }

    #region Start Setup Stuff (Disable Player Movement and whatnot)

    public async Task PrepareGameStart(){
        //Only run on the Server
        if(!IsServer) return;

        //We reset the Tracking variables
        clientsWithTask = new List<ulong>();
        numOfClientsWithTask = 0;
        
        //Just tell all the players to disable movement and whatnot
        foreach (var item in NetworkManager.ConnectedClientsIds)
        {
            clientRpc.Send.TargetClientIds = new List<ulong>(){
                NetworkPlayerObject.playerObjectDictionary[item].OwnerClientId
            };

            NetworkPlayerObject.playerObjectDictionary[item].StartGameDisableMovmentClientRpc(clientRpc);
        }

        //Now we wait until everyone has finished or just until 10 seconds have passed

        //We store the max resposes because someone who joins midway in this process won't respond
        int maxResponses = NetworkManager.ConnectedClients.Count;
        float timer = 10f;
        while(numOfClientsWithTask < Mathf.Clamp(NetworkManager.ConnectedClients.Count, 0, maxResponses) && timer > 0f){
            await Task.Yield();
            timer -= Time.deltaTime;

            //If someone disconnected midway then we won't expect responses from anyone that joins
            if(NetworkManager.ConnectedClients.Count < maxResponses) maxResponses = NetworkManager.ConnectedClients.Count;
        }
        print("Timer is: " + timer + "  And num of Clients is: " + numOfClientsWithTask);
    }

    #endregion

    #region Set Team Stuff
    int GetRequiredSeekersHAS(){
        return Mathf.CeilToInt(NetworkManager.ConnectedClientsIds.Count * 0.2f);
    }

    public async Task SetAllTeams_HideAndSeek()
    {
        //Only run on the Server
        if(!IsServer) return;

        //We reset the Tracking variables
        clientsWithTask = new List<ulong>();
        numOfClientsWithTask = 0;

        //Calculate 10%-20% rounded up and get a variable to count how many seekers we have already set
        int numberOfSeekersSet = GetRequiredSeekersHAS();
        int currentNumberOfSeekers = 0;

        //Shuffle the players so we assign them randomly
        ulong[] shuffledPlayers = shufflePlayers(NetworkManager.ConnectedClientsIds.ToArray());

        //Set the player to a Seeker if the "Seeker Quota" hasn't been met. If it has, set them to a Hider
        foreach (var playerID in shuffledPlayers)
        {
            if(currentNumberOfSeekers < numberOfSeekersSet)
            {
                NetworkPlayerObject.playerObjectDictionary[playerID].ServerSetTeam(2);
                currentNumberOfSeekers++;
            }else{
                NetworkPlayerObject.playerObjectDictionary[playerID].ServerSetTeam(1);
            }
        }

        //We store the max resposes because someone who joins midway in this process won't respond
        int maxResponses = NetworkManager.ConnectedClients.Count;
        float timer = 20f;
        
        while(numOfClientsWithTask < Mathf.Clamp(NetworkManager.ConnectedClients.Count, 0, maxResponses) && timer > 0f){
            await Task.Yield();
            timer -= Time.deltaTime;

            //If someone disconnected midway then we won't expect responses from anyone that joins
            if(NetworkManager.ConnectedClients.Count < maxResponses) maxResponses = NetworkManager.ConnectedClients.Count;
        }

        print("Teams are done with setup, we can start the game!");
    }

    public async Task ResetPlayerTeams(){
        //Only run on the Server
        if(!IsServer) return;

        //We reset the Tracking variables
        clientsWithTask = new List<ulong>();
        numOfClientsWithTask = 0;
        

        foreach (var playerID in NetworkManager.ConnectedClientsIds.ToArray())
        {
            NetworkPlayerObject.playerObjectDictionary[playerID].ServerSetTeam(0);
        }

        //We store the max resposes because someone who joins midway in this process won't respond
        int maxResponses = NetworkManager.ConnectedClients.Count;
        float timer = 20f;
        while(numOfClientsWithTask < Mathf.Clamp(NetworkManager.ConnectedClients.Count, 0, maxResponses) && timer > 0f){
            await Task.Yield();
            timer -= Time.deltaTime;

            //If someone disconnected midway then we won't expect responses from anyone that joins
            if(NetworkManager.ConnectedClients.Count < maxResponses) maxResponses = NetworkManager.ConnectedClients.Count;
        }
    }

    
    
    //STOLEN CODE FROM: "https://forum.unity.com/threads/randomize-array-in-c.86871/"
    ulong[] shufflePlayers(ulong[] ids)
    {
        // Knuth shuffle algorithm :: courtesy of Wikipedia :)
        for (int t = 0; t < ids.Length; t++ )
        {
            ulong tmp = ids[t];
            int r = UnityEngine.Random.Range(t, ids.Length);
            ids[t] = ids[r];
            ids[r] = tmp;
        }
        return ids;
    }

    #endregion

    #region Check Lag

    IEnumerator CheckLag(){
        while(true){
            yield return new WaitForSeconds(1f);
            
            if(canAutoKick){
                CheckPlayersForLag();
            }
        }
    }
    void CheckPlayersForLag(){
        for(int i = 0; i < PlayerLag.playerLags.Count; i++)
        {
            PlayerLag playerLag = PlayerLag.playerLags.Values.ToArray()[i];
            if(playerLag.GetPing() > PingThreshold){
                //We want to kick this player
                playerLag.ForceDisconnect();
            }
        }
    }

    public void SetAutoKick(bool v){
        canAutoKick = v;
    }

    #endregion

    #region Check Teams throughout Game

    IEnumerator CheckTeams(){
        float timer = TimeBetweenTeamChecks;

        while(true){
            timer -= Time.deltaTime;

            if(timer <= 0){
                timer = TimeBetweenTeamChecks;
                
                int requiredSeekers = NumOfNeededSeekersHAS();
                if(requiredSeekers > 0) SetHidersToSeekers(requiredSeekers);
            } 

            if(!HasGameStarted()) {
                yield break;
            }

            yield return null;
        }
    }

    /// <summary>
    /// This returns the number of needed seekers in a Hide and Seek game
    /// </summary>
    /// <returns></returns>
    int NumOfNeededSeekersHAS(){
        //Get all the players in a way where we have access to their teams
        Player[] players = NetworkPlayerObject.playerObjectDictionary.Values.ToArray();
        int currentNumberOfSeekers = 0;

        //Count up the number of seekers
        for (int i = 0; i < players.Length; i++)
        {
            if(players[i].GetTeam() == 2) currentNumberOfSeekers++;
        }

        //subtract the number of seekers their should be
        return GetRequiredSeekersHAS() - currentNumberOfSeekers;
    }

    void SetHidersToSeekers(int num){
        //First, we shuffle the players
        ulong[] shuffledPlayers = shufflePlayers(NetworkManager.ConnectedClientsIds.ToArray());

        //Then, we set any hiders to seekers (until we meet the quota)
        for (int i = 0; i < shuffledPlayers.Length; i++)
        {
            Player curPlay = NetworkPlayerObject.playerObjectDictionary[shuffledPlayers[i]];

            //If we haven't met our quota and the player isn't already a seeker, we set them to a seeker
            if(num > 0 && curPlay.GetTeam() != 2) {
                curPlay.ServerSetTeam(2);
                num--;
            }
        }
    }
    #endregion

    #region Load/Unload Scene Stuff
    public async Task LoadScenesForAll(){
        //This is only run on the host
        if(!IsHost) return;

        //TEST CODE
        //UnityEngine.SceneManagement.SceneManager.LoadScene(MapSceneManager.instance.GetActiveMap(), LoadSceneMode.Additive);
        //return;

        //reset variables
        sceneHasLoaded = false;

        //Get the current amount of players

        //Calculate what map to load

        //load that map

        //First we subscribe to the event so we can store data
        NetworkManager.SceneManager.OnSceneEvent += OnSceneEvent;
        //Load it on every client
        SceneEventProgressStatus progressStatus = NetworkManager.SceneManager.LoadScene(MapSceneManager.instance.GetActiveMap(), LoadSceneMode.Additive);

        while(!sceneHasLoaded) {await Task.Yield();}

        sceneHasLoaded = false;

        //We are done!
    }


    void OnSceneEvent(SceneEvent e){
        //if every client is done then we can go!
        if(e.ClientId == NetworkManager.ServerClientId && !string.IsNullOrEmpty(e.Scene.name)){
            loadedScene = e.Scene;
            print("We set scene: " + e.Scene.name + " to our currently loaded scene: " + loadedScene.name);
        }
        if(e.SceneEventType == SceneEventType.LoadEventCompleted && IsHost){
            sceneHasLoaded = true;
            NetworkManager.SceneManager.OnSceneEvent -= OnSceneEvent;
        }
    }

    public void UnloadGameForAll(){
        //This is only run on the host
        if(!IsHost) return;

        //unload the active scene
        print("We are unloading! Scene: " + loadedScene.name);
        NetworkManager.SceneManager.UnloadScene(loadedScene);

        //We are done! (THIS ISN'T DONE ON EVERY CLIENT BY THE TIME THIS IS DONE)
    }
    #endregion

    #region Teleport Players


    //THIS NEEDS TO CHANGE - we need options for maps
    public async Task TeleportAllPlayers(bool toLobby){
        //We reset the tracking variables
        clientsWithTask = new List<ulong>();
        numOfClientsWithTask = 0;
        
        foreach (var id in NetworkManager.ConnectedClientsIds)
        {
            NetworkPlayerObject.playerObjectDictionary[id].ServerTeleport(toLobby);
        }

        //We store the max resposes because someone who joins midway in this process won't respond
        int maxResponses = NetworkManager.ConnectedClients.Count;
        float timer = 20f;
        while(numOfClientsWithTask < Mathf.Clamp(NetworkManager.ConnectedClients.Count, 0, maxResponses) && timer > 0f){
            await Task.Yield();
            timer -= Time.deltaTime;

            //If someone disconnected midway then we won't expect responses from anyone that joins
            if(NetworkManager.ConnectedClients.Count < maxResponses) maxResponses = NetworkManager.ConnectedClients.Count;
        }
    }

    #endregion

    #region Set Timer Stuff
    
    public void SetupTimer(){
        //This is one of the only functions that doesn't track player progress
        Timer.instance.SetTimerWithDelay(GameManager.instance.maxTimer, 5f);
    }

    public void SetTimer(float t){
        ushort u = (ushort)(t * 10);
        timeLeftInGame.Value = u;
    }

    public float GetTime(){
        return (float)timeLeftInGame.Value / 10;
    }

    #endregion

    #region Finish Game Setup

    public void FinishGameSetup(){
        //This just tells all the clients that the game is done with it's setup
        GameDoneSetupClientRpc();
    }

    [ClientRpc]
    void GameDoneSetupClientRpc(){
        Player.isRightPlayer.StartGame();
    }

    #endregion
    
    #region End Game Stuff
    
    /// <summary>
    /// This tells all the clients that we are ending the game
    /// </summary>
    /// <returns></returns>
    public async Task EndGameServer(){
        //Only run on the Server
        if(!IsServer) return;

        //We reset the Tracking variables
        clientsWithTask = new List<ulong>();
        numOfClientsWithTask = 0;

        //Just tell all the players to disable movement and whatnot
        foreach (var item in NetworkManager.ConnectedClientsIds)
        {
            clientRpc.Send.TargetClientIds = new List<ulong>(){
                NetworkPlayerObject.playerObjectDictionary[item].OwnerClientId
            };

            NetworkPlayerObject.playerObjectDictionary[item].EndGameDisableMovementClientRpc(clientRpc);
        }

        //Now we wait until everyone has finished or just until 10 seconds have passed

        //We store the max resposes because someone who joins midway in this process won't respond
        int maxResponses = NetworkManager.ConnectedClients.Count;
        float timer = 10f;
        while(numOfClientsWithTask < Mathf.Clamp(NetworkManager.ConnectedClients.Count, 0, maxResponses) && timer > 0f){
            await Task.Yield();
            timer -= Time.deltaTime;

            //If someone disconnected midway then we won't expect responses from anyone that joins
            if(NetworkManager.ConnectedClients.Count < maxResponses) maxResponses = NetworkManager.ConnectedClients.Count;
        }
    }

    /// <summary>
    /// This tells all the clients that we are done going back to the lobby and we can start stuff
    /// </summary>
    public void EndGameDoneServer(){
        //Only run on the Server
        if(!IsServer) return;

        //This task is also not confirmed

        foreach(var item in NetworkManager.ConnectedClientsIds){
            clientRpc.Send.TargetClientIds = new List<ulong>(){
                NetworkPlayerObject.playerObjectDictionary[item].OwnerClientId
            };

            NetworkPlayerObject.playerObjectDictionary[item].EndGameEnableMovementClientRpc(clientRpc);
        }
    }

    

    #endregion
    
    public void ConfirmTask(ulong id){
        //This just makes sure the same client can't confirm twice
        if(clientsWithTask.Contains(id)) return;

        clientsWithTask.Add(id);
        numOfClientsWithTask++;
    }
    //We also want to receive data like Time and whatnot

    #region Leave Game Stuff

    public async void Disconnect(){
        if(!IsHost) {
            NetworkPlayerObject.Singleton.ResetDictionary();
            PlayerNickname.ResetData();
            NetworkManager.Shutdown();
        }
        else{
            ClientDisconnectClientRpc();

            while(NetworkManager.ConnectedClientsIds.Count > 1){
                await Task.Yield();
            }

            NetworkPlayerObject.Singleton.ResetDictionary();
            PlayerNickname.ResetData();
            NetworkManager.Shutdown();
        }
    }

    

    [ClientRpc]
    void ClientDisconnectClientRpc(){
        if(IsHost) return;
        NetworkPlayerObject.Singleton.ResetDictionary();
        PlayerNickname.ResetData();

        NetworkManager.Shutdown();
        
        GameManager.instance.LeaveLobby();
    }

    #endregion
}
