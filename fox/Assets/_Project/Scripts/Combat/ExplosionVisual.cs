using UnityEngine;

public class ExplosionVisual : MonoBehaviour
{
    private static Material sharedMaterial;
    private float remaining = 0.35f;
    private float radius;
    private LineRenderer ring;

    public static void Play(Vector3 position, float radius)
    {
        var root = new GameObject("CabbageExplosion");
        root.name = "CabbageExplosion";
        root.transform.position = position;
        Material material = RunSession.Current != null ? RunSession.Current.Catalog.explosionMaterial : null;
        if (material == null && sharedMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            sharedMaterial = new Material(shader);
            sharedMaterial.color = new Color(0.72f, 0.91f, 0.25f);
        }
        LineRenderer line = root.AddComponent<LineRenderer>();
        line.sharedMaterial = material != null ? material : sharedMaterial;
        line.loop = true;
        line.useWorldSpace = false;
        line.positionCount = 48;
        line.widthMultiplier = 0.12f;
        ExplosionVisual visual = root.AddComponent<ExplosionVisual>();
        visual.radius = radius;
        visual.ring = line;
        ParticleSystem particles = root.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = 0.3f;
        main.startSpeed = radius * 4f;
        main.startSize = 0.10f;
        main.startColor = new Color(0.82f, 0.95f, 0.35f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 28;
        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = line.sharedMaterial;
        particles.Play();
    }

    private void Update()
    {
        remaining -= Time.deltaTime;
        float expansion = radius * (1f - Mathf.Clamp01(remaining / 0.35f));
        for (int index = 0; index < ring.positionCount; index++)
        {
            float angle = index * Mathf.PI * 2f / ring.positionCount;
            ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * expansion, 0.08f, Mathf.Sin(angle) * expansion));
        }
        if (remaining <= 0f) Destroy(gameObject);
    }
}
