using UnityEngine;

public static class Ballistics
{
    public static Vector3 LaunchVelocity(Vector3 origin, Vector3 target, Vector3 fallbackForward, float speed, float range)
    {
        Vector3 flat = target - origin;
        flat.y = 0f;
        float distance = Mathf.Clamp(flat.magnitude, 1f, Mathf.Max(1f, range));
        Vector3 direction = flat.sqrMagnitude > 0.001f ? flat.normalized : fallbackForward.normalized;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        float time = distance / Mathf.Max(0.1f, speed);
        float vertical = (target.y - origin.y - 0.5f * Physics.gravity.y * time * time) / time;
        return direction * Mathf.Max(0.1f, speed) + Vector3.up * vertical;
    }
}
