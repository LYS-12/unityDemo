using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 original;
    private float target = 1;
    private void Awake()
    {
        original = transform.localScale;
        if (GetComponent<Shadow>() == null)
        {
            var shadow = gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.12f, .24f, .19f, .14f);
            shadow.effectDistance = new Vector2(0, -4);
            shadow.useGraphicAlpha = true;
        }
    }
    private void Update() => transform.localScale = Vector3.Lerp(transform.localScale, original * target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 20));
    public void OnPointerEnter(PointerEventData data)
    {
        if (!GetComponent<Selectable>().IsInteractable())
            return;
        target = 1.025f;
        PartyAudio.Play(PartySound.Hover);
    }
    public void OnPointerExit(PointerEventData data) => target = 1;
    public void OnPointerDown(PointerEventData data)
    {
        if (!GetComponent<Selectable>().IsInteractable())
            return;
        target = .96f;
        PartyAudio.Play(PartySound.Confirm);
    }
    public void OnPointerUp(PointerEventData data) => target = 1;
    private void OnDisable()
    {
        transform.localScale = original;
        target = 1;
    }
}
