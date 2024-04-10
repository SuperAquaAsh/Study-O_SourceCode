using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class PlayerKickButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] PlayerConnection playerConnection;

    [SerializeField] Image kickButtonImage;
    [SerializeField] Color kickButtonColor;

    [SerializeField] TextMeshProUGUI text;
    [SerializeField] Color textColor;

    void Start(){
        //kickButtonImage.color = Color.clear;
        text.color = Color.clear;

        if(!NetworkManager.Singleton.IsHost || playerConnection.IsFollowPlayerHost()) Destroy(gameObject);
    }

    public void KickPlayer(){
        playerConnection.KickPlayer();
    }

    public void OnPointerEnter(PointerEventData eventData){
        print("We are here is showing!");
        if(!ConnectionsListManager.instance.isVisable || !GameManager.instance.isHost) return;
        
        
        //if(kickButtonImage != null) kickButtonImage.color = kickButtonColor;

        if(text != null) text.color = textColor;
    }

    public void OnPointerExit(PointerEventData eventData){
        if(!ConnectionsListManager.instance.isVisable || !GameManager.instance.isHost) return;
        print("We are here is hiding! And the host");

        
        //if(kickButtonImage != null) kickButtonImage.color = Color.clear;
        
        if(text != null) text.color = Color.clear;
    }
}
