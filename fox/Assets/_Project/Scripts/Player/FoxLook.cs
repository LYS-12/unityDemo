using UnityEngine;

public class FoxLook : MonoBehaviour
{
    [SerializeField] private Transform foxHead;
    [SerializeField] private Transform lookAtTarget;

    private void Update()
    {
        if (foxHead != null && lookAtTarget != null) foxHead.LookAt(lookAtTarget);
    }
}
