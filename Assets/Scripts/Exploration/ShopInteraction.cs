using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// ShopInteraction
// ─────────────────────────────────────────────────────────────────────────────
// Coloca este componente en "_TiendaTrigger" (hijo de la Tienda).
// Requiere un Collider con isTrigger = true en el mismo GO.
//
// FLUJO:
//   Jugador entra al trigger
//     → movimiento del player desactivado
//     → cursor visible
//     → cámara swap a shopCamera (si está asignada)
//     → shopPanel aparece con los 3 botones
//
//   Jugador hace click en uno de los botones
//     → sonido click
//     → shopPanel desaparece
//     → GameProgress.CompleteShop(índice)
//     → cámara vuelve, movimiento libre, cursor restaurado
//     → este trigger desactivado PERMANENTEMENTE
//
// SETUP (o ejecuta Kimera/14 para hacerlo automático):
//   · shopPanel        → el GO del panel UI de la tienda (empieza inactivo)
//   · shopItems[]      → los 3 botones con componente ShopItem
//   · playerMovement   → thirdPersonMovement del jugador
//   · cameraController → CameraController de la cámara principal
//   · explorationCamera / shopCamera → para el swap de cámara
//   · fadeImage        → Image negra full-screen (opcional, para fade suave)
// ─────────────────────────────────────────────────────────────────────────────
[RequireComponent(typeof(Collider))]
public class ShopInteraction : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("UI de la tienda")]
    [Tooltip("Panel/Canvas de la tienda. Debe empezar INACTIVO en la escena.")]
    public GameObject shopPanel;
    [Tooltip("Los 3 botones de opción, cada uno con componente ShopItem.")]
    public ShopItem[] shopItems;

    [Header("Player")]
    public thirdPersonMovement playerMovement;
    public CameraController    cameraController;

    [Header("Cámaras (opcional)")]
    [Tooltip("Cámara principal — se apaga al entrar si hay una shopCamera asignada")]
    public Camera explorationCamera;
    [Tooltip("Cámara de la tienda — se enciende al entrar. Deja vacío para no hacer swap.")]
    public Camera shopCamera;

    [Header("Fade (opcional)")]
    [Tooltip("Image negra full-screen para el corte suave entre cámaras")]
    public Image fadeImage;
    [Range(0f, 1f)]
    public float fadeDuration = 0.2f;

    [Header("Audio (opcional)")]
    [Tooltip("Clip al seleccionar. Usa SoundManager si está vacío.")]
    public AudioClip selectSound;

    // ── Estado interno ─────────────────────────────────────────────────────────
    private bool           _shopOpen    = false;
    private bool           _shopUsed    = false;
    private CursorLockMode _prevLock;
    private bool           _prevVisible;
    private AudioSource    _audio;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Asegurar que el panel empieza oculto
        if (shopPanel != null) shopPanel.SetActive(false);

        // Shop camera empieza desactivada
        if (shopCamera != null) shopCamera.enabled = false;

        // Audio
        _audio = GetComponent<AudioSource>();
        if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake  = false;
        _audio.spatialBlend = 0f;

        // Auto-detectar playerMovement si no está asignado
        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<thirdPersonMovement>();
        if (cameraController == null)
            cameraController = FindFirstObjectByType<CameraController>();
        if (explorationCamera == null)
            explorationCamera = Camera.main;

        // Suscribir los botones de los items
        WireItemButtons();
    }

    // ── Suscripción a botones ─────────────────────────────────────────────────

    private void WireItemButtons()
    {
        if (shopItems == null) return;

        for (int i = 0; i < shopItems.Length; i++)
        {
            var item = shopItems[i];
            if (item == null) continue;

            item.itemIndex = i;   // garantiza que el índice coincide con la posición del array

            // IMPORTANTE: item.button lo asigna ShopItem.Awake(), pero si el panel
            // empieza inactivo ese Awake aún no corrió → usamos GetComponent directamente,
            // que sí funciona en GOs inactivos, y sincronizamos la referencia cacheada.
            var btn = item.GetComponent<Button>();
            if (btn == null)
            {
                Debug.LogWarning($"[ShopInteraction] shopItems[{i}] no tiene componente Button.", item);
                continue;
            }
            item.button = btn;   // sync por si ShopItem lo necesita después

            int capturedIndex = i;   // closure-safe
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => StartCoroutine(Purchase(capturedIndex)));
        }
    }

    // ── Detección del jugador ─────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (_shopUsed || _shopOpen) return;

        StartCoroutine(OpenShop());
    }

    // ── Apertura ──────────────────────────────────────────────────────────────

    private IEnumerator OpenShop()
    {
        _shopOpen = true;

        // 1. Fade a negro
        yield return StartCoroutine(Fade(0f, 1f));

        // 2. Bloquear movimiento
        if (playerMovement  != null) playerMovement.enabled  = false;
        if (cameraController != null) cameraController.enabled = false;

        // 3. Swap de cámaras (solo si hay shopCamera)
        bool doSwap = shopCamera != null;
        if (doSwap)
        {
            if (explorationCamera != null) explorationCamera.enabled = false;
            shopCamera.enabled = true;
        }

        // 4. Mostrar el panel
        if (shopPanel != null) shopPanel.SetActive(true);

        // 5. Fade desde negro
        yield return StartCoroutine(Fade(1f, 0f));

        // 6. Mostrar cursor
        _prevLock    = Cursor.lockState;
        _prevVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        Debug.Log("[ShopInteraction] Tienda abierta. Esperando selección del jugador.");
    }

    // ── Compra ────────────────────────────────────────────────────────────────

    private IEnumerator Purchase(int itemIndex)
    {
        if (_shopUsed) yield break;   // guard contra doble click
        _shopUsed = true;

        // 1. Sonido
        PlaySelectSound();

        // Pequeña pausa para que el sonido suene
        yield return new WaitForSeconds(0.12f);

        // 2. Fade a negro
        yield return StartCoroutine(Fade(0f, 1f));

        // 3. Ocultar panel
        if (shopPanel != null) shopPanel.SetActive(false);

        // 4. Restaurar cursor
        Cursor.lockState = _prevLock;
        Cursor.visible   = _prevVisible;

        // 5. Swap de cámaras de vuelta
        if (shopCamera        != null) shopCamera.enabled        = false;
        if (explorationCamera != null) explorationCamera.enabled = true;

        // 6. Re-activar movimiento
        if (playerMovement  != null) playerMovement.enabled  = true;
        if (cameraController != null) cameraController.enabled = true;

        // 7. Fade desde negro
        yield return StartCoroutine(Fade(1f, 0f));

        // 8. Registrar en GameProgress (activa enemigos)
        GameProgress.Reset();
        GameProgress.CompleteShop(itemIndex);

        Debug.Log($"[ShopInteraction] Item {itemIndex} comprado. La tienda puede volver a usarse.");

        Debug.Log($"[ShopInteraction] Item {itemIndex} comprado. Trigger desactivado permanentemente.");

        // 9. Desactivar PERMANENTEMENTE
        _shopUsed = false;
        _shopOpen = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void PlaySelectSound()
    {
        if (selectSound != null && _audio != null)
        {
            _audio.PlayOneShot(selectSound);
            return;
        }
        SoundManager.Instance?.PlayUIClick();
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeImage == null || fadeDuration <= 0f) { yield return null; yield break; }

        fadeImage.gameObject.SetActive(true);
        SetFadeAlpha(from);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            SetFadeAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / fadeDuration)));
            yield return null;
        }

        SetFadeAlpha(to);
        if (to <= 0f) fadeImage.gameObject.SetActive(false);
    }

    private void SetFadeAlpha(float a)
    {
        if (fadeImage == null) return;
        var c = fadeImage.color; c.a = a; fadeImage.color = c;
    }
}
