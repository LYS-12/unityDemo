using UnityEngine;

[DefaultExecutionOrder(-500)]
public class GameplayBootstrap : MonoBehaviour
{
    public GameplayCatalog catalog;
    public Player player;
    public Transform canvasRoot;

    private void Awake()
    {
        if (catalog == null || player == null || canvasRoot == null)
        {
            Debug.LogError("GameplayBootstrap 配置不完整。", this);
            enabled = false;
            return;
        }
        RunSession.Ensure(catalog);
    }

    private void Start()
    {
        GameObject view = Instantiate(catalog.hudPrefab, canvasRoot, false);
        view.GetComponent<GameplayHUD>().Bind(player, catalog);
    }
}
