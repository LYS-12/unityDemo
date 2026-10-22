using System.Collections;
using UnityEngine;

public class LoadingSceneController : MonoBehaviour
{
    private IEnumerator Start()
    {
        // 给加载场景至少一个更新帧，再开始异步加载目标。
        yield return null;
        AsyncOperation operation = SceneLoader.LoadTargetScene();
        if (operation != null) yield return operation;
    }
}
