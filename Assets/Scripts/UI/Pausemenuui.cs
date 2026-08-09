using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menú de pausa. Settings es hijo del mismo panel.
/// La transición entre vistas es un crossfade (fade out + fade in).
///
/// Jerarquía:
///   Overlay          (Image negra + CanvasGroup)
///   PausePanel       (RectTransform)
///   ├── PauseContent    (GameObject + CanvasGroup — los 4 botones)
///   └── SettingsContent (GameObject + CanvasGroup — título + slider)
///
/// Campos en el Inspector:
///   pausePanel        → RectTransform del panel contenedor
///   pauseContent      → CanvasGroup de la vista de pausa
///   settingsContent   → CanvasGroup de la vista de settings
///   overlay           → CanvasGroup del overlay oscuro
///   openSound         → AudioClip al abrir
///   closeSound        → AudioClip al cerrar / back
///   audioSource       → AudioSource de la UI
/// </summary>
public class PauseMenuUI : MonoBehaviour
{
    [Header("Panel principal")]
    [SerializeField] private RectTransform pausePanel;

    [Header("Contenidos internos (CanvasGroup en cada uno)")]
    [SerializeField] private CanvasGroup pauseContent;
    [SerializeField] private CanvasGroup settingsContent;

    [Header("Overlay")]
    [SerializeField] private CanvasGroup overlay;

    [Header("Sonidos")]
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip closeSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Animación")]
    [SerializeField] private float slideDuration = 0.35f;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float overlayTargetAlpha = 0.55f;

    private Vector2 panelHiddenPos;
    private Vector2 panelVisiblePos = Vector2.zero;

    private bool isAnimating = false;

    private enum MenuState { Closed, PauseOpen, SettingsOpen }
    private MenuState currentState = MenuState.Closed;

    private void Awake()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        float offscreen = canvas != null
            ? canvas.GetComponent<RectTransform>().rect.width + 100f
            : Screen.width + 100f;

        panelHiddenPos = new Vector2(-offscreen, 0f);

        // Estado inicial del panel
        pausePanel.anchoredPosition = panelHiddenPos;
        pausePanel.gameObject.SetActive(false);

        // PauseContent visible, interactuable
        SetCanvasGroup(pauseContent, 1f, true);

        // SettingsContent invisible y bloqueado
        SetCanvasGroup(settingsContent, 0f, false);

        if (overlay != null)
        {
            overlay.alpha = 0f;
            overlay.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (isAnimating) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            switch (currentState)
            {
                case MenuState.Closed: StartCoroutine(OpenMenu()); break;
                case MenuState.PauseOpen: StartCoroutine(CloseMenu()); break;
                case MenuState.SettingsOpen: StartCoroutine(SettingsBack()); break;
            }
        }
    }

    // ── Botones públicos ─────────────────────────────────────────────────────

    public void OnResumeButton()
    {
        if (isAnimating || currentState != MenuState.PauseOpen) return;
        StartCoroutine(CloseMenu());
    }

    public void OnSettingsButton()
    {
        if (isAnimating || currentState != MenuState.PauseOpen) return;
        StartCoroutine(OpenSettings());
    }

    public void OnSettingsBackButton()
    {
        if (isAnimating || currentState != MenuState.SettingsOpen) return;
        StartCoroutine(SettingsBack());
    }

    public void OnMainMenuButton()
    {
        if (isAnimating) return;
        StartCoroutine(GoToMainMenu());
    }

    public void OnQuitButton()
    {
        if (isAnimating) return;
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ── Coroutines ───────────────────────────────────────────────────────────

    private IEnumerator OpenMenu()
    {
        isAnimating = true;
        currentState = MenuState.PauseOpen;

        Time.timeScale = 0f;

        // Asegura estado correcto antes de animar
        SetCanvasGroup(pauseContent, 1f, true);
        SetCanvasGroup(settingsContent, 0f, false);

        pausePanel.anchoredPosition = panelHiddenPos;
        pausePanel.gameObject.SetActive(true);
        if (overlay != null) overlay.gameObject.SetActive(true);

        PlaySound(openSound);

        yield return AnimatePanel(panelHiddenPos, panelVisiblePos, 0f, overlayTargetAlpha);

        isAnimating = false;
    }

    private IEnumerator CloseMenu()
    {
        isAnimating = true;

        PlaySound(closeSound);

        yield return AnimatePanel(panelVisiblePos, panelHiddenPos, overlayTargetAlpha, 0f);

        pausePanel.gameObject.SetActive(false);
        if (overlay != null) overlay.gameObject.SetActive(false);

        Time.timeScale = 1f;
        currentState = MenuState.Closed;
        isAnimating = false;
    }

    private IEnumerator OpenSettings()
    {
        isAnimating = true;

        PlaySound(openSound);

        // Fade out de pause
        yield return FadeCanvasGroup(pauseContent, 1f, 0f);
        SetCanvasGroup(pauseContent, 0f, false);

        // Fade in de settings
        SetCanvasGroup(settingsContent, 0f, false); // empieza transparente pero ya interactuable
        yield return FadeCanvasGroup(settingsContent, 0f, 1f);
        SetCanvasGroup(settingsContent, 1f, true);

        currentState = MenuState.SettingsOpen;
        isAnimating = false;
    }

    private IEnumerator SettingsBack()
    {
        isAnimating = true;

        PlaySound(closeSound);

        // Fade out de settings
        yield return FadeCanvasGroup(settingsContent, 1f, 0f);
        SetCanvasGroup(settingsContent, 0f, false);

        // Fade in de pause
        yield return FadeCanvasGroup(pauseContent, 0f, 1f);
        SetCanvasGroup(pauseContent, 1f, true);

        currentState = MenuState.PauseOpen;
        isAnimating = false;
    }

    private IEnumerator GoToMainMenu()
    {
        isAnimating = true;

        PlaySound(closeSound);

        float fromAlpha = overlay != null ? overlay.alpha : 0f;
        yield return AnimatePanel(panelVisiblePos, panelHiddenPos, fromAlpha, 0f);

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // ── Helpers de animación (unscaled) ──────────────────────────────────────

    private IEnumerator AnimatePanel(Vector2 from, Vector2 to, float alphaFrom, float alphaTo)
    {
        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / slideDuration);
            pausePanel.anchoredPosition = Vector2.Lerp(from, to, t);
            if (overlay != null) overlay.alpha = Mathf.Lerp(alphaFrom, alphaTo, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        pausePanel.anchoredPosition = to;
        if (overlay != null) overlay.alpha = alphaTo;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to)
    {
        float elapsed = 0f;
        cg.alpha = from;
        while (elapsed < fadeDuration)
        {
            float t = elapsed / fadeDuration;
            cg.alpha = Mathf.Lerp(from, to, t);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        cg.alpha = to;
    }

    // ── Utilidades ────────────────────────────────────────────────────────────

    /// alpha, interactable y blocksRaycasts de un CanvasGroup de una vez
    private void SetCanvasGroup(CanvasGroup cg, float alpha, bool interactable)
    {
        cg.alpha = alpha;
        cg.interactable = interactable;
        cg.blocksRaycasts = interactable;
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }
}
