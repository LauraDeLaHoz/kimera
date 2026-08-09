using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// EnemyActionDisplay
// ─────────────────────────────────────────────────────────────────────────────
//
// PROPÓSITO:
//   Controla el display visual de UN enemigo en el Canvas de combate.
//   Cada display es completamente autónomo: solo toca sus propios componentes.
//   Ningún script externo escribe en el Image ni en el RectTransform de este GO.
//
// INVARIANTES DE DISEÑO (no romper):
//   • displayImage    → es el Image del hijo "SpriteImage" de ESTE display.
//   • spriteAnimator  → es el EnemyUISpriteAnimator del mismo "SpriteImage".
//   • Nunca compartas estas referencias con otro EnemyActionDisplay.
//   • Toda escritura a displayImage.color ocurre DENTRO de esta clase.
//   • Toda escritura al RectTransform raíz ocurre DENTRO de esta clase.
//
// CICLO POR COMBATE:
//   1. EnemyDisplayManager.RefreshForCombat() → llama Refresh() en cada display.
//   2. Refresh() resuelve qué EnemyCombatant seguir usando enemyIndex.
//      · Si Enemies[enemyIndex] existe → SetActive(true), carga sprite/animación.
//      · Si no existe (boss fight, slot vacío) → SetActive(false).
//   3. Durante el combate:
//      · OnAction()   → cambia tint de color al anunciarse una acción.
//      · ResetToIdle()→ restaura sprite/animación al inicio del turno jugador.
//      · FlashDamage()→ feedback de hit (flash rojo + shake). CombatHitFeedback
//                       llama este método; no toca el Image directamente.
//
// CÓMO AÑADIR UN TERCER ENEMIGO (sin cambios de código):
//   1. Crea un GO "EnemyDisplay_2" en la jerarquía del Canvas
//      con la misma estructura que EnemyDisplay_0/1
//      (hijo Frame con Image + hijo SpriteImage con Image + EnemyUISpriteAnimator).
//   2. Añade EnemyActionDisplay con enemyIndex = 2 y cablea displayImage.
//   3. Añádelo a EnemyDisplayManager.displays[2].
//   4. Añade un EnemyHUDElement a CombatUI.enemyHUDs[2].
//   Sin cambios de código en ningún script existente.
//
// LOGS:
//   Activa "Verbose Logs" en el Inspector para ver diagnóstico de Refresh().
//   Desactívalo en producción para eliminar spam de Consola.
// ─────────────────────────────────────────────────────────────────────────────
public class EnemyActionDisplay : MonoBehaviour, IPointerClickHandler
{
    // ── Referencia visual ─────────────────────────────────────────────────────

    [Header("Referencia visual ← apunta al Image del hijo 'SpriteImage'")]
    [Tooltip("Debe ser el Image del hijo SpriteImage de ESTE display. " +
             "No compartas esta referencia con otro EnemyActionDisplay.")]
    [SerializeField] public Image displayImage;

    [Header("Animación idle ← auto-detectado desde displayImage si queda vacío")]
    [SerializeField] private EnemyUISpriteAnimator spriteAnimator;

    // ── Seguimiento del enemigo ───────────────────────────────────────────────

    [Header("Seguimiento del enemigo")]
    [Tooltip("Índice en CombatManager.Enemies[]. " +
             "0 = primer enemigo, 1 = segundo, etc. " +
             "EnemyDisplayManager.displays[] debe respetar el mismo orden.")]
    [SerializeField] private int enemyIndex = 0;

    [Tooltip("LEGACY — solo activo si enemyIndex == -1. " +
             "Usa enemyIndex siempre que sea posible.")]
    [SerializeField] private string trackedEnemyName = "";

    // ── Sprites de acción (opcionales) ───────────────────────────────────────

