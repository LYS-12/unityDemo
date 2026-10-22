using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ScreenTransition : MonoBehaviour
{
    private static ScreenTransition instance;
    private CanvasGroup group;
    private static ScreenTransition Ensure()
    {
        if (instance != null)
            return instance;
        instance = new GameObject("ForestSceneTransition").AddComponent<ScreenTransition>();
        DontDestroyOnLoad(instance.gameObject);
        return instance;
    }
    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        gameObject.AddComponent<GraphicRaycaster>();
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0;
        group.blocksRaycasts = false;
        var image = PartyUI.Stretch("CreamCurtain", transform).gameObject.AddComponent<Image>();
        image.color = new Color(.98f, .96f, .88f);
    }
    public static void Cover(Action covered)
    {
        var service = Ensure();
        service.StopAllCoroutines();
        service.StartCoroutine(service.Fade(1, covered));
    }
    public static void Reveal(Action revealed = null)
    {
        var service = Ensure();
        service.StopAllCoroutines();
        service.StartCoroutine(service.Fade(0, revealed));
    }
    private IEnumerator Fade(float target, Action completed)
    {
        float from = group.alpha, elapsed = 0;
        group.blocksRaycasts = true;
        while (elapsed < .45f)
        {
            elapsed += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, target, Mathf.SmoothStep(0, 1, elapsed / .45f));
            yield return null;
        }
        group.alpha = target;
        group.blocksRaycasts = target > 0;
        completed?.Invoke();
    }
    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
