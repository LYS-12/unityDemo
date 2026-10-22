using UnityEngine;
using UnityEngine.Serialization;

public class CameraMove : MonoBehaviour
{
    [SerializeField] private Transform targetPlayer;
    [FormerlySerializedAs("H"), SerializeField] private float height = 17f;

    private void LateUpdate()
    {
        if (targetPlayer == null) return;
        Vector3 position = targetPlayer.position;
        position.y = height;
        transform.position = position;
    }
}
