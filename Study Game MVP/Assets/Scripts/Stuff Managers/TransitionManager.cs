using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TransitionManager : MonoBehaviour
{
    #region Singleton
    public static TransitionManager instance;
    void SetSingleton(){
        if(instance == null){
            instance = this;
        }else{
            Debug.LogWarning("Two instances of TranstionManager Found! Destroying this gameobject!");
            Destroy(gameObject);
        }
    }
    #endregion

    [SerializeField] Animator transitionAnimator;

    [SerializeField] Animator loadingAnimator;

    bool isIn;

    void Awake(){
        SetSingleton();
    }

    public void SetTransition(bool v){
        if(v) transitionAnimator.Play("Transition_In", 0, 0);
        else transitionAnimator.Play("Transition_Out", 0, 0);

        isIn = v;
        loadingAnimator.Play("Load_Loop");
    }

    public void ToggleTransition(){
        bool v = !isIn;
        if(v) transitionAnimator.Play("Transition_In", 0, 0);
        else transitionAnimator.Play("Transition_Out", 0, 0);

        isIn = v;
        loadingAnimator.Play("Load_Loop");
    }
}
