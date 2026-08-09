using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// EnemyWorldParticles
// ─────────────────────────────────────────────────────────────────────────────
// Añade partículas rojas a cualquier GO de enemigo en el mundo de exploración.
// Coloca este componente en el GO del enemigo (o en sus GOs de "Enemy Visuals"
// dentro del InSceneCombatTrigger).
//
// Las partículas se crean automáticamente en Start() como un hijo del GO.
// No requiere ninguna configuración adicional, funciona out-of-the-box.
// Puedes ajustar los parámetros visuales desde el Inspector.
// ─────────────────────────────────────────────────────────────────────────────
public class EnemyWorldParticles : MonoBehaviour
{
    [Header("Partículas")]
    [Tooltip("Partículas por segundo")]
    [Range(5f, 60f)]
    public float emissionRate = 18f;

    [Tooltip("Tamaño mínimo de cada partícula")]
    public float sizeMin = 0.04f;
    [Tooltip("Tamaño máximo de cada partícula")]
    public float sizeMax = 0.14f;

    [Tooltip("Velocidad mínima (hacia arriba)")]
    public float speedMin = 0.4f;
    [Tooltip("Velocidad máxima (hacia arriba)")]
    public float speedMax = 1.2f;

    [Tooltip("Tiempo de vida mínimo de cada partícula")]
    public float lifetimeMin = 0.6f;
    [Tooltip("Tiempo de vida máximo de cada partícula")]
    public float lifetimeMax = 1.4f;

    [Tooltip("Radio de la esfera desde la que emiten las partículas")]
    public float emitRadius = 0.35f;

    [Tooltip("Offset vertical del sistema de partículas sobre el GO")]
    public float heightOffset = 1.2f;

    // ─────────────────────────────────────────────────────────────────────────

    private ParticleSystem _ps;

    private void Start()
    {
        BuildParticleSystem();
    }

    private void BuildParticleSystem()
    {
        // Crear GO hijo
        var psGO = new GameObject("EnemyParticles");
        psGO.transform.SetParent(transform);
        psGO.transform.localPosition = new Vector3(0f, heightOffset, 0f);

        _ps = psGO.AddComponent<ParticleSystem>();

        // ── Main ──────────────────────────────────────────────────────────────
        var main = _ps.main;
        main.loop            = true;
        main.playOnAwake     = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(lifetimeMin, lifetimeMax);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
        main.startSize       = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0f, 0f, 1f),
            new Color(1f, 0.25f, 0.1f, 0.85f));
        main.gravityModifier = -0.25f;   // flotan levemente hacia arriba

        // ── Emission ──────────────────────────────────────────────────────────
        var emission = _ps.emission;
        emission.rateOverTime = emissionRate;

        // ── Shape: esfera ─────────────────────────────────────────────────────
        var shape = _ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = emitRadius;

        // ── Color Over Lifetime: fade out ─────────────────────────────────────
        var col = _ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(new Color(1f, 0.1f, 0.1f), 0f),
                new GradientColorKey(new Color(0.8f, 0f, 0f), 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ── Size Over Lifetime: se achican al morir ───────────────────────────
        var sizeOverLife = _ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // ── Renderer ──────────────────────────────────────────────────────────
        var rend = psGO.GetComponent<ParticleSystemRenderer>();
        rend.renderMode   = ParticleSystemRenderMode.Billboard;
        rend.sortingOrder = 2;

        _ps.Play();
    }

    // Llamar desde código si quieres pausar/reanudar las partículas
    public void SetActive(bool active)
    {
        if (_ps == null) return;
        if (active) _ps.Play();
        else        _ps.Stop();
    }
}
