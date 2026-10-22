using System.Collections;
using UnityEngine;

public class MainMenuBootstrap : MonoBehaviour
{
    private IEnumerator Start()
    {
        PartyAudio.Ensure();
        if (UIManager.Instance.GetPanel<StartPanel>() != null)
            yield break;
        yield return null;
        if (UIManager.Instance.GetPanel<StartPanel>() == null)
            UIManager.Instance.ShowPanel<StartPanel>();
    }
}
