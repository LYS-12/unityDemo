using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SettingsPanel : BasePanel
{
    [SerializeField] private Image bgImage;
    [SerializeField] private Slider bgSlider;
    [SerializeField] private Slider bkSlider;
    [SerializeField] private Button btnExit;

    public override void Init()
    {
        if (!RequireReferences(bgSlider, bkSlider, btnExit))
            return;
        btnExit.onClick.AddListener(CloseSettings);
        bgSlider.onValueChanged.AddListener(SetMusic);
        bkSlider.onValueChanged.AddListener(SetEffects);
    }

    private void CloseSettings()
    {
        UIManager.Instance.HidePanel<SettingsPanel>();
        UIManager.Instance.ShowPanel<StartPanel>();
    }

    private void SetMusic(float value) => AudioManager.Instance.SaveBgVolume(value);
    private void SetEffects(float value) => AudioManager.Instance.SaveBkVolume(value);

    public override void ShowMe()
    {
        base.ShowMe();
        bgSlider.SetValueWithoutNotify(AudioManager.Instance.GetBgVolume());
        bkSlider.SetValueWithoutNotify(AudioManager.Instance.GetBkVolume());
    }

    public override void HideMe(UnityAction callback)
    {
        AudioManager.Instance.SaveBgVolume(bgSlider.value);
        AudioManager.Instance.SaveBkVolume(bkSlider.value);
        base.HideMe(callback);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        if (btnExit != null)
            btnExit.onClick.RemoveListener(CloseSettings);
        if (bgSlider != null)
            bgSlider.onValueChanged.RemoveListener(SetMusic);
        if (bkSlider != null)
            bkSlider.onValueChanged.RemoveListener(SetEffects);
    }
}
