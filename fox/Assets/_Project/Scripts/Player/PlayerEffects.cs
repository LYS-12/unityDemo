using System.Collections.Generic;
using UnityEngine;

public class PlayerEffects : MonoBehaviour
{
    private readonly List<GameObject> shields = new List<GameObject>();
    private RunSession session;
    private float angle;

    private void Awake() => session = RunSession.Current;
    private void OnEnable() { if (session != null) session.Changed += RefreshShields; }
    private void Start() => RefreshShields();
    private void OnDisable() { if (session != null) session.Changed -= RefreshShields; }

    private void RefreshShields()
    {
        if (session == null) return;
        while (shields.Count > session.ShieldCharges)
        {
            int index = shields.Count - 1;
            Destroy(shields[index]);
            shields.RemoveAt(index);
        }
        if (session.ShieldDefinition == null) return;
        while (shields.Count < session.ShieldCharges)
        {
            GameObject shield = ItemView.CreateVisual(session.ShieldDefinition, transform);
            shield.name = "CheeseShield";
            shield.transform.localScale *= 0.5f;
            shields.Add(shield);
        }
    }

    private void Update()
    {
        if (session == null || GameplayPause.IsPaused || SceneLoader.IsLoading) return;
        session.Tick(Time.deltaTime);
        if (session.ShieldDefinition == null) return;
        angle += session.ShieldDefinition.shieldOrbitSpeed * Time.deltaTime;
        for (int index = 0; index < shields.Count; index++)
        {
            float radians = (angle + index * 360f / shields.Count) * Mathf.Deg2Rad;
            shields[index].transform.position = transform.position + Vector3.up * 1.1f +
                new Vector3(Mathf.Cos(radians), 0.15f * Mathf.Sin(radians * 2f), Mathf.Sin(radians)) * session.ShieldDefinition.shieldRadius;
            shields[index].transform.Rotate(Vector3.up, 70f * Time.deltaTime);
        }
    }
}
