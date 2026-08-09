using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// CombatHitFeedback
// ─────────────────────────────────────────────────────────────────────────────
// Coloca este componente en el GO "_CombatCanvas" (o cualquier hijo activo).
//
// CÓMO FUNCIONA EL FEEDBACK DE ENEMIGOS:
//   Este script NO produce ningún efecto visual directamente en los enemigos.
//   Cuando recibe onEnemyTookDamage, identifica el display correcto mediante
//   EnemyDisplayManager.GetDisplayAt(índice) y delega TODO el feedback visual
//   a EnemyActionDisplay.FlashDamage(). Cada display controla únicamente sus
//   propios componentes (Image, RectTransform, Animator) — sin referencias
//   cruzadas entre enemigos.
//
//   Búsqueda primaria:  EnemyDisplayManager.GetDisplayAt(idx)   — O(1), segura
//   Búsqueda de respaldo: TracksEnemy() sobre el caché en _displays — solo si
//                          EnemyDisplayManager no está en la escena.
//
// EFECTOS:
//   Enemigo  → delegado íntegramente a EnemyActionDisplay.FlashDamage()
//   Jugador  → flash rojo en retrato + shake + overlay rojo + cámara shake
//              (FlashImage / ShakeRect son EXCLUSIVOS del jugador)
//   Instinto → pulse de overlay azul/teal al usar Mirada Instintiva
// ─────────────────────────────────────────────────────────────────────────────
[DefaultExecutionOrder(-50)]
public class CombatHitFeedback : MonoBehaviour
{
    [Header("Jugador")]
    [SerializeField] private Image  playerPortraitImage;
    [Tooltip("Imagen de pantalla completa para el overlay rojo de daño recibido")]
    [SerializeField] private Image  screenDamageOverlay;
    [SerializeField] private Camera combatCamera;

    [Header("Ajustes de feedback de daño")]
    [SerializeField] private float flashDuration        = 0.07f;
    [SerializeField] private float flashRecoverDuration = 0.20f;
    [SerializeField] private float shakeDuration        = 0.28f;
    [SerializeField] private float shakeMagnitude       = 9f;
    [SerializeField] private float cameraShakeMagnitude = 0.14f;
    [SerializeField] private float overlayDuration      = 0.40f;
    [SerializeField] private float overlayMaxAlpha      = 0.38f;

    [Header("Overlay de Instinto (pulse azul/teal)")]
    [SerializeField] private float instinctPulseIn   = 0.18f;
    [SerializeField] private float instinctPulseOut  = 0.70f;
    [SerializeField] private float instinctMaxAlpha  = 0.28f;
    [SerializeField] private Color instinctColor     = new Color(0.10f, 0.50f, 1.00f);

    private static readonly Color HitRed = new Color(1f, 0.08f, 0.08f);

    private Coroutine            _instinctRoutine;
    private EnemyActionDisplay[] _displays;

    // ── Lifecycle ───────────────────────────────────────────────────────────

    private IEnumerator Start()
    {
        while (CombatManager.Instance == null) yield return null;

        CombatManager.Instance.onEnemyTookDamage   += OnEnemyDamaged;
        CombatManager.Instance.onPlayerTookDamage  += OnPlayerDamaged;
        CombatManager.Instance.onShowAnalysisPanel += OnInstinct;

        // Cachear todos los EnemyActionDisplay de la escena.
        // Se crean en el Setup (Menu 5) y nunca se destruyen en runtime,
        // así que basta con cachearlos una vez.
        RefreshDisplayCache();
    }

    private void OnDestroy()
    {
        if (CombatManager.Instance == null) return;
        CombatManager.Instance.onEnemyTookDamage   -= OnEnemyDamaged;
        CombatManager.Instance.onPlayerTookDamage  -= OnPlayerDamaged;
        CombatManager.Instance.onShowAnalysisPanel -= OnInstinct;
    }

    /// <summary>
    /// Re-escanea todos los EnemyActionDisplay de la escena (activos e inactivos).
    /// No es necesario llamarlo entre combates — los displays son permanentes.
    /// </summary>
    public void RefreshDisplayCache()
    {
        _displays = FindObjectsByType<EnemyActionDisplay>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        Debug.Log($"[CombatHitFeedback] ── Cache de displays ────────────────────");
        foreach (var d in _displays)
        {
            if (d == null) { Debug.Log("  [null]"); continue; }
            string imgInfo = d.displayImage == null
                ? "displayImage=NULL ⚠️"
                : $"displayImage='{d.displayImage.gameObject.name}' (ID:{d.displayImage.gameObject.GetInstanceID()})";
            Debug.Log($"  GO='{d.gameObject.name}'  activo={d.gameObject.activeInHierarchy}  " +
                      $"enemyIndex={d.EnemyIndex}  {imgInfo}");
        }
        Debug.Log($"[CombatHitFeedback] ────────────────────────────────────────");
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private void OnEnemyDamaged(EnemyCombatant enemy)
    {
        // ── Paso 1: encontrar el índice del enemigo en la lista activa ─────────
        // IReadOnlyList<T> no expone IndexOf, así que buscamos manualmente.
        var enemies = CombatManager.Instance?.Enemies;
        if (enemies == null) return;

        int idx = -1;
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == enemy) { idx = i; break; }
        }

        // ── Paso 2: obtener el display por índice (camino principal) ───────────
        EnemyActionDisplay display = null;