    [Header("Sprites de acción (opcional — si null se usa el sprite idle con tint)")]
    [Tooltip("Si asignas un sprite aquí, se muestra durante la acción en vez del idle con tint.")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite attackSprite;
    [SerializeField] private Sprite dodgeSprite;
    [SerializeField] private Sprite defendSprite;
    [SerializeField] private Sprite chargeSprite;

    // ── Colores de acción ─────────────────────────────────────────────────────

    [Header("Colores de acción (tint sobre el sprite)")]
    [SerializeField] private Color idleColor   = Color.white;
    [SerializeField] private Color attackColor = new Color(1f,    0.28f, 0.28f);
    [SerializeField] private Color dodgeColor  = new Color(0.28f, 0.90f, 0.90f);
    [SerializeField] private Color defendColor = new Color(0.28f, 0.50f, 1.00f);
    [SerializeField] private Color chargeColor = new Color(1f,    0.78f, 0.18f);

    // ── Feedback de daño ─────────────────────────────────────────────────────

    [Header("Feedback de daño recibido")]
    [Tooltip("Duración del destello rojo (seg)")]
    [SerializeField] private float damageFlashDuration   = 0.07f;
    [Tooltip("Duración del fundido de vuelta al idle (seg)")]
    [SerializeField] private float damageRecoverDuration = 0.20f;
    [Tooltip("Duración del temblor del panel completo (seg)")]
    [SerializeField] private float damageShakeDuration   = 0.28f;
    [Tooltip("Magnitud máxima del temblor en píxeles")]
    [SerializeField] private float damageShakeMagnitude  = 9f;

    // ── Selección visual ──────────────────────────────────────────────────────

    [Header("Selección (auto-creado por Kimera/11)")]
    [Tooltip("Imagen de overlay que se activa cuando este enemigo está seleccionado como objetivo. " +
             "Kimera/11 la crea automáticamente como hijo 'SelectionHighlight'.")]
    [SerializeField] private Image selectionHighlight;

    // ── Depuración ────────────────────────────────────────────────────────────

    [Header("Depuración")]
    [Tooltip("Activa logs detallados de Refresh() en la Consola. " +
             "Desactivar en producción para reducir ruido.")]
    [SerializeField] private bool verboseLogs = false;

    // ── Constantes ────────────────────────────────────────────────────────────

    // Color rojo de daño, fijo. Independiente de cualquier tint de acción.
    private static readonly Color DamageColor = new Color(1f, 0.08f, 0.08f);

    // ── Estado en runtime ─────────────────────────────────────────────────────

    private EnemyCombatant _tracked;       // enemigo que este slot muestra actualmente
    private Coroutine      _resetRoutine;  // temporizador para volver al idle tras acción
    private Coroutine      _damageRoutine; // corutina de flash de daño (color)
    private Coroutine      _shakeRoutine;  // corutina de shake (posición RT raíz)
    private bool           _subscribed;   // suscrito a eventos de CombatManager
    private bool           _isSelected;   // este display está actualmente seleccionado

    // Posición del RT raíz antes de que empiece un shake.
    // Se usa para restaurar si el GO es desactivado a mitad de un temblor.
    private Vector2 _restorePosition;
    private bool    _shakeInProgress;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-detectar el SpriteAnimator temprano, antes de que RefreshForCombat()
        // pueda llamar a Refresh() mientras Start() aún está pendiente.
        if (spriteAnimator == null && displayImage != null)
            spriteAnimator = displayImage.GetComponent<EnemyUISpriteAnimator>();
    }

    private IEnumerator Start()
    {
        // Esperar a que CombatManager esté listo y tenga enemigos cargados.
        while (CombatManager.Instance == null || CombatManager.Instance.Enemies == null)
            yield return null;

        // Segunda oportunidad de auto-detección (por si Awake no se ejecutó antes).
        if (spriteAnimator == null && displayImage != null)
            spriteAnimator = displayImage.GetComponent<EnemyUISpriteAnimator>();

        Refresh();
    }

