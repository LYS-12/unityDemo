using UnityEngine;
using UnityEngine.UI;

public class StartPanel : BasePanel
{
    [SerializeField] private Button startBtn;
    [SerializeField] private Button settingBtn;
    [SerializeField] private Button levelSelectBtn;
    [SerializeField] private Button quitBtn;

    public override void Init()
    {
        if (!RequireReferences(startBtn, settingBtn))
            return;
        startBtn.onClick.AddListener(StartJourney);
        settingBtn.onClick.AddListener(OpenSettings);
        if (levelSelectBtn != null)
            levelSelectBtn.onClick.AddListener(OpenLevelSelect);
        if (quitBtn != null)
            quitBtn.onClick.AddListener(Quit);
    }

    private void StartJourney()
    {
        RunSession.End();
        SceneLoader.Load(SceneLoader.Scene.GameScene1);
    }
    private void Quit() => Application.Quit();

    private void OpenLevelSelect()
    {
        RunSession.End();
        UIManager.Instance.HidePanel<StartPanel>();
        UIManager.Instance.ShowPanel<LevelSelectPanel>();
    }

    private void OpenSettings()
    {
        UIManager.Instance.HidePanel<StartPanel>();
        UIManager.Instance.ShowPanel<SettingsPanel>();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (startBtn != null)
            startBtn.onClick.RemoveListener(StartJourney);
        if (settingBtn != null)
            settingBtn.onClick.RemoveListener(OpenSettings);
        if (levelSelectBtn != null)
            levelSelectBtn.onClick.RemoveListener(OpenLevelSelect);
        if (quitBtn != null)
            quitBtn.onClick.RemoveListener(Quit);
    }
}
