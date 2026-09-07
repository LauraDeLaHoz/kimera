using System.Collections;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// PlayerCombatAnimator  (v2 — migración 3D, sin swap de sprites)
// ─────────────────────────────────────────────────────────────────────────────
// Coloca este componente en el GO raíz del Player (el mismo objeto en
// exploración y en combate — YA NO existe un "sprite de combate" separado).
//
// CÓMO FUNCIONA AHORA:
//   Exploración → el/los scripts de movimiento controlan el Animator normalmente.
//   Combate     → se desactivan los scripts de movimiento/dirección Y ADEMÁS
//                 se fuerza el Animator del propio player a "Idle 0" y se
//                 mantiene ahí (congelando el parámetro de velocidad) mientras
//                 dure el combate. El mismo GameObject nunca se desactiva ni
//                 se reemplaza por otro.
//
// CONFIGURACIÓN EN EL INSPECTOR:
//   1. Player Animator   → arrastra el Animator real del player (el del
//                           Animator Controller "Player3D", el que ves en
//                           Window > Animation > Animator con los estados
//                           "Idle 0" / "Run_N"). Si lo dejas vacío, el script
//                           busca uno con GetComponentInChildren.
//   2. Direction Script  → (opcional) Alpha_2D_Character_In_3D_World del jugador.
//                          Si está vacío el script busca y desactiva TODOS
//                          los del prefab.
//   3. Speed Param Name  → nombre del parámetro float de velocidad en el
//                          Animator Controller (por defecto "speed").
//
// NOTA DE MIGRACIÓN: los campos combatSpriteGO / explorationSpriteGO del
// sistema viejo (sprite 2D en Canvas) se eliminaron. Si tu escena todavía
// tiene esos GOs, ya no hace falta — el player 3D se queda quieto en su
// sitio, es el mismo objeto en exploración y en combate.
// ─────────────────────────────────────────────────────────────────────────────
[DefaultExecutionOrder(-80)]
public class PlayerCombatAnimator : MonoBehaviour
{
    [Header("Animator del player (3D)")]
    [Tooltip("Animator real del player (Controller 'Player3D'). " +
             "Si lo dejas vacío se busca automáticamente en hijos.")]
    [SerializeField] private Animator playerAnimator;

    [Header("Script de dirección")]
    [Tooltip("(Opcional) referencia explícita a Alpha_2D_Character_In_3D_World.\n" +
             "Si está vacío el script busca y desactiva TODOS los del prefab.")]
    [SerializeField] private Alpha_2D_Character_In_3D_World directionScript;

    [Header("Animación en combate")]
    [Tooltip("Nombre del estado de Idle en el Animator Controller del player.")]
    [SerializeField] private string combatIdleState = "Idle 0";

    [Tooltip("Nombre del parámetro float de velocidad usado para Idle/Run.")]
    [SerializeField] private string speedParamName = "speed";

    // ── Cache ────────────────────────────────────────────────────────────────

    private Alpha_2D_Character_In_3D_World[] _allDirScripts;
    private bool _inCombat = false;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Cachear todos los scripts Alpha_2D del prefab (raíz + hijos, activos e inactivos).
        _allDirScripts = GetComponentsInChildren<Alpha_2D_Character_In_3D_World>(true);

        if (playerAnimator == null)
            playerAnimator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);
    }

    private void Start()
    {
        if (playerAnimator == null)
            Debug.LogWarning("[PlayerCombatAnimator] 'Player Animator' no asignado. " +
                             "Arrastra el Animator del player (Controller 'Player3D') al Inspector.");
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>Llamar al entrar en combate. El player se queda quieto en Idle, en el mismo sitio.</summary>
    public void EnterCombat()
    {
        StopAllCoroutines();
        _inCombat = true;

        // 1. Cortar cualquier script que pueda seguir escribiendo el parámetro de velocidad
        SetDirectionScripts(false);

        // 2. Forzar Idle en el Animator real del player (ya no en un sprite aparte)
        ForceIdle();
    }

    /// <summary>Llamar al salir del combate. Reactiva el control normal del movimiento.</summary>
    public void ExitCombat()
    {
        StopAllCoroutines();
        _inCombat = false;

        // No forzamos ningún estado al salir: dejamos que el script de
        // movimiento retome el control del Animator con el input real.
        SetDirectionScripts(true);
    }

    // ── Privado ───────────────────────────────────────────────────────────────

    private void ForceIdle()
    {
        if (playerAnimator == null) return;

        if (HasParam(playerAnimator, speedParamName))
            playerAnimator.SetFloat(speedParamName, 0f);

        if (!string.IsNullOrEmpty(combatIdleState))
        {
            playerAnimator.Play(combatIdleState, -1, 0f);
            playerAnimator.Update(0f);
        }

        Debug.Log($"[PlayerCombatAnimator] Combate → Idle forzado en '{playerAnimator.gameObject.name}'");
    }

    /// <summary>Activa o desactiva todos los Alpha_2D_Character_In_3D_World encontrados en el prefab.</summary>
    private void SetDirectionScripts(bool active)
    {
        if (_allDirScripts != null)
            foreach (var ds in _allDirScripts)
                if (ds != null) ds.enabled = active;

        // También el campo explícito por si quedó fuera del cache
        if (directionScript != null) directionScript.enabled = active;
    }

    // Mientras dure el combate, re-congelamos el parámetro cada frame por si
    // algún otro script (físicas, root motion, etc.) lo vuelve a mover.
    private void LateUpdate()
    {
        if (!_inCombat || playerAnimator == null) return;

        if (HasParam(playerAnimator, speedParamName))
            playerAnimator.SetFloat(speedParamName, 0f);
    }

    private bool HasParam(Animator anim, string name)
    {
        foreach (var p in anim.parameters)
        {
            if (p.name == name)
                return true;
        }

        return false;
    }
}

