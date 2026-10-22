using UnityEngine;
using UnityEngine.UI;

public class LevelSelectPanel : BasePanel
{
    [SerializeField] private Button btnCard1;
    [SerializeField] private Button btnCard2;
    [SerializeField] private Button btnCard3;
    [SerializeField] private Button backBtn;

    public override void Init()
    {
        if (!RequireReferences(btnCard1, btnCard2, btnCard3))
            return;
        btnCard1.onClick.AddListener(LoadFirstLevel);
        btnCard2.onClick.AddListener(LoadSecondLevel);
        btnCard3.onClick.AddListener(LoadThirdLevel);
        if (backBtn != null)
            backBtn.onClick.AddListener(Back);
    }
    private void Back()
    {
        UIManager.Instance.HidePanel<LevelSelectPanel>();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "StartScene")
            UIManager.Instance.ShowPanel<StartPanel>();
    }

    private void LoadFirstLevel() => LoadLevel(SceneLoader.Scene.GameScene1);
    private void LoadSecondLevel() => LoadLevel(SceneLoader.Scene.GameScene2);
    private void LoadThirdLevel() => LoadLevel(SceneLoader.Scene.GameScene3);

    private void LoadLevel(SceneLoader.Scene scene)
    {
        if (SceneLoader.IsLoading)
            return;
        if (!SceneLoader.Load(scene, () =>
        {
            switch (scene)
            {
                case SceneLoader.Scene.GameScene1:
                    break;
                case SceneLoader.Scene.GameScene2:
                    UIManager.Instance.ShowPanel<OrderPanel2>();
                    break;
                case SceneLoader.Scene.GameScene3:
                    UIManager.Instance.ShowPanel<OrderPanel3>();
                    break;
            }
        }))
            return;
        btnCard1.interactable = false;
        btnCard2.interactable = false;
        btnCard3.interactable = false;
        UIManager.Instance.HidePanel<LevelSelectPanel>();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (btnCard1 != null)
            btnCard1.onClick.RemoveListener(LoadFirstLevel);
        if (btnCard2 != null)
            btnCard2.onClick.RemoveListener(LoadSecondLevel);
        if (btnCard3 != null)
            btnCard3.onClick.RemoveListener(LoadThirdLevel);
        if (backBtn != null)
            backBtn.onClick.RemoveListener(Back);
    }
}
