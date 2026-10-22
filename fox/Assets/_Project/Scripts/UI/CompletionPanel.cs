using UnityEngine;
using UnityEngine.UI;

public class CompletionPanel : BasePanel
{
    [SerializeField] private Button btn;

    public override void Init()
    {
        if (RequireReferences(btn)) btn.onClick.AddListener(ReturnToMenu);
    }

    private void ReturnToMenu()
    {
        if (!SceneLoader.Load(SceneLoader.Scene.StartScene, () => UIManager.Instance.ShowPanel<StartPanel>())) return;
        btn.interactable = false;
        UIManager.Instance.HidePanel<CompletionPanel>();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (btn != null) btn.onClick.RemoveListener(ReturnToMenu);
    }
}
