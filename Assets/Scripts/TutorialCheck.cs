using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TutorialCheck : MonoBehaviour
{
    private int check;
    public GameObject tutorial;
    void IfChecked()
    {
        check = PlayerPrefs.GetInt("Tutor", 0);
    
    }
    void Start()
    {
        IfChecked();
        if(check == 0){
            tutorial.SetActive(true);
        }
    }
    public void Cheked(){
        check = 1;
        PlayerPrefs.SetInt("Tutor", check);
        PlayerPrefs.Save();
    }
}
