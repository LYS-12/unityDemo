using UnityEngine;
using UnityEngine.Events;

public abstract class BasePanel : MonoBehaviour
{
    private CanvasGroup canvasGroup;
    private float transitionTime, fromAlpha;
    private bool isShowing;
    private UnityAction hideCallback;
    public bool IsShowing => isShowing;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        Init();
    }

    public abstract void Init();

    protected bool RequireReferences(params Object[] references)
    {
        foreach (Object reference in references)
        {
            if (reference != null)
                continue;
            Debug.LogError($"{GetType().Name} 存在未配置的 UI 引用。", this);
            enabled = false;
            return false;
        }
        return true;
    }

    public virtual void ShowMe()
    {
        if (this is LevelSelectPanel || this is CompletionPanel)
            GameplayPause.Set(this, true);
        hideCallback = null;
        isShowing = true;
        canvasGroup.alpha = 0f;
        fromAlpha = 0;
        transitionTime = 0;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;
        gameObject.SetActive(true);
    }

    public virtual void HideMe(UnityAction callback)
    {
        isShowing = false;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;
        hideCallback = callback;
        fromAlpha = canvasGroup.alpha;
        transitionTime = 0;
    }

    protected virtual void Update()
    {
        transitionTime += Time.unscaledDeltaTime;
        canvasGroup.alpha = Mathf.Lerp(fromAlpha, isShowing ? 1 : 0, Mathf.SmoothStep(0, 1, transitionTime / .25f));
        canvasGroup.interactable = isShowing && canvasGroup.alpha >= 1f;
        if (isShowing || canvasGroup.alpha > 0f || hideCallback == null)
            return;
        UnityAction callback = hideCallback;
        hideCallback = null;
        GameplayPause.Set(this, false);
        callback();
    }

    protected virtual void OnDestroy() => GameplayPause.Set(this, false);
}
