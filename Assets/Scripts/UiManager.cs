using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UiManager : MonoBehaviour
{
    [Header("Main Menu")]

    public Button continueBtn;
    public Button NewGameBtn;
    public Button settingsBtn;
    public Button quitBtn;
    public GameObject menuPanel;

    [Header("Game TypePanel")]
    public GameObject gamePlayPanel;
    public Button PauseBtn;
    public TMP_Text Level;
    public TMP_Text ScoreText;

    [Header("settings Panel")]
    public GameObject settingPanel;
    public Toggle audioToggle;

    [Header("Pause Menu")]
    public GameObject pausePanel;
    public Button settings;
    public Button resumeBtn;
    public Button mainMenu;
   

    [Header("Level Complete")]
    public GameObject LevelComplete;
    public TMP_Text Score;
    public Button NextLevelBtn;


    #region
    public static Action NewGameAction;
    public static Action ContinueGameAction;
    public static Action NextLevelAction;
    public static Action ClearBoardAction;
    #endregion



    private void Start()
    {
        continueBtn.gameObject.SetActive(GameManager.hasSaveData);
        NewGameBtn.onClick.AddListener(() => { GamePlay(); NewGameAction?.Invoke(); });
        continueBtn.onClick.AddListener(() => { ContinueGameAction?.Invoke(); GamePlay(); });
        quitBtn.onClick.AddListener(Application.Quit);
        NextLevelBtn.onClick.AddListener(() => { NextLevelAction?.Invoke(); NextLevel(); });
        settingsBtn.onClick.AddListener(() => { ActivateSettingsPanel(true); });

        PauseBtn.onClick.AddListener(() => ActivatePausePanel(true));
        settings.onClick.AddListener(() => { ActivateSettingsPanel(true); });
        resumeBtn.onClick.AddListener(() => ActivatePausePanel(false));

        audioToggle.onValueChanged.AddListener(ToggleAudio);


    }

    public void SettingPanel(bool Value)
    {
        settingPanel.SetActive(Value);
        menuPanel.SetActive(!Value);
    }

    private void GamePlay()
    {
        menuPanel.SetActive(false);
        gamePlayPanel.SetActive(true);
        Level.text = $"Level:{GameManager.Instance.currentLevelIndex + 1}";

    }

    private void ToggleAudio(bool value)
    {
        AudioManager.Instance.oneShotSource.mute = !value;

    }

    private void ActivateSettingsPanel(bool Value)
    {
        settingPanel.SetActive(Value);
    }

    private void ActivatePausePanel(bool Value)
    {
        pausePanel.SetActive(Value);
        GameManager.Instance.InputAllowed = !Value;
    }

    private void RetunMainMenu()
    {
        menuPanel.SetActive(true);
        gamePlayPanel.SetActive(false);
        continueBtn.gameObject.SetActive(true);
        ClearBoardAction?.Invoke();
    }


    private void LevelCompleted()
    {
        LevelComplete.SetActive(true);
        Score.text = $"Score: {GameManager.Instance.score}";
    }

    private void NextLevel()
    {
        LevelComplete.SetActive(false);
        Level.text = $"Level:{GameManager.Instance.currentLevelIndex + 1}";
    }


    private void UpdateScore(int Score, int Combo)
    {
        ScoreText.text = $"Score: {Score}\nCombo:{Combo}";
    }



    private void OnEnable()
    {
        GameManager.ScoreUpdateAction += UpdateScore;
        GameManager.LevelCompleteAction += LevelCompleted;
    }

    private void OnDisable()
    {
        GameManager.ScoreUpdateAction -= UpdateScore;
        GameManager.LevelCompleteAction -= LevelCompleted;
    }

}
