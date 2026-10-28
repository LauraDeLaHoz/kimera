using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Post-combate:
//   Victoria → vuelve a exploración (sin triggers).
//   Derrota  → panel "Perdiste" con Reintentar combate / Reiniciar juego.
[DefaultExecutionOrder(-50)]
public class PostCombatFlow : MonoBehaviour
{
    [System.Serializable]
    public class PCF_DefeatScreen
    {
        [Tooltip("Raíz de la pantalla. Empieza inactiva.")]
        public GameObject root;
        public Image artworkSlot;
        [Tooltip("Reintentar combate")]
        public Button retryButton;
        [Tooltip("Reiniciar juego")]
        public Button homeButton;
    }

    [Header("Escena inicial (botón 'Reiniciar juego')")]
    [SerializeField] private string startSceneName = "SampleScene";

    [Header("Canvas de overlays (_CombatCanvas)")]
    [SerializeField] private Canvas postCombatCanvas;

    [Header("Pantalla de derrota")]
    [SerializeField] private PCF_DefeatScreen defeatScreenRefs = new PCF_DefeatScreen();
    [SerializeField] private Image defeatArtworkImage;

    // ── Lifecycle ──────────────────────────────────────────────────────────────
    private IEnumerator Start()
    {
        while (CombatManager.Instance == null) yield return null;
        CombatManager.Instance.onVictory += OnVictory;
        CombatManager.Instance.onDefeat += OnDefeat;
    }

    private void OnDestroy()
    {
        if (CombatManager.Instance == null) return;
        CombatManager.Instance.onVictory -= OnVictory;
        CombatManager.Instance.onDefeat -= OnDefeat;
    }

    // ── Handlers ───────────────────────────────────────────────────────────────
    private void OnVictory() { StartCoroutine(ReturnAfterVictory()); }
    private void OnDefeat() { StartCoroutine(ShowDefeatScreen()); }

    private IEnumerator ReturnAfterVictory()
    {
        yield return new WaitForSeconds(1.2f);
        if (InSceneCombatController.Instance != null)
            InSceneCombatController.Instance.ReturnToExploration();
    }

    // ── Derrota ────────────────────────────────────────────────────────────────
    private void RetryCombat()
    {
        Time.timeScale = 1f;
        if (InSceneCombatController.Instance != null)
            InSceneCombatController.Instance.RetryCombat();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void RestartGame()
    {
        Time.timeScale = 1f;
        LevelUpData.IsBossFight = false;   // estáticos: si no, sobreviven a la recarga
        LevelUpData.PendingLevelUp = false;
        SceneManager.LoadScene(startSceneName);
    }

    private IEnumerator ShowDefeatScreen()
    {
        yield return new WaitForSeconds(1.0f);

        // ── Pantalla pre-construida ────────────────────────────────────────────
        if (defeatScreenRefs.root != null)
        {
            defeatScreenRefs.root.SetActive(true);
            yield return StartCoroutine(FadeIn(defeatScreenRefs.root, 0.4f));

            if (defeatScreenRefs.artworkSlot != null)
            {
                Sprite art = GetSprite(defeatArtworkImage);
                if (defeatScreenRefs.artworkSlot.sprite == null && art != null)
                    defeatScreenRefs.artworkSlot.sprite = art;
                defeatScreenRefs.artworkSlot.enabled = defeatScreenRefs.artworkSlot.sprite != null;
            }

            if (defeatScreenRefs.retryButton != null)
            {
                defeatScreenRefs.retryButton.onClick.RemoveAllListeners();
                defeatScreenRefs.retryButton.onClick.AddListener(() => {
                    defeatScreenRefs.root.SetActive(false);
                    RetryCombat();
                });
            }

            if (defeatScreenRefs.homeButton != null)
            {
                defeatScreenRefs.homeButton.onClick.RemoveAllListeners();
                defeatScreenRefs.homeButton.onClick.AddListener(() => {
                    defeatScreenRefs.root.SetActive(false);
                    RestartGame();
                });
            }
            yield break;
        }

        // ── Fallback dinámico ──────────────────────────────────────────────────
        Canvas canvas = GetOverlayCanvas();
        if (canvas == null) yield break;

        GameObject overlay = BuildOverlay(canvas.transform, new Color(0.07f, 0f, 0f, 0.95f));
        yield return StartCoroutine(FadeIn(overlay, 0.4f));

        Lbl(overlay.transform, "Perdiste.",
            40, FontStyles.Bold, new Color(1f, 0.3f, 0.2f),
            0f, 0.60f, 1f, 0.85f).alignment = TextAlignmentOptions.Center;

        Btn(overlay.transform, "Reintentar combate", new Color(0.15f, 0.35f, 0.55f),
            0.08f, 0.35f, 0.92f, 0.50f,
            () => { Destroy(overlay); RetryCombat(); });

        Btn(overlay.transform, "Reiniciar juego", new Color(0.22f, 0.22f, 0.22f),
            0.25f, 0.15f, 0.75f, 0.28f,
            () => { Destroy(overlay); RestartGame(); });
    }

    // ── Helpers ────────────────────────────────────────────────────────────────
    private static Sprite GetSprite(Image img) { return img == null ? null : img.sprite; }

    private static IEnumerator FadeIn(GameObject go, float duration)
    {
        if (go == null) yield break;
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        float t = 0f;
        while (t < duration) { t += Time.unscaledDeltaTime; cg.alpha = Mathf.Clamp01(t / duration); yield return null; }
        cg.alpha = 1f;
    }

    private Canvas GetOverlayCanvas()
    {
        if (postCombatCanvas != null) return postCombatCanvas;
        CombatUI cui = FindFirstObjectByType<CombatUI>();
        if (cui != null) { Canvas c = cui.GetComponentInParent<Canvas>(); if (c != null) return c; }
        return FindFirstObjectByType<Canvas>();
    }

    private static GameObject BuildOverlay(Transform parent, Color color)
    {
        GameObject go = new GameObject("PostCombatOverlay");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
        go.transform.SetAsLastSibling();
        return go;
    }

    private static void Btn(Transform parent, string text, Color color,
        float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject("Btn");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
        go.AddComponent<Button>().onClick.AddListener(action);
        Lbl(go.transform, text, 17, FontStyles.Bold, Color.white, 0f, 0f, 1f, 1f, 8, 4, -8, -4)
            .alignment = TextAlignmentOptions.Center;
    }

    private static TextMeshProUGUI Lbl(Transform parent, string text,
        int size, FontStyles style, Color color,
        float x0, float y0, float x1, float y1,
        float ox0 = 0, float oy0 = 0, float ox1 = 0, float oy1 = 0)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
        rt.offsetMin = new Vector2(ox0, oy0); rt.offsetMax = new Vector2(ox1, oy1);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style;
        tmp.color = color; tmp.enableWordWrapping = true;
        return tmp;
    }
}