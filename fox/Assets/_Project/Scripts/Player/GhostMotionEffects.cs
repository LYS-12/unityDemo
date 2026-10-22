using UnityEngine;

public class GhostMotionEffects : MonoBehaviour
{
    public Material sparkleMaterial;
    public Material smokeMaterial;
    public Mesh smokeMesh;
    [Min(0)] public float smokePerMetre = 5;
    [Min(1)] public int maxSmoke = 45;
    [Min(.1f)] public float smokeLifetime = .85f;
    [Min(.01f)] public float smokeSize = .6f;
    public Color smokeColor = new Color(.90f, .85f, 1, .43f);
    [Min(.1f)] public float ghostHeight = 2.15f;
    private Player player;
    private ParticleSystem smoke, sparkles;
    private Renderer[] renderers;
    private MaterialPropertyBlock pearlProperties;
    private void Start()
    {
        player = GetComponentInParent<Player>();
        renderers = GetComponentsInChildren<Renderer>();
        pearlProperties = new MaterialPropertyBlock();
        smoke = Create("CurlingMovementSmoke", smokeMaterial);
        var main = smoke.main;
        main.startLifetime = smokeLifetime;
        main.startSize = smokeSize;
        main.startSpeed = .15f;
        main.startColor = smokeColor;
        main.maxParticles = maxSmoke;
        var shape = smoke.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = .22f;
        shape.rotation = new Vector3(90, 0, 0);
        var render = smoke.GetComponent<ParticleSystemRenderer>();
        render.renderMode = ParticleSystemRenderMode.Mesh;
        render.mesh = smokeMesh;
        render.alignment = ParticleSystemRenderSpace.World;
        smoke.transform.localPosition = new Vector3(0, .18f, -.2f);
        var rotation = smoke.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = .7f;
        var color = smoke.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(smokeColor, 0), new GradientColorKey(smokeColor, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
        color.color = gradient;
        var size = smoke.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .4f, 1, 1.7f));
        sparkles = Create("LavenderOrbit", sparkleMaterial);
        main = sparkles.main;
        main.startLifetime = 2.8f;
        main.startSize = .045f;
        main.startSpeed = .1f;
        main.startColor = new Color(.88f, .75f, 1, .65f);
        main.maxParticles = 22;
        shape = sparkles.shape;
        shape.shapeType = ParticleSystemShapeType.Donut;
        shape.radius = .95f;
        shape.donutRadius = .05f;
        shape.rotation = new Vector3(90, 0, 0);
        sparkles.transform.localPosition = Vector3.up * 1.2f;
        var emission = sparkles.emission;
        emission.rateOverTime = 6;
        var velocity = sparkles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.orbitalY = .8f;
        velocity.y = .13f;
        color = sparkles.colorOverLifetime;
        color.enabled = true;
        color.color = gradient;
        smoke.Play();
        sparkles.Play();
    }
    private ParticleSystem Create(string label, Material material)
    {
        var node = new GameObject(label);
        node.transform.SetParent(transform, false);
        node.layer = LayerMask.NameToLayer("DynamicFX");
        var system = node.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        var emission = system.emission;
        emission.rateOverTime = 0;
        node.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        return system;
    }
    private void Update()
    {
        if (smoke == null)
            return;
        var main = smoke.main;
        main.useUnscaledTime = StoryCinematic.IsPlaying;
        main = sparkles.main;
        main.useUnscaledTime = StoryCinematic.IsPlaying;
        var emission = smoke.emission;
        emission.rateOverDistance = player != null && player.IsWalking && !GameplayPause.IsPaused ? smokePerMetre : 0;
    }
    private void LateUpdate()
    {
        if (renderers == null)
            return;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.HasProperty("_GhostBaseY"))
                continue;
            renderer.GetPropertyBlock(pearlProperties);
            pearlProperties.SetFloat("_GhostBaseY", transform.position.y);
            pearlProperties.SetFloat("_GhostHeight", ghostHeight);
            renderer.SetPropertyBlock(pearlProperties);
        }
    }
}
