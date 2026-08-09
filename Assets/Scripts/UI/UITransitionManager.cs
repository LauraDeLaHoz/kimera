using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class UITransitionManager : MonoBehaviour
{
    [Header("Paneles")]
    public GameObject panelInicio;
    public GameObject panelFinal;

    [Header("Overlay")]
    public Image overlayImage;
    public float fadeDuration = 0.5f;

    [Header("Waypoints")]
    public Transform waypointA;
    public Transform waypointB;
    public Transform waypointC;

    [Header("Tiempos por tramo")]
    public float durationAtoB = 1.2f;
    public float durationBtoC = 1.8f;
    public float targetFOV = 35f;
    private float originalFOV;

    [Header("Audio")]
    public UIAudioManager audioManager;

    Camera cam;

    // ─── Init ──────────────────────────────────────────

    void Awake()
    {
        cam = Camera.main;
        originalFOV = cam.fieldOfView;
    }

    void Start()
    {
        panelInicio.SetActive(true);
        panelFinal.SetActive(false);
        overlayImage.color = Color.black;
        StartCoroutine(Fade(0f));
    }

    // ─── API pública ───────────────────────────────────

    public void TransitionToFinal()
    {
        audioManager?.PlayClick();
        StartCoroutine(DoTransitionForward());
    }

    public void TransitionToStart()
    {
        audioManager?.PlayClick();
        StartCoroutine(DoTransitionBackward());
    }

    public void FadeOutThenLoad(string sceneName)
    {
        audioManager?.PlayClick();
        StartCoroutine(DoFadeLoad(sceneName));
    }

    // ─── Coroutinas ────────────────────────────────────

    IEnumerator DoTransitionForward()
    {
        yield return StartCoroutine(Fade(1f));
        panelInicio.SetActive(false);
        yield return StartCoroutine(Fade(0f));

        // Tramo 1: WP1 → WP2 (sube, sin cambio de FOV)
        yield return StartCoroutine(AnimateCamera(
            cam.transform.position, waypointB.position,
            cam.transform.rotation, waypointB.rotation,
            cam.fieldOfView, cam.fieldOfView,
            durationAtoB));

        // Tramo 2: WP2 → WP3 (zoom in hacia la ventana)
        yield return StartCoroutine(AnimateCamera(
            cam.transform.position, waypointC.position,
            cam.transform.rotation, waypointC.rotation,
            cam.fieldOfView, targetFOV,
            durationBtoC));

        yield return StartCoroutine(Fade(1f));
        panelFinal.SetActive(true);
        yield return StartCoroutine(Fade(0f));
    }

    IEnumerator DoTransitionBackward()
    {
        yield return StartCoroutine(Fade(1f));
        panelFinal.SetActive(false);
        yield return StartCoroutine(Fade(0f));

        // Tramo 1: WP3 → WP2 (reverso zoom)
        yield return StartCoroutine(AnimateCamera(
            cam.transform.position, waypointB.position,
            cam.transform.rotation, waypointB.rotation,
            cam.fieldOfView, originalFOV,
            durationBtoC));

        // Tramo 2: WP2 → WP1 (baja de vuelta)
        yield return StartCoroutine(AnimateCamera(
            cam.transform.position, waypointA.position,
            cam.transform.rotation, waypointA.rotation,
            cam.fieldOfView, originalFOV,
            durationAtoB));

        yield return StartCoroutine(Fade(1f));
        panelInicio.SetActive(true);
        yield return StartCoroutine(Fade(0f));
    }

    IEnumerator DoFadeLoad(string sceneName)
    {
        yield return StartCoroutine(Fade(1f));
        SceneManager.LoadScene(sceneName);
    }

    IEnumerator Fade(float target)
    {
        float start = overlayImage.color.a;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            overlayImage.color = new Color(0, 0, 0, Mathf.Lerp(start, target, t));
            yield return null;
        }

        overlayImage.color = new Color(0, 0, 0, target);
    }

    IEnumerator AnimateCamera(
        Vector3 fromPos, Vector3 toPos,
        Quaternion fromRot, Quaternion toRot,
        float fromFOV, float toFOV,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / duration));

            cam.transform.position = Vector3.Lerp(fromPos, toPos, t);
            cam.transform.rotation = Quaternion.Lerp(fromRot, toRot, t);
            cam.fieldOfView = Mathf.Lerp(fromFOV, toFOV, t);

            yield return null;
        }

        cam.transform.position = toPos;
        cam.transform.rotation = toRot;
        cam.fieldOfView = toFOV;
    }

    float EaseInOut(float t) => t * t * (3f - 2f * t);
}