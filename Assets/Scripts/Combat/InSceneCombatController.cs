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
    [SerializeField] private CharacterStats playerStats;
    [SerializeField] private List<ItemData> startingItems;
    [SerializeField] private Slider hungerBarInCombat;

    [Tooltip("Scripts de movimiento/control que se desactivan al entrar en combate.")]
    [SerializeField] private MonoBehaviour[] playerMovementScripts;
    [Tooltip("Gestor de animaciones de combate (PlayerCombatAnimator en el Player GO)")]
    [SerializeField] private PlayerCombatAnimator playerCombatAnimator;

    // ── Arena de combate fija ──────────────────────────────────────────────────
    [Header("Arena de combate fija")]
    [Tooltip("Transform raíz del jugador en el mundo (el GO 'Player'). " +
             "Se usa para teletransportarlo a 'Player Combat Spot' al entrar en combate.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Punto fijo donde debe pararse el jugador durante CUALQUIER combate " +
             "(cerca de combatCamera). Si se deja vacío, el jugador no se mueve " +
             "y el combate ocurre donde esté parado (comportamiento anterior).")]
    [SerializeField] private Transform playerCombatSpot;

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
    [SerializeField] private Canvas combatCanvas;
    [SerializeField] private CombatUI combatUI;
    [SerializeField] private CanvasGroup combatCanvasGroup;  // raíz del Canvas

    // ── Transición ─────────────────────────────────────────────────────────────
    [Header("Transición")]
    [Tooltip("Imagen negra a pantalla completa (en un Canvas de orden superior) para el fade.")]
    [SerializeField] private Image screenFadeImage;
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Zoom de cámara al entrar en combate")]
    [Tooltip("Duración del movimiento de cámara (posición/rotación/FOV) hacia el encuadre de combate.\n" +
             "Corre en paralelo con el fade a negro, así que lo ideal es que dure parecido a Fade Duration.")]
    [SerializeField] private float cameraZoomDuration = 0.5f;

    // ── Estado ─────────────────────────────────────────────────────────────────
    private EnemyData[] _currentEnemies;
    private InSceneCombatTrigger _currentTrigger;

    // Pose FIJA de combatCamera, capturada una sola vez en Awake — el zoom
    // siempre apunta aquí, nunca a una posición calculada en tiempo real.
    private Vector3 _combatCameraDefaultPos;
    private Quaternion _combatCameraDefaultRot;
    private float _combatCameraDefaultFOV;

    // Dónde estaba el jugador ANTES de teletransportarlo a la arena,
    // para devolverlo ahí al salir del combate.
    private Vector3 _savedPlayerPosition;
    private Quaternion _savedPlayerRotation;
    private bool _playerWasTeleported;

    public bool IsInCombat { get; private set; }
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

        if (combatCamera != null)
        {
            combatCamera.enabled = false;

            // Capturar la pose original de combatCamera UNA sola vez, antes de
            // que cualquier zoom la mueva. Este es el destino fijo del zoom,
            // siempre — el combate ya no depende de dónde esté el jugador.
            _combatCameraDefaultPos = combatCamera.transform.position;
            _combatCameraDefaultRot = combatCamera.transform.rotation;
            _combatCameraDefaultFOV = combatCamera.fieldOfView;
        }
        if (screenFadeImage != null) screenFadeImage.gameObject.SetActive(false);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // API pública
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Iniciar un encuentro de combate desde un trigger del mundo.</summary>
    public void StartCombat(EnemyData[] enemies, InSceneCombatTrigger trigger)
    {
        if (IsInCombat) return;
        IsInCombat = true;
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
        // 1 — Encuadre final de combate: SIEMPRE la pose fija de combatCamera
        //     capturada en Awake. Ya no depende de dónde/cómo esté el jugador —
        //     por eso 'cameraAnchor' ya no se usa aquí (se deja el parámetro
        //     por si en el futuro se necesitan varias arenas distintas).
        Vector3 targetPos = _combatCameraDefaultPos;
        Quaternion targetRot = _combatCameraDefaultRot;
        float targetFOV = _combatCameraDefaultFOV;

        // 2 — Swap de cámara INVISIBLE: activar combatCamera exactamente en la
        //     pose actual de explorationCamera, así el corte no se nota.
        if (explorationCamera != null && combatCamera != null)
        {
            combatCamera.transform.SetPositionAndRotation(
                explorationCamera.transform.position, explorationCamera.transform.rotation);
            combatCamera.fieldOfView = explorationCamera.fieldOfView;
        }
        if (explorationCamera != null) explorationCamera.enabled = false;
        if (combatCamera != null) combatCamera.enabled = true;

        // 3 — Zoom hacia la arena fija EN PARALELO con el fade a negro:
        //     el jugador ve el arranque del zoom y el corte termina de ocultarse
        //     justo cuando la pantalla ya está en negro.
        Coroutine zoomRoutine = combatCamera != null
            ? StartCoroutine(LerpCamera(combatCamera, targetPos, targetRot, targetFOV, cameraZoomDuration))
            : null;

        yield return StartCoroutine(FadeScreen(0f, 1f, fadeDuration));
        if (zoomRoutine != null) yield return zoomRoutine;   // por si el zoom dura más que el fade

        // 4 — Ocultar objetos del mundo (NPCs, decorados, etc.)
        SetWorldObjects(false);

        // 4.5 — Teletransportar al jugador a la arena fija (pantalla ya en negro).
        //       Solo se guarda su posición real la PRIMERA vez — en un reintento
        //       (RetryCombat) el jugador ya está en el spot de combate, y no
        //       queremos sobrescribir la posición original de exploración.
        if (playerTransform != null && playerCombatSpot != null)
        {
            if (!_playerWasTeleported)
            {
                _savedPlayerPosition = playerTransform.position;
                _savedPlayerRotation = playerTransform.rotation;
                _playerWasTeleported = true;
            }
            playerTransform.SetPositionAndRotation(playerCombatSpot.position, playerCombatSpot.rotation);
        }

        // 5 — Desactivar movimiento del jugador + iniciar animación de entrada
        SetPlayerMovement(false);


        //ermm lineas que agregue (donuts) 

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;





        if (playerCombatAnimator != null)
            playerCombatAnimator.EnterCombat();
        else
            Debug.LogWarning("[InSceneCombatController] ⚠️ playerCombatAnimator es null — " +
                             "ejecuta Kimera/12 para configurarlo automáticamente.");
        SoundManager.Instance?.PlayCombatMusic();

        // 6 — Mostrar UI de combate
        ApplyCombatUIVisibility(true);

        // 7 — Inicializar combate
        InitializeCombatLogic(enemies);

        // 8 — Fade desde negro (revela el combate ya encuadrado)
        yield return StartCoroutine(FadeScreen(1f, 0f, fadeDuration));
    }

    /// <summary>Interpola posición, rotación y FOV de una cámara hacia una pose de destino.</summary>
    private IEnumerator LerpCamera(Camera cam, Vector3 toPos, Quaternion toRot, float toFOV, float duration)
    {
        if (cam == null) yield break;
        if (duration <= 0f)
        {
            cam.transform.SetPositionAndRotation(toPos, toRot);
            cam.fieldOfView = toFOV;
            yield break;
        }

        Vector3 fromPos = cam.transform.position;
        Quaternion fromRot = cam.transform.rotation;
        float fromFOV = cam.fieldOfView;

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = k * k * (3f - 2f * k);   // smoothstep — easing suave, sin Cinemachine

            cam.transform.SetPositionAndRotation(
                Vector3.Lerp(fromPos, toPos, k),
                Quaternion.Slerp(fromRot, toRot, k));
            cam.fieldOfView = Mathf.Lerp(fromFOV, toFOV, k);

            yield return null;
        }

        cam.transform.SetPositionAndRotation(toPos, toRot);
        cam.fieldOfView = toFOV;
    }

    private IEnumerator TransitionOutOfCombat()
    {
        yield return StartCoroutine(FadeScreen(0f, 1f, fadeDuration));

        // Ocultar UI de combate
        ApplyCombatUIVisibility(false);
        SoundManager.Instance?.PlayExplorationMusic();

        // Ocultar SIEMPRE los visuales 3D de enemigo (gane, pierda o abandone) —
        // antes solo se ocultaban en victoria normal, vía InSceneCombatTrigger.OnCombatWon(),
        // y nunca en derrota/abandono/boss.
        combatUI?.SetupEnemyVisuals(null);

        // Swap cámara de vuelta
        if (combatCamera != null) combatCamera.enabled = false;
        if (explorationCamera != null) explorationCamera.enabled = true;

        // Restaurar objetos del mundo (NPCs, decorados, etc.)
        SetWorldObjects(true);

        // Devolver al jugador a donde estaba antes de entrar a la arena de combate
        if (_playerWasTeleported && playerTransform != null)
        {
            playerTransform.SetPositionAndRotation(_savedPlayerPosition, _savedPlayerRotation);
            _playerWasTeleported = false;
        }

        // Re-activar movimiento + volver a animación de exploración
        playerCombatAnimator?.ExitCombat();
        SetPlayerMovement(true);

        // para que vuelva a moverse el player ok buddy :3
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

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

        // Activar/ocultar y configurar los visuales 3D de enemigo (placeholders)
        // según la lista real de este combate. Reemplaza al viejo EnemyDisplayManager
        // (sistema de sprites en Canvas).
        combatUI?.SetupEnemyVisuals(CombatManager.Instance.Enemies);

        // Inicializar UI
        if (combatUI != null)
        {
            combatUI.InitEnemyHUDs(enemies.Length);
            combatUI.SetInventory(new List<ItemData>(startingItems));
            combatUI.UpdateHUD();
        }

        // Suscribirse al final del combate
        CombatManager.Instance.onVictory += OnCombatVictory;
        CombatManager.Instance.onDefeat += OnCombatDefeat;
        CombatManager.Instance.onEnemyTookDamage += OnEnemyTookDamage;
        CombatManager.Instance.onPlayerTookDamage += OnPlayerTookDamage;
        CombatManager.Instance.onEnemyActionDecided += OnEnemyActionDecided;
    }

    /// <summary>Cuando el jugador recibe daño, dispara su animación de reacción.</summary>
    private void OnPlayerTookDamage(int damage)
    {
        combatUI?.PlayPlayerHurtReaction();
    }

    /// <summary>Cada golpe que recibe un enemigo: decide sola si es Hurt o Death.</summary>
    private void OnEnemyTookDamage(EnemyCombatant enemy)
    {
        combatUI?.NotifyEnemyDamaged(enemy);
    }

    /// <summary>
    /// Cuando el enemigo decide su acción del turno: dispara Prepare (turno de carga),
    /// Defend (se defiende/esquiva) o Attack1 (ataque normal) según corresponda.
    /// El ataque en sí (daño aplicado) ya lo resuelve CombatManager por su cuenta.
    /// </summary>
    private void OnEnemyActionDecided(EnemyCombatant enemy, EnemyActionResult action)
    {
        if (combatUI == null || enemy == null) return;

        if (action.isDefend || action.isDodge)
            combatUI.PlayEnemyDefendAnimation(enemy);
        else if (enemy.IsPreparing)
            combatUI.PlayEnemyPrepareAnimation(enemy);
        else
            combatUI.PlayEnemyAttackAnimation(enemy, CombatManager.Instance.Player);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Handlers de fin de combate
    // ══════════════════════════════════════════════════════════════════════════

    private void OnCombatVictory()
    {
        UnsubscribeCombatEnd();
        SoundManager.Instance?.PlayVictoryJingle();
        combatUI?.PlayPlayerVictoryAnimation();

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
        combatUI?.PlayPlayerDeathAnimation();
        // PostCombatFlow muestra panel con "Reintentar" / "Abandonar".
        // Mantenemos IsInCombat = true para que el jugador no pueda moverse.
    }

    private void UnsubscribeCombatEnd()
    {
        if (CombatManager.Instance == null) return;
        CombatManager.Instance.onVictory -= OnCombatVictory;
        CombatManager.Instance.onDefeat -= OnCombatDefeat;
        CombatManager.Instance.onEnemyTookDamage -= OnEnemyTookDamage;
        CombatManager.Instance.onPlayerTookDamage -= OnPlayerTookDamage;
        CombatManager.Instance.onEnemyActionDecided -= OnEnemyActionDecided;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private void ApplyCombatUIVisibility(bool visible)
    {
        if (combatCanvasGroup != null)
        {
            combatCanvasGroup.alpha = visible ? 1f : 0f;
            combatCanvasGroup.interactable = visible;
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