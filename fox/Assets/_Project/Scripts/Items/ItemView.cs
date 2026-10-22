using UnityEngine;

/// <summary>切换道具代理的碰撞，不改变模型或共享材质资产。</summary>
public class ItemView : MonoBehaviour
{
    private Collider[] colliders;
    private bool[] enabledStates;

    public void SetHeld(bool held)
    {
        if (colliders == null)
        {
            colliders = GetComponentsInChildren<Collider>(true);
            enabledStates = new bool[colliders.Length];
            for (int index = 0; index < colliders.Length; index++)
                enabledStates[index] = colliders[index].enabled;
        }
        for (int index = 0; index < colliders.Length; index++)
            if (colliders[index] != null)
                colliders[index].enabled = !held && enabledStates[index];
        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = !held;
        }
    }

    public static GameObject CreateVisual(KitchenObjectSO definition, Transform parent)
    {
        GameObject view = Instantiate(definition.Prefab, parent, false);
        foreach (Transform child in view.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = LayerMask.NameToLayer("WorldItem");
        foreach (MonoBehaviour behaviour in view.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        ItemView state = view.GetComponent<ItemView>();
        if (state == null)
            state = view.AddComponent<ItemView>();
        state.SetHeld(true);
        return view;
    }
}
