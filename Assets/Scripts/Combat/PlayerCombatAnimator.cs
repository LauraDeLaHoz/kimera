using System.Collections;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// PlayerCombatAnimator
// ─────────────────────────────────────────────────────────────────────────────
// Coloca este componente en el GO raíz del Player.
//
// CÓMO FUNCIONA:
//   Exploración  →  "Combat Sprite GO" inactivo    │  "Exploration Sprite GO" activo
//   Combate      →  "Combat Sprite GO" activo       │  "Exploration Sprite GO" inactivo
//
// CONFIGURACIÓN EN EL INSPECTOR:
//   1. Combat Sprite GO     → arrastra el hijo "sprite de combate"
//   2. Exploration Sprite GO → arrastra el GO del sprite de exploración
//      (si está vacío, solo se gestiona el combat GO)
//   3. Direction Script     → (opcional) Alpha_2D_Character_In_3D_World del jugador.
//      Si está vacío el script igualmente busca y desactiva todos los del prefab.
//
// El "sprite de combate" debe estar INACTIVO en la escena antes de darle a Play.
// El script garantiza ese estado inicial en Start().
// ─────────────────────────────────────────────────────────────────────────────
[DefaultExecutionOrder(-80)]
public class PlayerCombatAnimator : MonoBehaviour
{
    [Header("GameObjects de sprite")]
    [Tooltip("El hijo 'sprite de combate'.\n" +
             "Se ACTIVA al entrar en combate y se DESACTIVA al salir.\n" +
             "Debe estar inactivo en la escena antes de darle a Play.")]
    [SerializeField] private GameObject combatSpriteGO;

    [Tooltip("(Opcional) GO del sprite de exploración (el sprite normal del jugador).\n" +
             "Se DESACTIVA al entrar en combate y se ACTIVA al salir.\n" +
             "Si está vacío solo se gestiona 'Combat Sprite GO'.")]
    [SerializeField] private GameObject explorationSpriteGO;

    [Header("Script de dirección")]
    [Tooltip("(Opcional) referencia explícita a Alpha_2D_Character_In_3D_World.\n" +
             "Si está vacío el script busca y desactiva TODOS los del prefab.")]
    [SerializeField] private Alpha_2D_Character_In_3D_World directionScript;

    [Header("Animación del sprite de combate (opcional)")]
    [Tooltip("Si 'Combat Sprite GO' tiene un Animator, fuerza este estado al activarlo.\n" +
             "Déjalo vacío si el estado por defecto del Animator Controller ya es correcto\n" +
             "o si el sprite de combate no tiene Animator (solo SpriteRenderer estático).")]
    [SerializeField] private string combatIdleState = "AnimacionCombate1";

    // ── Cache ────────────────────────────────────────────────────────────────

    private Alpha_2D_Character_In_3D_World[] _allDirScripts;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Cachear todos los scripts Alpha_2D del prefab (raíz + hijos, activos e inactivos).
        _allDirScripts = GetComponentsInChildren<Alpha_2D_Character_In_3D_World>(true);
    }

    private void Start()
    {
        // Garantizar estado inicial correcto al arrancar el juego:
        // combat sprite INACTIVO, exploration sprite ACTIVO.
        ApplyExplorationState();

        if (combatSpriteGO == null)
            Debug.LogWarning("[PlayerCombatAnimator] 'Combat Sprite GO' no asignado. " +
                             "Arrastra el hijo 'sprite de combate' al Inspector de PlayerCombatAnimator.");
    }

    // ── API pública ───────────────────────────────────────────────────────────

    /// <summary>Llamar al entrar en combate. Activa el sprite de combate y oculta el de exploración.</summary>
    public void EnterCombat()
    {
        StopAllCoroutines();

        // 1. Desactivar script de dirección para que no interfiera
        SetDirectionScripts(false);

        // 2. Ocultar sprite de exploración
        if (explorationSpriteGO != null)
            explorationSpriteGO.SetActive(false);

        // 3. Mostrar sprite de combate
        if (combatSpriteGO != null)
        {
            combatSpriteGO.SetActive(true);

            // Si tiene Animator y hay un estado configurado, forzarlo en el siguiente frame.
            if (!string.IsNullOrEmpty(combatIdleState))
            {
                var anim = combatSpriteGO.GetComponent<Animator>()
                        ?? combatSpriteGO.GetComponentInChildren<Animator>();
                if (anim != null)
                    StartCoroutine(ForcePlayState(anim, combatIdleState));
            }
        }
        else
        {
            Debug.LogWarning("[PlayerCombatAnimator] 'Combat Sprite GO' no asignado — " +
                             "arrastra el hijo 'sprite de combate' al Inspector.");
        }
    }

    /// <summary>Llamar al salir del combate. Restaura el sprite de exploración y oculta el de combate.</summary>
    public void ExitCombat()
    {
        StopAllCoroutines();
        ApplyExplorationState();
    }

    // ── Privado ───────────────────────────────────────────────────────────────

    /// <summary>Aplica el estado de exploración: combat GO inactivo, exploration GO activo, dirección activa.</summary>
    private void ApplyExplorationState()
    {
        if (combatSpriteGO      != null) combatSpriteGO.SetActive(false);
        if (explorationSpriteGO != null) explorationSpriteGO.SetActive(true);
        SetDirectionScripts(true);
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

    /// <summary>Espera un frame y fuerza un estado del Animator directamente por nombre.</summary>
    private IEnumerator ForcePlayState(Animator anim, string stateName)
    {
        // El Animator necesita un frame para inicializarse después de que su GO se active.
        yield return null;

        if (anim == null || !anim.isActiveAndEnabled) yield break;

        anim.Play(stateName, -1, 0f);
        anim.Update(0f);
        Debug.Log($"[PlayerCombatAnimator] ✓ Play('{stateName}') en '{anim.gameObject.name}'");
    }
}
