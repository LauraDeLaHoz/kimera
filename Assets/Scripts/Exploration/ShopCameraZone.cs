using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// ShopCameraZone
// ─────────────────────────────────────────────────────────────────────────────
// Swap de cámaras al entrar en la zona de la tienda.
//
// DISEÑO:
//   · "Exploration Camera" → cámara principal que sigue al jugador (se APAGA).
//   · "Shop Camera"        → cámara fija que encuadra la tienda  (se ENCIENDE).
//   Las dos cámaras son GameObjects independientes; este script solo cambia
//   cuál está activa. No mueve ninguna cámara en runtime.
//
// FLUJO:
//   Jugador entra al trigger
//     → fade a negro (opcional)
//     → Exploration Camera disabled
//     → Shop Camera enabled
//     → fade a transparente
//
//   Jugador sale del trigger
//     → fade a negro (opcional)
//     → Shop Camera disabled
//     → Exploration Camera enabled
//     → fade a transparente
//
// SETUP (o ejecuta Kimera/14 para hacerlo automáticamente):
//   1. Crea un GO "ShopCamera" en la escena, ponle un componente Camera,
//      pósicionalo y rótalo frente a la tienda.  Desactívalo (enabled=false).
//   2. En este trigger, asigna:
//      · Exploration Camera → la Main Camera (la que sigue al player)
//      · Shop Camera        → el GO "ShopCamera" que creaste
//      · Fade Image         → (opcional) Image negra full-screen para el corte suave
// ─────────────────────────────────────────────────────────────────────────────
public class ShopCameraZone : MonoBehaviour
{
    [Header("Cámaras")]
    [Tooltip("La cámara de exploración (sigue al jugador). Se desactiva al entrar.")]
    public Camera explorationCamera;
    [Tooltip("La cámara nueva dedicada a la tienda. Se activa al entrar.\n" +
             "Debe estar ya posicionada mirando la tienda y con enabled = false.")]
    public Camera shopCamera;

    [Header("Fade (opcional)")]
    [Tooltip("Image negra full-screen para el corte suave entre cámaras.\n" +
             "Déjalo vacío para hacer el swap instantáneo.")]
    public Image fadeImage;
    [Tooltip("Duración del fade de entrada/salida en segundos")]
    [Range(0f, 1f)]
    public float fadeDuration = 0.25f;

    // ── Estado ─────────────────────────────────────────────────────────────────
    private bool      _insideZone = false;
    private Coroutine _swapRoutine;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Auto-detectar la cámara de exploración si no está asignada
        if (explorationCamera == null)
            explorationCamera = Camera.main;

        // La shop camera arranca siempre desactivada
        if (shopCamera != null)
            shopCamera.enabled = false;

        // La fade image arranca invisible e inactiva
        if (fadeImage != null)
        {
            SetFadeAlpha(0f);
            fadeImage.gameObject.SetActive(false);
        }
    }

    // ── Detección del jugador ─────────────────────────────────────────────────

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") || _insideZone) return;
        _insideZone = true;

        if (_swapRoutine != null) StopCoroutine(_swapRoutine);
        _swapRoutine = StartCoroutine(SwapToShop());
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") || !_insideZone) return;
        _insideZone = false;

        if (_swapRoutine != null) StopCoroutine(_swapRoutine);
        _swapRoutine = StartCoroutine(SwapToExploration());
    }

    // ── Swap: exploración → tienda ────────────────────────────────────────────

    private IEnumerator SwapToShop()
    {
        if (shopCamera == null)
        {
            Debug.LogWarning("[ShopCameraZone] 'Shop Camera' no asignada.", this);
            yield break;
        }

        // Fade a negro
        yield return StartCoroutine(Fade(0f, 1f));

        // Swap
        if (explorationCamera != null) explorationCamera.enabled = false;
        shopCamera.enabled = true;

        // Fade a transparente
        yield return StartCoroutine(Fade(1f, 0f));
    }

    // ── Swap: tienda → exploración ────────────────────────────────────────────

    private IEnumerator SwapToExploration()
    {
        // Fade a negro
        yield return StartCoroutine(Fade(0f, 1f));

        // Swap
        if (shopCamera != null) shopCamera.enabled = false;
        if (explorationCamera != null) explorationCamera.enabled = true;

        // Fade a transparente
        yield return StartCoroutine(Fade(1f, 0f));
    }

    // ── Corrutina de fade ─────────────────────────────────────────────────────

    private IEnumerator Fade(float from, float to)
    {
        if (fadeImage == null || fadeDuration <= 0f)
        {
            // Sin fade: espera un frame para que el swap sea visible
            yield return null;
            yield break;
        }

        fadeImage.gameObject.SetActive(true);
        SetFadeAlpha(from);

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            SetFadeAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration)));
            yield return null;
        }

        SetFadeAlpha(to);
        if (to <= 0f) fadeImage.gameObject.SetActive(false);
    }

    private void SetFadeAlpha(float a)
    {
        if (fadeImage == null) return;
        var c = fadeImage.color;
        c.a = a;
        fadeImage.color = c;
    }

    // ── Gizmo ─────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Muestra el frustum de la shop camera en azul
        if (shopCamera != null)
        {
            Gizmos.color  = new Color(0.3f, 0.8f, 1f, 0.85f);
            Gizmos.matrix = shopCamera.transform.localToWorldMatrix;
            Gizmos.DrawFrustum(Vector3.zero,
                               shopCamera.fieldOfView,
                               shopCamera.farClipPlane * 0.3f,
                               shopCamera.nearClipPlane,
                               shopCamera.aspect);
            Gizmos.matrix = Matrix4x4.identity;
        }

        // Línea desde el trigger hasta la shop camera
        if (shopCamera != null)
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            Gizmos.DrawLine(transform.position, shopCamera.transform.position);
        }
    }
}