    private void OnDisable()
    {
        // Si el GO es desactivado a mitad de un shake (p.ej. Refresh() → SetActive(false)),
        // las corutinas se detienen pero la posición del RT puede quedar desplazada.
        // Restauramos la posición capturada antes de que empezara el temblor.
        if (_shakeInProgress)
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = _restorePosition;
            _shakeInProgress = false;
        }
    }

    private void OnDestroy() => Unsubscribe();

    // ── API pública ────────────────────────────────────────────────────────────

    /// <summary>
    /// Decide qué enemigo muestra este slot y carga su sprite/animación.
    /// <list type="bullet">
    ///   <item><c>Enemies[enemyIndex]</c> existe → activa el GO y carga datos.</item>
    ///   <item>No existe (slot vacío, boss fight) → desactiva el GO.</item>
    /// </list>
    /// Llamado por <see cref="EnemyDisplayManager.RefreshForCombat"/> al inicio
    /// de cada combate y por <see cref="CombatUI.InitEnemyHUDs"/>.
    /// </summary>
    public void Refresh()
    {
        // Cancelar todas las corutinas visuales pendientes antes de reconfigurar.
        CancelAllRoutines();
        Unsubscribe();
        _tracked = null;

        if (CombatManager.Instance?.Enemies == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // ── Resolver qué enemigo seguir ───────────────────────────────────────
        if (enemyIndex >= 0)
        {
            if (enemyIndex < CombatManager.Instance.Enemies.Count)
                _tracked = CombatManager.Instance.Enemies[enemyIndex];
        }
        else if (!string.IsNullOrEmpty(trackedEnemyName))
        {
            // LEGACY: búsqueda por nombre. Preferir enemyIndex siempre que sea posible.
            foreach (var e in CombatManager.Instance.Enemies)
                if (e?.Data?.enemyName == trackedEnemyName) { _tracked = e; break; }
        }

        // ── Log de diagnóstico (solo si verboseLogs está activo) ──────────────
        if (verboseLogs)
        {
            string imgInfo = displayImage == null
                ? "displayImage=NULL ⚠️"
                : $"displayImage='{displayImage.gameObject.name}' (ID:{displayImage.gameObject.GetInstanceID()})";
            string enemyInfo = _tracked == null
                ? $"OCULTAR (enemyIndex={enemyIndex}, Enemies.Count={CombatManager.Instance.Enemies.Count})"
                : $"tracking '{_tracked.Data.enemyName}' (enemyIndex={enemyIndex})";
            Debug.Log($"[EnemyActionDisplay] GO='{gameObject.name}' → {enemyInfo} | {imgInfo}");
        }

        if (_tracked == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        SetSelected(false);   // deseleccionar al inicio de cada combate

        // ── Cargar sprite/animación desde EnemyData ───────────────────────────
        // El sprite siempre viene del enemigo tracked, no de valores pre-asignados
        // en el editor. Esto garantiza que en el boss fight se muestre la hiena,
        // no los frames del conejo que quedaron del combate anterior.
        bool hasAnimFrames = _tracked.Data.idleAnimationFrames != null
                             && _tracked.Data.idleAnimationFrames.Length >= 2;

        if (spriteAnimator != null)
        {
            if (hasAnimFrames)
            {
                spriteAnimator.SetFrames(_tracked.Data.idleAnimationFrames,
                                          _tracked.Data.animationFps);
                // Forzar sprite en displayImage de inmediato, sin esperar al Update del animador.
                // Necesario cuando displayImage y spriteAnimator._image son la misma referencia
                // pero el animador aún no ha ejecutado su primer Update tras el cambio de combate.
                if (displayImage != null && _tracked.Data.idleAnimationFrames[0] != null)
                    displayImage.sprite = _tracked.Data.idleAnimationFrames[0];
                if (verboseLogs)
                    Debug.Log($"[EnemyActionDisplay] '{gameObject.name}' → animación '{_tracked.Data.enemyName}' ({_tracked.Data.idleAnimationFrames.Length} frames)");
            }
            else
            {
                // Sin frames → limpiar animación anterior y mostrar sprite estático.
                spriteAnimator.SetFrames(null);
                SetStaticSprite();
                if (verboseLogs)
                {
                    string sprInfo = _tracked.Data.sprite == null
                        ? "sprite=NULL ⚠️" : $"sprite='{_tracked.Data.sprite.name}'";
                    Debug.Log($"[EnemyActionDisplay] '{gameObject.name}' → sprite estático '{_tracked.Data.enemyName}': {sprInfo}");
                }
            }
        }
        else
        {
            SetStaticSprite();
            if (verboseLogs)
            {
                string sprInfo = _tracked.Data.sprite == null
                    ? "sprite=NULL ⚠️" : $"sprite='{_tracked.Data.sprite.name}'";
                Debug.Log($"[EnemyActionDisplay] '{gameObject.name}' → sin animador, sprite estático: {sprInfo}");
            }
        }

        if (displayImage != null)
        {
            displayImage.color          = idleColor;
            displayImage.preserveAspect = true;
        }

        Subscribe();
    }

    /// <summary>
    /// Feedback visual de daño: destello rojo + shake del panel completo.
    /// Autocontenido — gestiona su propio Image.color y RectTransform.
    /// Llamado exclusivamente por <see cref="CombatHitFeedback"/>.
    /// </summary>
    public void FlashDamage()
    {
        if (displayImage == null)
        {
            Debug.LogWarning($"[EnemyActionDisplay] '{gameObject.name}' FlashDamage() ignorado — " +
                             "displayImage es NULL. Re-ejecuta Kimera/11 para recablear.");
            return;
        }

        // Log de diagnóstico: muestra qué display se activa y qué sprite tiene cargado.
        Debug.Log($"[EnemyActionDisplay] ⚡ FlashDamage  GO='{gameObject.name}'  " +
                  $"tracked='{_tracked?.Data?.enemyName ?? "NULL"}'  " +
                  $"sprite='{displayImage.sprite?.name ?? "NULL"}'");

        // Cancelar routines de daño anteriores (golpes rápidos consecutivos)
        // y cualquier reset de acción pendiente.
        if (_damageRoutine != null) { StopCoroutine(_damageRoutine); _damageRoutine = null; }
        if (_shakeRoutine  != null) { StopCoroutine(_shakeRoutine);  _shakeRoutine  = null; }
        if (_resetRoutine  != null) { StopCoroutine(_resetRoutine);  _resetRoutine  = null; }

        _damageRoutine = StartCoroutine(DamageFlashRoutine());
        _shakeRoutine  = StartCoroutine(DamageShakeRoutine());
    }

    /// <summary>¿Este display está siguiendo al enemigo indicado?</summary>
    public bool TracksEnemy(EnemyCombatant enemy) => _tracked != null && _tracked == enemy;

    /// <summary>Índice serializado del slot (refleja <c>enemyIndex</c>).</summary>
    public int EnemyIndex => enemyIndex;

    /// <summary>Asigna el índice de slot antes de llamar a <see cref="Refresh"/>.</summary>
    public void SetEnemyIndex(int idx) => enemyIndex = idx;

    /// <summary>LEGACY. Usa <see cref="SetEnemyIndex"/> siempre que sea posible.</summary>
    public void SetTrackedEnemy(string n) => trackedEnemyName = n;

    // ── Click para seleccionar enemigo ────────────────────────────────────────

    /// <summary>
    /// Recibe el clic del jugador sobre el sprite del enemigo.
    /// El evento sube automáticamente desde los hijos (SpriteImage) hasta este GO raíz.
    /// Solo actúa durante el turno del jugador y si el enemigo sigue vivo.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (CombatManager.Instance == null) return;
        if (CombatManager.Instance.State != CombatState.PlayerTurn) return;
        if (_tracked == null || !_tracked.IsAlive) return;

        // Deseleccionar todos los displays activos, luego activar este.
        var all = FindObjectsByType<EnemyActionDisplay>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var d in all) d.SetSelected(false);
        SetSelected(true);

        // Notificar a CombatUI (aplica el ataque u otra acción sobre este enemigo).
        FindFirstObjectByType<CombatUI>()?.SelectEnemy(_tracked);
    }

    /// <summary>
    /// Muestra u oculta el highlight de selección.
    /// Llamado automáticamente por <see cref="OnPointerClick"/> y por <see cref="Refresh"/>.
    /// </summary>
    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (selectionHighlight != null)
            selectionHighlight.gameObject.SetActive(selected);
    }

    // ── Feedback de daño ─────────────────────────────────────────────────────

    private IEnumerator DamageFlashRoutine()
    {
        spriteAnimator?.Pause(true);

        displayImage.color = DamageColor;
        yield return new WaitForSeconds(damageFlashDuration);

        float t = 0f;
        while (t < damageRecoverDuration)
        {
            t += Time.deltaTime;
            displayImage.color = Color.Lerp(DamageColor, idleColor, t / damageRecoverDuration);
            yield return null;
        }

        // Restaurar estado idle completo (reanuda animación, restaura sprite y color).
        ResetToIdle();
        _damageRoutine = null;
    }

    private IEnumerator DamageShakeRoutine()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) yield break;

        // Guardar posición para restaurarla si se interrumpe (OnDisable / golpe nuevo).
        _restorePosition = rt.anchoredPosition;
        _shakeInProgress = true;

        float elapsed = 0f;
        while (elapsed < damageShakeDuration)
        {
            float progress = 1f - (elapsed / damageShakeDuration);
            rt.anchoredPosition = _restorePosition + new Vector2(
                Random.Range(-1f, 1f) * damageShakeMagnitude * progress,
                Random.Range(-1f, 1f) * damageShakeMagnitude * progress);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rt.anchoredPosition = _restorePosition;
        _shakeInProgress = false;
        _shakeRoutine    = null;
    }

    // ── Visual de acciones (tint de color según la acción declarada) ──────────

    private void OnAction(string message)
    {
        if (_tracked == null) return;

        // Los mensajes tienen el formato "NombreEnemigo: Acción".
        // Cada display filtra solo los mensajes de SU enemigo.
        string prefix = _tracked.Data.enemyName + ":";
        if (!message.StartsWith(prefix)) return;

        string action = message.Substring(prefix.Length).Trim();

        if (action.Contains("Esquivar"))
            Flash(dodgeSprite, dodgeColor);
        else if (action.Contains("Preparando") || action.Contains("Defender"))
            Flash(defendSprite, defendColor);
        else if ((action.Contains("Carga") || action.Contains("Pisotón") || action.Contains("Mordida"))
                 && !action.Contains("Preparando"))
            Flash(chargeSprite, chargeColor);
        else if (action.Contains("Ataque") || action.Contains("Frenético") ||
                 action.Contains("Normal")  || action.Contains("Agresivo"))
            Flash(attackSprite, attackColor);
    }

    private void ResetToIdle()
    {
        if (_tracked == null) return;

        bool hasAnimFrames = _tracked.Data.idleAnimationFrames != null
                             && _tracked.Data.idleAnimationFrames.Length >= 2;

        if (hasAnimFrames && spriteAnimator != null && spriteAnimator.HasAnimation)
        {
            // Reanudar animación y forzar el sprite del frame actual inmediatamente.
            // Sin esto, displayImage.sprite puede mostrar el último sprite de acción
            // durante un frame antes de que el animador actualice en su Update.
            spriteAnimator.Pause(false);
            if (displayImage != null)
            {
                Sprite cur = spriteAnimator.FirstFrame;
                if (cur != null) displayImage.sprite = cur;
                displayImage.color = idleColor;
            }
        }
        else
        {
            // Prioridad: sprite del enemigo tracked > idleSprite del Inspector.
            // idleSprite del Inspector es solo fallback cuando EnemyData no tiene sprite.
            Sprite idle = _tracked?.Data.sprite ?? idleSprite;
            Apply(idle, idleColor);
        }
    }

    private void Flash(Sprite sprite, Color color)
    {
        spriteAnimator?.Pause(true);

        if (sprite == null)
            sprite = spriteAnimator?.FirstFrame ?? _tracked?.Data.sprite;

        Apply(sprite, color);

        if (_resetRoutine != null) StopCoroutine(_resetRoutine);
        _resetRoutine = StartCoroutine(ResetAfter(1.0f));
    }

    private void Apply(Sprite sprite, Color color)
    {
        if (displayImage == null) return;
        if (sprite != null) displayImage.sprite = sprite;
        displayImage.color = color;
    }

    private IEnumerator ResetAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        ResetToIdle();
        _resetRoutine = null;
    }

    // ── Helpers privados ──────────────────────────────────────────────────────

    /// <summary>Cancela todas las corutinas visuales activas y limpia sus referencias.</summary>
    private void CancelAllRoutines()
    {
        if (_resetRoutine  != null) { StopCoroutine(_resetRoutine);  _resetRoutine  = null; }
        if (_damageRoutine != null) { StopCoroutine(_damageRoutine); _damageRoutine = null; }
        if (_shakeRoutine  != null) { StopCoroutine(_shakeRoutine);  _shakeRoutine  = null; }

        // Si había un shake en curso, restaurar la posición del RT raíz.
        if (_shakeInProgress)
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null) rt.anchoredPosition = _restorePosition;
            _shakeInProgress = false;
        }
    }

    private void SetStaticSprite()
    {
        if (displayImage == null) return;
        // Prioridad: sprite del enemigo tracked > idleSprite del Inspector.
        // Esto evita que un idleSprite residual de un combate anterior sobreescriba
        // el sprite correcto del nuevo enemigo (ej. idleSprite=Conejo sobreescribía a Hiena).
        Sprite s = _tracked?.Data.sprite ?? idleSprite;
        if (s != null)
        {
            displayImage.sprite         = s;
            displayImage.preserveAspect = true;
        }
    }

    // ── Suscripciones a eventos de CombatManager ─────────────────────────────

    private void Subscribe()
    {
        if (_subscribed) return;
        CombatManager.Instance.onActionPerformed   += OnAction;
        CombatManager.Instance.onPlayerTurnStarted += ResetToIdle;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || CombatManager.Instance == null) return;
        CombatManager.Instance.onActionPerformed   -= OnAction;
        CombatManager.Instance.onPlayerTurnStarted -= ResetToIdle;
        _subscribed = false;
    }
}
