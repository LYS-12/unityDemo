using UnityEngine;

/// <summary>每个场景显式注册自己的 Canvas，不跨场景保留相机或 UI 对象。</summary>
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(Canvas))]
public class SceneUIRoot : MonoBehaviour
{
    private void Awake() => UIManager.Instance.RegisterCanvas(transform);
    private void OnDestroy() => UIManager.Instance.UnregisterCanvas(transform);
}
