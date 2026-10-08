using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// InSceneCombatTrigger
// ─────────────────────────────────────────────────────────────────────────────
// Colocar en un GameObject con Collider (isTrigger = true) en la escena de
// exploración. Cuando el jugador entra en el volumen, inicia el combate a
// través de InSceneCombatController (sin cambio de escena).
//
// Uso básico en Inspector:
//   · Enemies          — EnemyData[] que se cargarán en el combate
//   · PrerequisiteFlagId — flag de StoryFlags que debe estar puesto para que el
//                        combate pueda iniciar (ej. 'guia_encontrada'). Vacío =
//                        sin requisito, se activa apenas el jugador entra.
//   · CombatCameraAnchor — (ya no se usa para la cámara; se deja por compatibilidad)
//   · EnemyVisuals     — GOs (sprites/modelos) visibles en exploración;
//                        se desactivan cuando el jugador gana
//   · OneTimeOnly      — si es true, el trigger se desactiva tras la victoria
// ─────────────────────────────────────────────────────────────────────────────
public class InSceneCombatTrigger : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("Combate")]
    [Tooltip("Enemigos que aparecerán en este combate.")]
    [SerializeField] private EnemyData[] enemies;

    [Header("Requisito previo")]
    [Tooltip("Flag de StoryFlags que debe estar puesto para que este combate pueda " +
             "iniciar. Ej: 'guia_encontrada' = el jugador ya terminó la conversación " +
             "con la guía. Mientras no esté puesto, entrar al volumen no hace nada " +
             "(y si el jugador ya estaba parado adentro, el combate arranca solo " +
             "apenas se cumple). Dejalo vacío para no pedir ningún requisito.")]
    [SerializeField] private string prerequisiteFlagId;

    [Header("Cámara")]
    [Tooltip("Transform cuya posición y rotación usará la cámara de combate. " +
             "Si es null se usa la posición actual de la cámara de combate.")]
    [SerializeField] private Transform combatCameraAnchor;

    [Header("Visuales del enemigo en exploración")]
    [Tooltip("GameObjects (sprites, modelos) de los enemigos en el mundo. " +
             "Se ocultan cuando el jugador gana el combate.")]
    [SerializeField] private GameObject[] enemyVisuals;

    [Header("Opciones")]
    [Tooltip("Si es true, el trigger se desactiva definitivamente tras ganar. " +
             "Si es false, el combate puede repetirse cada vez que el jugador entre.")]
    [SerializeField] private bool oneTimeOnly = true;

    // ── Propiedades públicas ───────────────────────────────────────────────────
    /// <summary>Posición / rotación que se asignará a la cámara de combate.</summary>
    public Transform CombatCameraAnchor => combatCameraAnchor;

    /// <summary>True si no hay requisito, o si el flag requerido ya está puesto.</summary>
    public bool PrerequisiteMet =>
        string.IsNullOrEmpty(prerequisiteFlagId) ||
        (StoryFlags.Instance != null && StoryFlags.Instance.IsSet(prerequisiteFlagId));

    // ── Estado ─────────────────────────────────────────────────────────────────
    private bool _triggered = false;
    private bool _warnedNoEnemies = false;
    private bool _waitingForPrerequisite = false;

    // ══════════════════════════════════════════════════════════════════════════
    // Unity – detección de colisión
    // ══════════════════════════════════════════════════════════════════════════

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        // Si entra y todavía no se cumple el requisito, lo anotamos: así, si el
        // requisito se cumple mientras el jugador sigue parado adentro, el
        // combate arranca solo (ver OnTriggerStay).
        if (!PrerequisiteMet)
        {
            _waitingForPrerequisite = true;
            return;
        }

        TryStartCombat(other);
    }

    // Solo actúa en el caso "entré sin cumplir el requisito y ahora ya lo
    // cumplí sin salir del volumen". No interfiere con el resto de los casos
    // (por ejemplo, volver del combate al mismo lugar no re-dispara nada).
    private void OnTriggerStay(Collider other)
    {
        if (!_waitingForPrerequisite) return;
        if (!other.CompareTag("Player")) return;
        if (!PrerequisiteMet) return;

        _waitingForPrerequisite = false;
        TryStartCombat(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            _waitingForPrerequisite = false;
    }

    private void TryStartCombat(Collider other)
    {
        if (_triggered) return;                                    // evitar doble activación
        if (!other.CompareTag("Player")) return;                   // sólo el jugador
        if (!PrerequisiteMet) return;                              // todavía no es el momento
        if (InSceneCombatController.Instance == null) return;      // controlador no existe
        if (InSceneCombatController.Instance.IsInCombat) return;   // ya hay un combate en curso

        if (enemies == null || enemies.Length == 0)
        {
            if (!_warnedNoEnemies)
            {
                Debug.LogWarning($"[InSceneCombatTrigger] '{name}' no tiene enemigos asignados.", this);
                _warnedNoEnemies = true;
            }
            return;
        }

        _triggered = true;
        InSceneCombatController.Instance.StartCombat(enemies, this);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Callbacks desde InSceneCombatController
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Llamado por InSceneCombatController cuando el jugador gana el combate.
    /// Oculta los visuales del enemigo y, si oneTimeOnly, desactiva el trigger.
    /// </summary>
    public void OnCombatWon()
    {
        // Ocultar los visuales del enemigo en el mundo
        if (enemyVisuals != null)
        {
            foreach (var go in enemyVisuals)
                if (go != null) go.SetActive(false);
        }

        if (oneTimeOnly)
        {
            gameObject.SetActive(false);   // never fires again
        }
        else
        {
            _triggered = false;            // permite combates repetidos en el mismo trigger
        }
    }

    /// <summary>
    /// Llamado por InSceneCombatController cuando el jugador abandona el combate.
    /// Re-activa el trigger para que el jugador pueda volver a entrar.
    /// </summary>
    public void OnCombatAbandoned()
    {
        _triggered = false;
    }
}