        if (idx >= 0 && EnemyDisplayManager.Instance != null)
        {
            display = EnemyDisplayManager.Instance.GetDisplayAt(idx);
            Debug.Log($"[CombatHitFeedback] Daño a '{enemy?.Data?.enemyName}' " +
                      $"(índice {idx}) → display '{display?.gameObject.name ?? "NULL"}'");
        }

        // ── Paso 3: fallback — búsqueda dinámica por TracksEnemy() ────────────
        // Necesario si EnemyDisplayManager no está en escena (escena sin Menú 11).
        if (display == null || !display.gameObject.activeInHierarchy)
        {
            display = FindActiveDisplayFor(enemy);
            if (display != null)
                Debug.Log($"[CombatHitFeedback] (fallback TracksEnemy) → '{display.gameObject.name}'");
        }

        if (display == null)
        {
            Debug.LogWarning($"[CombatHitFeedback] ⚠️ Sin display activo para '{enemy?.Data?.enemyName}'.");
            return;
        }

        // ── Paso 4: delegar TODO el feedback visual al propio display ─────────
        // FlashDamage() gestiona color + shake internamente.
        // Este script NO toca ningún Image ni RectTransform del display.
        display.FlashDamage();
    }

    /// <summary>Búsqueda de respaldo: recorre el caché y usa TracksEnemy().</summary>
    private EnemyActionDisplay FindActiveDisplayFor(EnemyCombatant enemy)
    {
        if (_displays == null) return null;
        foreach (var d in _displays)
        {
            if (d == null || !d.gameObject.activeInHierarchy) continue;
            if (d.TracksEnemy(enemy)) return d;
        }
        return null;
    }

    private void OnPlayerDamaged(int damage)
    {
        if (playerPortraitImage != null)
        {
            StartCoroutine(FlashImage(playerPortraitImage));
            StartCoroutine(ShakeRect(playerPortraitImage.rectTransform));
        }
        if (screenDamageOverlay != null)
            StartCoroutine(FlashOverlay());
        if (combatCamera != null)
            StartCoroutine(ShakeCamera());
    }

    private void OnInstinct(string _)
    {
        if (screenDamageOverlay == null) return;
        if (_instinctRoutine != null) StopCoroutine(_instinctRoutine);
        _instinctRoutine = StartCoroutine(FlashInstinct());
    }

    // ── Efectos de daño — EXCLUSIVOS DEL JUGADOR ────────────────────────────
    // FlashImage y ShakeRect solo se llaman desde OnPlayerDamaged.
    // El feedback de enemigos lo gestiona EnemyActionDisplay.FlashDamage().

    private IEnumerator FlashImage(Image img)
    {
        Color original = img.color;

        img.color = HitRed;
        yield return new WaitForSeconds(flashDuration);

        float t = 0f;
        while (t < flashRecoverDuration)
        {
            t += Time.deltaTime;
            img.color = Color.Lerp(HitRed, original, t / flashRecoverDuration);
            yield return null;
        }
        img.color = original;
    }

    private IEnumerator ShakeRect(RectTransform rt)
    {
        if (rt == null) yield break;
        Vector2 origin  = rt.anchoredPosition;
        float   elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float progress = 1f - (elapsed / shakeDuration);
            float ox = Random.Range(-1f, 1f) * shakeMagnitude * progress;
            float oy = Random.Range(-1f, 1f) * shakeMagnitude * progress;
            rt.anchoredPosition = origin + new Vector2(ox, oy);
            elapsed += Time.deltaTime;
            yield return null;
        }
        rt.anchoredPosition = origin;
    }

    private IEnumerator FlashOverlay()
    {
        screenDamageOverlay.gameObject.SetActive(true);

        Color c = new Color(1f, 0f, 0f, overlayMaxAlpha);
        screenDamageOverlay.color = c;

        float t = 0f;
        while (t < overlayDuration)
        {
            t   += Time.deltaTime;
            c.a  = Mathf.Lerp(overlayMaxAlpha, 0f, t / overlayDuration);
            screenDamageOverlay.color = c;
            yield return null;
        }

        screenDamageOverlay.gameObject.SetActive(false);
    }

    private IEnumerator ShakeCamera()
    {
        Transform camT   = combatCamera.transform;
        Vector3   origin = camT.localPosition;
        float elapsed    = 0f;
        float duration   = shakeDuration * 0.55f;

        while (elapsed < duration)
        {
            float progress = 1f - (elapsed / duration);
            float ox = Random.Range(-1f, 1f) * cameraShakeMagnitude * progress;
            float oy = Random.Range(-1f, 1f) * cameraShakeMagnitude * progress;
            camT.localPosition = origin + new Vector3(ox, oy, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }
        camT.localPosition = origin;
    }

    // ── Efecto de Instinto ────────────────────────────────────────────────────

    private IEnumerator FlashInstinct()
    {
        screenDamageOverlay.gameObject.SetActive(true);

        Color c = instinctColor;
        c.a = 0f;
        screenDamageOverlay.color = c;

        float t = 0f;
        while (t < instinctPulseIn)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0f, instinctMaxAlpha, t / instinctPulseIn);
            screenDamageOverlay.color = c;
            yield return null;
        }
        c.a = instinctMaxAlpha;
        screenDamageOverlay.color = c;

        t = 0f;
        while (t < instinctPulseOut)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(instinctMaxAlpha, 0f, t / instinctPulseOut);
            screenDamageOverlay.color = c;
            yield return null;
        }

        c.a = 0f;
        screenDamageOverlay.color = c;
        screenDamageOverlay.gameObject.SetActive(false);
        _instinctRoutine = null;
    }
}
