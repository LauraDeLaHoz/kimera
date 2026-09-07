using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// EnemyCombatVisual3D
// ─────────────────────────────────────────────────────────────────────────────
// Representa a UN enemigo como un GameObject 3D real, colocado a mano en su
// "spot" dentro de la escena de combate.
//
// Reemplaza al sistema viejo de sprites en Canvas:
//   EnemyActionDisplay / EnemyUISpriteAnimator / EnemyDisplayManager
// (esos scripts quedan sin usarse — no se borraron todavía, se limpian
// más adelante una vez migrado todo).
//
// CÓMO USARLO:
//   1. Coloca este componente en el GO raíz del modelo del enemigo (ej. WhiteClown).
//   2. Arrástralo al array "Enemy Views" de CombatUI (campo "visual").
//   3. InSceneCombatController llama a CombatUI.SetupEnemyVisuals() al
//      iniciar cada combate, que a su vez llama a Setup()/Hide() aquí según
//      haya o no un enemigo real para este slot.
//
// Los nombres de trigger por defecto ya coinciden con los estados del
// Animator Controller "EnemyCombate": Idle, Attack1, Prepare, Defend, Hurt, Death.
// Son best-effort: si el Animator todavía no tiene alguno de estos parámetros,
// simplemente no hace nada — no rompe el combate.
// ─────────────────────────────────────────────────────────────────────────────
public class EnemyCombatVisual3D : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Animator del enemigo (Controller 'EnemyCombate').")]
    [SerializeField] private Animator animator;

    [Tooltip("(Opcional, para más adelante) Renderer usado para flash de daño / tintado.")]
    [SerializeField] private Renderer bodyRenderer;

    [Header("Nombres de estados/triggers del Animator")]
    [Tooltip("Estado de reposo.")]
    [SerializeField] private string idleState = "Idle";
    [Tooltip("Trigger del ataque normal del enemigo.")]
    [SerializeField] private string attackTrigger = "Attack1";
    [Tooltip("Trigger de la animación de carga/telegraph (turno de 'Preparando...').")]
    [SerializeField] private string prepareTrigger = "Prepare";
    [Tooltip("Trigger de cuando el enemigo decide defenderse/esquivar su turno.")]
    [SerializeField] private string defendTrigger = "Defend";
    [Tooltip("Trigger al recibir daño sin morir.")]
    [SerializeField] private string hurtTrigger = "Hurt";
    [Tooltip("Trigger al llegar a 0 HP.")]
    [SerializeField] private string deathTrigger = "Death";

    /// <summary>Datos del enemigo actualmente representado por este visual (null si está oculto).</summary>
    public EnemyData Data { get; private set; }

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

        if (bodyRenderer == null)
            bodyRenderer = GetComponentInChildren<Renderer>();
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>Activa este visual y lo prepara para representar a este enemigo.</summary>
    public void Setup(EnemyData data)
    {
        Data = data;
        gameObject.SetActive(true);
        PlayIdle();
    }

    /// <summary>Oculta el visual (slot sin enemigo en este combate — ej. boss fight con 1 solo enemigo).</summary>
    public void Hide()
    {
        Data = null;
        gameObject.SetActive(false);
    }

    public void PlayIdle()
    {
        if (animator == null || string.IsNullOrEmpty(idleState)) return;
        animator.Play(idleState, -1, 0f);
    }

    public void PlayAttack()
    {
        if (HasParam(attackTrigger)) animator.SetTrigger(attackTrigger);
    }

    /// <summary>Turno de "Preparando Carga..." (telegraph antes del golpe fuerte).</summary>
    public void PlayPrepare()
    {
        if (HasParam(prepareTrigger)) animator.SetTrigger(prepareTrigger);
    }

    /// <summary>El enemigo decide defenderse/esquivar en su turno.</summary>
    public void PlayDefend()
    {
        if (HasParam(defendTrigger)) animator.SetTrigger(defendTrigger);
    }

    /// <summary>Recibe daño y sigue con vida.</summary>
    public void PlayHurt()
    {
        if (HasParam(hurtTrigger)) animator.SetTrigger(hurtTrigger);
    }

    public void PlayDeath()
    {
        if (HasParam(deathTrigger)) animator.SetTrigger(deathTrigger);
    }

    // ── Privado ───────────────────────────────────────────────────────────────

    private bool HasParam(string name)
    {
        if (animator == null || string.IsNullOrEmpty(name)) return false;
        foreach (var p in animator.parameters)
            if (p.name == name) return true;
        return false;
    }
}