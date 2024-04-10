using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class Timer : MonoBehaviour
{
    #region Singleton
    public static Timer instance;
    void SetSingleton(){
        if(instance == null){
            instance = this;
        }else{
            Debug.LogWarning("Two instances of Timer found, deleting gameobject");
            Destroy(gameObject);
        }
    }
    #endregion

    [SerializeField] TextMeshProUGUI text;
    [SerializeField] Image divider;

    bool canCountdown = false;

    float timer;


    const float timeBetweenNetworkSyncs = 5f;
    float timeTillNetworkSync = timeBetweenNetworkSyncs;

    public float timeUntilCountdown {get; private set;}


    public event EventHandler OnTimerEnd;
    
    void Awake() {
        SetSingleton();
    }
    
    // Start is called before the first frame update
    void Start()
    {
        canCountdown = false;
        timer = 0f;
    }

    // Update is called once per frame
    void Update()
    {
        UpdateTimer();

        if(!NetworkManager.Singleton.IsHost) return;

        timeTillNetworkSync -= Time.deltaTime;

        if(timeTillNetworkSync <= 0f){
            PushTimeToNetwork();
        }
    }

    String IntToTime(int v){
        int minutes = 0;
        while(v >= 60){
            v -= 60;
            minutes++;
        }
        string sec = v.ToString();
        if(sec.Length == 1){
            sec = "0" + sec;
        }

        return minutes + ":" + sec;
    }

    void UpdateTimer(){
        if(canCountdown) timer -= Time.deltaTime;

        if(timer <= 0) {
            timer = 0;
            canCountdown = false;
            text.text = "";
            divider.enabled = false;
            if(OnTimerEnd != null) OnTimerEnd(this, new EventArgs());
            return;
        }


        text.text = IntToTime((int)Mathf.Ceil(timer));
        divider.enabled = true;
    }

    public void PushTimeToNetwork(){
        timeTillNetworkSync = timeBetweenNetworkSyncs;
        NetworkedGameManager.instance.SetTimer(timer);
    }

    #region Set Stuff

    public void SetTimerWithDelay(float time, float delay){
        StartCoroutine(StartTimerWithDelay(time, delay));
        return;
    }

    IEnumerator StartTimerWithDelay(float time, float delay){
        
        timer = time;
        canCountdown = false;
        timeUntilCountdown = delay;
        while(timeUntilCountdown > 0f){
            timeUntilCountdown -= Time.deltaTime;
            yield return null;
        }

        //CHANGE CODE HERE to allow for a countdown

        timer = time;
        canCountdown = true;

        GameManager.instance.SetGameStart(true);
        
        yield return null;
    }

    public void OnNetworkTimerChange(ushort p, ushort c){
        
        //We also stop the menu from poping up

        timer = NetworkedGameManager.instance.GetTime();

        if(timer > 0){
            canCountdown = true;
            divider.enabled = true;
        }else{
            timer = 0;
            canCountdown = false;
            text.text = "";
            divider.enabled = false;
        }
    }

    #endregion

    #region Get Stuff (lol one function)

    public float GetTimer() {return timer;}

    #endregion

}
