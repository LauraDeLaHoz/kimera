using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
// InSceneCombatController
// ─────────────────────────────────────────────────────────────────────────────
// Orquestador de combate en la misma escena (sin cambio de escena).
// Colocar en un GO "_CombatController" dentro de la escena principal.
//
// Flujo:
//   InSceneCombatTrigger.OnTriggerEnter
//     → StartCombat(enemies, trigger)
//       → fade negro → swap cámara → mostrar Combat UI → InitializeCombat
//   Al ganar/perder:
//     PostCombatFlow muestra el panel de resultado.
//     El jugador elige Reintentar / Abandonar / Continuar al boss.
// ─────────────────────────────────────────────────────────────────────────────
[DefaultExecutionOrder(-90)]   // después de CombatManager(-100) pero antes de CombatUI(-50)
public class InSceneCombatController : MonoBehaviour
{
    public static InSceneCombatController Instance { get; private set; }

    // ── Jugador ────────────────────────────────────────────────────────────────
    [Header("Jugador")]
    [SerializeField] private CharacterStats     playerStats;
    [SerializeField] private List<ItemData>     startingItems;
    [SerializeField] private Slider             hungerBarInCombat;

    [Tooltip("Scripts de movimiento/control que se desactivan al entrar en combate.")]
    [SerializeField] private MonoBehaviour[]    playerMovementScripts;
    [Tooltip("Gestor de animaciones de combate (PlayerCombatAnimator en el Player GO)")]
    [SerializeField] private PlayerCombatAnimator playerCombatAnimator;

    // ── Cámaras ────────────────────────────────────────────────────────────────
    [Header("Cámaras")]
    [SerializeField] private Camera explorationCamera;
    [SerializeField] private Camera combatCamera;

    // ── Objetos del mundo a ocultar ────────────────────────────────────────────
    [Header("Objetos del mundo")]
    [Tooltip("GOs que se desactivan al entrar en combate y se reactivan al salir.\n" +
             "Arrastra aquí el NPC Spawner, decorados, etc.")]
    [SerializeField] private GameObject[] worldObjectsToHide;

    // ── Combat UI ──────────────────────────────────────────────────────────────
    [Header("UI de Combate")]
    [SerializeField] private Canvas      combatCanvas;
    [SerializeField] private CombatUI    combatUI;
    [SerializeField] private CanvasGroup combatCanvasGroup;  // raíz del Canvas

    // ── Transición ─────────────────────────────────────────────────────────────
    [Header("Transición")]
    [Tooltip("Imagen negra a pantalla completa (en un Canvas de orden superior) para el fade.")]
    [SerializeField] private Image  screenFadeImage;
    [SerializeField] private float  fadeDuration = 0.35f;

    // ── Estado ─────────────────────────────────────────────────────────────────
    private EnemyData[]          _currentEnemies;
    private InSceneCombatTrigger _currentTrigger;

    public bool       IsInCombat     { get; private set; }
    public EnemyData[] CurrentEnemies => _currentEnemies;

    // ══════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // El Canvas de combate arranca invisible pero activo
        // (para que todos los MonoBehaviours puedan ejecutar Awake/Start)
        ApplyCombatUIVisibility(false);

        if (combatCamera != null) combatCamera.enabled = false;
        if (screenFadeImage != null) screenFadeImage.gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // API pública
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Iniciar un encuentro de combate desde un trigger del mundo.</summary>
    public void StartCombat(EnemyData[] enemies, InSceneCombatTrigger trigger)
    {
        if (IsInCombat) return;
        IsInCombat      = true;
        _currentEnemies = enemies;
        _currentTrigger = trigger;
        StartCoroutine(TransitionIntoCombat(enemies, trigger != null ? trigger.CombatCameraAnchor : null));
    }

    /// <summary>Reintentar el combate actual (llamado desde PostCombatFlow al perder).</summary>
    public void RetryCombat()
    {
        IsInCombat = true;
        StartCoroutine(TransitionIntoCombat(
            _currentEnemies,
            _currentTrigger != null ? _currentTrigger.CombatCameraAnchor : null));
    }

    /// <summary>Abandonar el combate y volver a la exploración.</summary>
    public void AbandonCombat()
    {
        StartCoroutine(TransitionOutOfCombat());
        _currentTrigger?.OnCombatAbandoned();
    }

