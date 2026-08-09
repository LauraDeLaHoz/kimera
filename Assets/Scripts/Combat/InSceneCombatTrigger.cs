using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// InSceneCombatTrigger
// ─────────────────────────────────────────────────────────────────────────────
// Colocar en un GameObject con Collider (isTrigger = true) en la escena de
// exploración. Cuando el jugador entra en el volumen, inicia el combate a
// través de InSceneCombatController (sin cambio de escena).
//
// Uso básico en Inspector:
//   · Enemies         — EnemyData[] que se cargarán en el combate
//   · CombatCameraAnchor — Transform cuya posición/rotación usará la cámara
//   · EnemyVisuals    — GOs (sprites/modelos) visibles en exploración;
//                       se desactivan cuando el jugador gana
//   · OneTimeOnly     — si es true, el trigger se desactiva tras la victoria
// ─────────────────────────────────────────────────────────────────────────────
public class InSceneCombatTrigger : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────
    [Header("Combate")]
    [Tooltip("Enemigos que aparecerán en este combate.")]
    [SerializeField] private EnemyData[] enemies;

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

    // ── Propiedad pública ──────────────────────────────────────────────────────
    /// <summary>Posición / rotación que se asignará a la cámara de combate.</summary>
    public Transform CombatCameraAnchor => combatCameraAnchor;

    // ── Estado ─────────────────────────────────────────────────────────────────
    private bool _triggered = false;

    // ══════════════════════════════════════════════════════════════════════════
    // Unity – detección de colisión
    // ══════════════════════════════════════════════════════════════════════════

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;                                    // evitar doble activación
        if (!other.CompareTag("Player")) return;                   // sólo el jugador
        if (InSceneCombatController.Instance == null) return;      // controlador no existe
        if (enemies == null || enemies.Length == 0)
        {
            Debug.LogWarning($"[InSceneCombatTrigger] '{name}' no tiene enemigos asignados.", this);
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
