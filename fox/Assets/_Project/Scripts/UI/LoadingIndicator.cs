using System.Collections;
using TMPro;
using UnityEngine;

public class LoadingIndicator : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI dotText;
    private const float DotInterval = 0.3f;

    private IEnumerator Start()
    {
        if (dotText == null)
        {
            Debug.LogError("LoadingIndicator 未配置文字组件。", this);
            yield break;
        }
        var delay = new WaitForSecondsRealtime(DotInterval);
        int count = 1;
        while (true)
        {
            dotText.text = new string('.', count);
            count = count % 6 + 1;
            yield return delay;
        }
    }
}