    /// <summary>
    /// Salir del modo combate con transición y, opcionalmente, cargar otra escena.
    /// Llamado desde PostCombatFlow cuando el jugador hace clic en "Volver al inicio"
    /// después de ganar/perder contra el boss.
    /// </summary>
    public void ReturnToExploration(string sceneToLoad = null)
    {
        StartCoroutine(TransitionOutThenLoad(sceneToLoad));
    }

    private IEnumerator TransitionOutThenLoad(string sceneToLoad)
    {
        yield return StartCoroutine(TransitionOutOfCombat());
        if (!string.IsNullOrEmpty(sceneToLoad))
            SceneManager.LoadScene(sceneToLoad);
    }

    /// <summary>Iniciar el combate contra el boss (llamado por PostCombatFlow).</summary>
    public void StartBossFight(EnemyData bossEnemy)
    {
        _currentEnemies = new EnemyData[] { bossEnemy };
        IsInCombat = true;
        StartCoroutine(TransitionIntoCombat(_currentEnemies, null));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Transiciones
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator TransitionIntoCombat(EnemyData[] enemies, Transform cameraAnchor)
    {
        // 1 — Fade a negro
        yield return StartCoroutine(FadeScreen(0f, 1f, fadeDuration));

        // 2 — Ocultar objetos del mundo (NPCs, decorados, etc.)
        SetWorldObjects(false);

        // 3 — Desactivar movimiento del jugador + iniciar animación de entrada
        SetPlayerMovement(false);
        if (playerCombatAnimator != null)
            playerCombatAnimator.EnterCombat();
        else
            Debug.LogWarning("[InSceneCombatController] ⚠️ playerCombatAnimator es null — " +
                             "ejecuta Kimera/12 para configurarlo automáticamente.");
        SoundManager.Instance?.PlayCombatMusic();

        // 3 — Swap cámara
        if (cameraAnchor != null && combatCamera != null)
            combatCamera.transform.SetPositionAndRotation(
                cameraAnchor.position, cameraAnchor.rotation);
        if (explorationCamera != null) explorationCamera.enabled = false;
        if (combatCamera      != null) combatCamera.enabled      = true;

        // 4 — Mostrar UI de combate
        ApplyCombatUIVisibility(true);

        // 5 — Inicializar combate
        InitializeCombatLogic(enemies);

        // 6 — Fade desde negro
        yield return StartCoroutine(FadeScreen(1f, 0f, fadeDuration));
    }

    private IEnumerator TransitionOutOfCombat()
    {
        yield return StartCoroutine(FadeScreen(0f, 1f, fadeDuration));

        // Ocultar UI de combate
        ApplyCombatUIVisibility(false);
        SoundManager.Instance?.PlayExplorationMusic();

        // Swap cámara de vuelta
        if (combatCamera      != null) combatCamera.enabled      = false;
        if (explorationCamera != null) explorationCamera.enabled = true;

        // Restaurar objetos del mundo (NPCs, decorados, etc.)
        SetWorldObjects(true);

        // Re-activar movimiento + volver a animación de exploración
        playerCombatAnimator?.ExitCombat();
        SetPlayerMovement(true);

        // Informar al HungerSystem
        HungerSystem.Instance?.ExitCombat();

        IsInCombat = false;

        yield return StartCoroutine(FadeScreen(1f, 0f, fadeDuration));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Inicialización de combate
    // ══════════════════════════════════════════════════════════════════════════

    private void InitializeCombatLogic(EnemyData[] enemies)
    {
        // Resetear la UI para que no queden pantallas de victoria/derrota anteriores
        combatUI?.ResetForNewCombat();

        // Inicializar el CombatManager
        CombatManager.Instance.InitializeCombat(playerStats, enemies.ToList());
        PlayerCombatant player = CombatManager.Instance.Player;

        // Aplicar bonus de nivel (combate de boss)
        if (LevelUpData.PendingLevelUp)
        {
            player.ApplyLevelUpBonus(
                LevelUpData.BonusMaxHP,
                LevelUpData.BonusAttack,
                LevelUpData.BonusMaxEnergy);
            LevelUpData.PendingLevelUp = false;
        }

        // Aplicar bonus de comida (pantalla de La Guía)
        if (LevelUpData.MealHPRestore > 0 || LevelUpData.MealHungerRestore > 0)
        {
            HungerSystem.Instance?.Eat(LevelUpData.MealHungerRestore);
            player.HungerPercent = HungerSystem.Instance != null
                ? HungerSystem.Instance.HungerPercent : 1f;
            player.Heal(LevelUpData.MealHPRestore);
            if (LevelUpData.MealEnergyRestore > 0)
                player.RecoverEnergy(LevelUpData.MealEnergyRestore);
            LevelUpData.ResetMeal();
        }
        else
        {
            player.HungerPercent = HungerSystem.Instance != null
                ? HungerSystem.Instance.HungerPercent : 1f;
        }

        // Conectar HungerSystem
        if (HungerSystem.Instance != null)
        {
            HungerSystem.Instance.RegisterHungerBar(hungerBarInCombat);
            HungerSystem.Instance.EnterCombat(player);
        }

        // Refrescar displays de enemigos (muestra/oculta según Enemies del combate actual).
        // Debe ir ANTES de InitEnemyHUDs (que también llama Refresh vía GetComponentsInChildren).
        if (EnemyDisplayManager.Instance != null)
            EnemyDisplayManager.Instance.RefreshForCombat();
        else
            Debug.LogWarning("[InSceneCombatController] ⚠️ EnemyDisplayManager.Instance es null — " +
                             "ejecuta Kimera/11. Los displays de enemigos NO se actualizarán " +
                             "(ambos pueden aparecer incluso en boss fight).");

        // Inicializar UI
        if (combatUI != null)
        {
            combatUI.InitEnemyHUDs(enemies.Length);
            combatUI.SetInventory(new List<ItemData>(startingItems));
            combatUI.UpdateHUD();
        }

        // Suscribirse al final del combate
        CombatManager.Instance.onVictory += OnCombatVictory;
        CombatManager.Instance.onDefeat  += OnCombatDefeat;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Handlers de fin de combate
    // ══════════════════════════════════════════════════════════════════════════

    private void OnCombatVictory()
    {
        UnsubscribeCombatEnd();
        SoundManager.Instance?.PlayVictoryJingle();

        if (!LevelUpData.IsBossFight)
        {
            // PostCombatFlow mostrará la pantalla de nivel subido y
            // luego llamará a StartBossFight() → IsInCombat debe estar false
            // para que la nueva llamada pueda proceder.
            IsInCombat = false;
            _currentTrigger?.OnCombatWon();
        }
        else
        {
            // Boss derrotado — PostCombatFlow.ShowBossEndScreen mostrará la pantalla final.
            // NO iniciamos TransitionOutOfCombat aquí: el overlay de PostCombatFlow necesita
            // que el canvas de combate siga visible. La transición se dispara cuando el jugador
            // hace clic en "Volver al inicio" → ReturnToExploration().
            IsInCombat = false;
        }
    }

    private void OnCombatDefeat()
    {
        UnsubscribeCombatEnd();
        SoundManager.Instance?.PlayDefeatJingle();
        // PostCombatFlow muestra panel con "Reintentar" / "Abandonar".
        // Mantenemos IsInCombat = true para que el jugador no pueda moverse.
    }

    private void UnsubscribeCombatEnd()
    {
        if (CombatManager.Instance == null) return;
        CombatManager.Instance.onVictory -= OnCombatVictory;
        CombatManager.Instance.onDefeat  -= OnCombatDefeat;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private void ApplyCombatUIVisibility(bool visible)
    {
        if (combatCanvasGroup != null)
        {
            combatCanvasGroup.alpha          = visible ? 1f : 0f;
            combatCanvasGroup.interactable   = visible;
            combatCanvasGroup.blocksRaycasts = visible;
        }
        else if (combatCanvas != null)
        {
            combatCanvas.enabled = visible;
        }
    }

    private void SetPlayerMovement(bool enabled)
    {
        if (playerMovementScripts == null) return;
        foreach (var s in playerMovementScripts)
            if (s != null) s.enabled = enabled;
    }

    private void SetWorldObjects(bool active)
    {
        if (worldObjectsToHide == null) return;
        foreach (var go in worldObjectsToHide)
            if (go != null) go.SetActive(active);
    }

    private IEnumerator FadeScreen(float from, float to, float duration)
    {
        if (screenFadeImage == null) { yield return new WaitForSeconds(duration * 0.5f); yield break; }

        screenFadeImage.gameObject.SetActive(true);
        Color c = screenFadeImage.color;
        c.a = from;
        screenFadeImage.color = c;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            screenFadeImage.color = c;
            yield return null;
        }
        c.a = to;
        screenFadeImage.color = c;

        if (to <= 0f) screenFadeImage.gameObject.SetActive(false);
    }
}
