using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UiManager : MonoBehaviour
{
    [Header("Main Menu")]

    public Button playBtn;
    public Button settingsBtn;
    public Button quitBtn;
    public GameObject memuPanel;

    [Header("Game TypePanel")]
    public GameObject gameTypePanel;
    public Button fourCard;
    public Button sixCards;
    public Button thirtyCard;


    [Header("settings Panel")]
    public GameObject settingPanel; 



    private void Start()
    {
       // quitBtn.onClick.AddListener(Application.Quit);
    }







    private void OnApplicationQuit()
    {

    }



}
