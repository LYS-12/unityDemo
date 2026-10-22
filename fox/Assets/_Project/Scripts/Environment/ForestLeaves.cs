using UnityEngine;

/// <summary>Authored local leaf zones. Ground litter is a static combined mesh with no physics.</summary>
public class ForestLeaves : MonoBehaviour
{
    public Mesh leafMesh;
    public Material leafMaterial;
    public Vector3[] canopyPositions;
    [Min(0)] public float leavesPerSecond = .35f;
    [Min(.1f)] public float lifetime = 8;
    private bool cinematicTime;
    private void Start()
    {
        cinematicTime = StoryCinematic.IsPlaying;
        if (canopyPositions == null)
            return;
        for (int i = 0; i < canopyPositions.Length; i++)
        {
            var node = new GameObject("FallingLeaves_" + i);
            node.transform.SetParent(transform, false);
            node.transform.position = canopyPositions[i];
            node.layer = LayerMask.NameToLayer("DynamicFX");
            var particles = node.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.startLifetime = lifetime;
            main.startSize = new ParticleSystem.MinMaxCurve(.10f, .23f);
            main.startSpeed = 0;
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = cinematicTime;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0, 6);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0, 6);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.84f, .65f, .32f), new Color(.58f, .72f, .4f));
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(4, 1, 4);
            var emission = particles.emission;
            emission.rateOverTime = leavesPerSecond;
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-.65f, -.65f);
            velocity.x = new ParticleSystem.MinMaxCurve(-.3f, .35f);
            velocity.z = new ParticleSystem.MinMaxCurve(.17f, .17f);
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = .8f;
            rotation.y = .7f;
            rotation.z = .5f;
            var render = node.GetComponent<ParticleSystemRenderer>();
            render.renderMode = ParticleSystemRenderMode.Mesh;
            render.mesh = leafMesh;
            render.sharedMaterial = leafMaterial;
            particles.Play();
        }
    }
    private void Update()
    {
        if (cinematicTime == StoryCinematic.IsPlaying)
            return;
        cinematicTime = StoryCinematic.IsPlaying;
        foreach (var particles in GetComponentsInChildren<ParticleSystem>())
        {
            var main = particles.main;
            main.useUnscaledTime = cinematicTime;
        }
    }
}
