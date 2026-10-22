using UnityEngine;

public class BaseCounter : KitchenObjectHolder
{
    [SerializeField] private GameObject selectedCounter;

    public virtual void Interact(Player player) { }
    public void SelectCounter()
    {
        if (selectedCounter != null) selectedCounter.SetActive(true);
    }
    public void CancelSelect()
    {
        if (selectedCounter != null) selectedCounter.SetActive(false);
    }
}
