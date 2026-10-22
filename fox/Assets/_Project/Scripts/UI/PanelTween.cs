using System;
using UnityEngine;

public class PanelTween : MonoBehaviour
{
    [Min(.01f)] public float duration = .25f;
    private CanvasGroup group;
    private float from, to, time;
    private bool running;
    private Action completed;
    public void SetVisible(bool visible, Action onComplete = null)
    {
        if (group == null)
        {
            group = GetComponent<CanvasGroup>();
            if (group == null)
                group = gameObject.AddComponent<CanvasGroup>();
        }
        if (visible && !gameObject.activeSelf)
            group.alpha = 0;
        gameObject.SetActive(true);
        from = group.alpha;
        to = visible ? 1 : 0;
        time = 0;
        running = true;
        completed = onComplete;
        group.interactable = false;
        group.blocksRaycasts = true;
    }
    private void Update()
    {
        if (!running)
            return;
        time += Time.unscaledDeltaTime;
        group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, time / duration));
        if (time < duration)
            return;
        running = false;
        group.interactable = to > 0;
        group.blocksRaycasts = to > 0;
        Action callback = completed;
        completed = null;
        if (to == 0)
            gameObject.SetActive(false);
        callback?.Invoke();
    }
}
