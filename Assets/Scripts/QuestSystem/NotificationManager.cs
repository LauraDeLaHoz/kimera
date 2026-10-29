using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Reemplaza a ObjectiveUI. Tres canales:
///
///  - OBJETIVO (panel persistente, ej. el banner "Encuentra al guía" o
///    "Explora la ciudad"): fade in y se queda visible mientras la misión
///    esté activa. Se oculta con fade out al completarla.
///
///  - LOGRO (toast naranja tipo "omg lograste!"): aparece, espera
///    'visibleTime' y se oculta solo. Si llegan varios seguidos se ENCOLAN.
///    Ahora puede traer un ÍCONO (item obtenido, mutación, etc.).
///
///  - GUÍA DE RUTA (texto "Dirígete a la carnicería..."): texto persistente
///    con fade in. Lo maneja cada RouteAlertIcon: cuando un NPC pasa a ser
///    "el siguiente punto", pide su texto; cuando se completa, lo quita.
///    Si llega otro texto mientras hay uno visible, hace fade out -> fade in.
///
/// No necesitas llamarlo manualmente desde tus scripts de misión: se
/// suscribe a QuestManager.OnQuestStarted / OnQuestCompleted en Start.
/// Los beats narrativos sueltos (OneShotTrigger) llaman a ShowAchievement().
/// </summary>
public class NotificationManager : MonoBehaviour
{
    public static NotificationManager Instance;

    [Header("Objetivo (panel persistente)")]
    public CanvasGroup objectiveGroup;
    public RectTransform objectivePanel;
    public TMP_Text objectiveText;
    public AudioClip objectiveSound;

    [Header("Logro (toast, se encola)")]
    public CanvasGroup achievementGroup;
    public RectTransform achievementPanel;
    public TMP_Text achievementText;
    public AudioClip achievementSound;
    [Tooltip("OPCIONAL. Image dentro del toast donde se muestra el ícono del logro/item " +
             "(el cuadro negro de tu mockup). Si el logro no trae ícono, este objeto se oculta solo.")]
    public Image achievementIcon;

    [Header("Guía de ruta (texto 'Dirígete a...')")]
    [Tooltip("OPCIONAL. CanvasGroup del texto guía (agregale un CanvasGroup al objeto del texto). " +
             "Si lo dejás vacío, la guía simplemente no se muestra.")]
    public CanvasGroup guideGroup;
    [Tooltip("RectTransform que se desliza. Si lo dejás vacío se usa el del propio Guide Group.")]
    public RectTransform guidePanel;
    public TMP_Text guideText;
    public AudioClip guideSound;
    [Tooltip("Cuánto se desliza el texto guía además del fade (0 = solo fade).")]
    public float guideSlideDistance = 20f;

    [Header("Animación")]
    [Tooltip("Ease in / ease out. Con esta curva controlas ambos tramos (0->1 entrando, 1->0 saliendo).")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float fadeDuration = 0.35f;
    public float achievementVisibleTime = 3f;
    [Tooltip("Cuánto se desliza el panel además del fade (0 = solo fade).")]
    public float slideDistance = 40f;

    private struct AchievementEntry
    {
        public string text;
        public Sprite icon;
    }

    private AudioSource audioSource;
    private Coroutine objectiveRoutine;
    private Coroutine guideRoutine;
    private readonly Queue<AchievementEntry> achievementQueue = new Queue<AchievementEntry>();
    private bool showingAchievement;

    private Vector2 objectiveShownPos;
    private Vector2 achievementShownPos;
    private Vector2 guideShownPos;

    // Quién pidió el texto guía actual (normalmente un RouteAlertIcon) y qué texto.
    private UnityEngine.Object guideOwner;
    private string currentGuideText;

    private void Awake()
    {
        Instance = this;
        audioSource = GetComponent<AudioSource>();

        objectiveShownPos = objectivePanel.anchoredPosition;
        achievementShownPos = achievementPanel.anchoredPosition;

        SetGroupHidden(objectiveGroup, objectivePanel, objectiveShownPos, slideDistance);
        SetGroupHidden(achievementGroup, achievementPanel, achievementShownPos, slideDistance);

        if (guideGroup != null)
        {
            if (guidePanel == null)
                guidePanel = guideGroup.GetComponent<RectTransform>();

            guideShownPos = guidePanel.anchoredPosition;
            SetGroupHidden(guideGroup, guidePanel, guideShownPos, guideSlideDistance);
        }

        if (achievementIcon != null)
            achievementIcon.gameObject.SetActive(false);
    }

    private void Start()
    {
        // OJO: esto va en Start(), NO en OnEnable(). Unity garantiza que
        // TODOS los Awake() de la escena corren antes que CUALQUIER Start(),
        // sin importar el orden de los GameObjects en la Hierarchy. Si esto
        // estuviera en OnEnable(), la suscripción podría fallar en silencio
        // dependiendo de si Canvas inicializa antes o después que GameManager
        // (justo el bug que causaba que "Explora la ciudad" no apareciera).
        if (QuestManager.Instance != null)
        {
            Subscribe(QuestManager.Instance);
        }
        else
        {
            Debug.LogError("NotificationManager: QuestManager.Instance es null en Start(). " +
                            "¿Existe un QuestManager en la escena y está activo?");
        }
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
            Unsubscribe(QuestManager.Instance);
    }

    private void Subscribe(QuestManager qm)
    {
        qm.OnQuestStarted += HandleQuestStarted;
        qm.OnQuestCompleted += HandleQuestCompleted;
    }

    private void Unsubscribe(QuestManager qm)
    {
        qm.OnQuestStarted -= HandleQuestStarted;
        qm.OnQuestCompleted -= HandleQuestCompleted;
    }

    private void HandleQuestStarted(QuestData quest)
    {
        ShowObjective(quest.description);
    }

    private void HandleQuestCompleted(QuestData quest)
    {
        HideObjective();

        string text = string.IsNullOrEmpty(quest.achievementText)
            ? "Completado: " + quest.questName
            : quest.achievementText;

        ShowAchievement(text);
    }

    // =========================
    // API pública
    // =========================

    public void ShowObjective(string text)
    {
        objectiveText.text = text;

        if (objectiveRoutine != null)
            StopCoroutine(objectiveRoutine);

        objectiveRoutine = StartCoroutine(FadeRoutine(objectiveGroup, objectivePanel, objectiveShownPos, true, objectiveSound, slideDistance));
    }

    public void HideObjective()
    {
        if (objectiveRoutine != null)
            StopCoroutine(objectiveRoutine);

        objectiveRoutine = StartCoroutine(FadeRoutine(objectiveGroup, objectivePanel, objectiveShownPos, false, null, slideDistance));
    }

    /// <summary>Logro sin ícono (se mantiene por compatibilidad: IntroSequence, UnityEvents, etc.).</summary>
    public void ShowAchievement(string text)
    {
        ShowAchievement(text, null);
    }

    /// <summary>Logro con ícono opcional (item obtenido, mutación, etc.).</summary>
    public void ShowAchievement(string text, Sprite icon)
    {
        achievementQueue.Enqueue(new AchievementEntry { text = text, icon = icon });

        if (!showingAchievement)
            StartCoroutine(ProcessAchievementQueue());
    }

    /// <summary>Muestra el texto guía (ej. "Dirígete a la carnicería...") con fade in.</summary>
    public void ShowGuide(string text)
    {
        ShowGuide(text, null);
    }

    /// <summary>
    /// 'owner' identifica quién pidió el texto (normalmente el RouteAlertIcon),
    /// para que un NPC que ya terminó no borre el texto del NPC siguiente.
    /// </summary>
    public void ShowGuide(string text, UnityEngine.Object owner)
    {
        if (guideGroup == null || guideText == null) return;
        if (string.IsNullOrEmpty(text)) return;

        // Mismo texto ya visible: no repetir animación.
        if (text == currentGuideText && guideGroup.alpha > 0.01f)
        {
            guideOwner = owner;
            return;
        }

        guideOwner = owner;
        currentGuideText = text;

        if (guideRoutine != null)
            StopCoroutine(guideRoutine);

        guideRoutine = StartCoroutine(GuideRoutine(text));
    }

    public void HideGuide()
    {
        HideGuide(null);
    }

    /// <summary>
    /// Oculta el texto guía. Si se pasa 'owner' y NO es quien lo mostró,
    /// se ignora (ya lo tomó otro NPC).
    /// </summary>
    public void HideGuide(UnityEngine.Object owner)
    {
        if (guideGroup == null) return;
        if (owner != null && owner != guideOwner) return;

        guideOwner = null;
        currentGuideText = null;

        if (guideRoutine != null)
            StopCoroutine(guideRoutine);

        guideRoutine = StartCoroutine(FadeRoutine(guideGroup, guidePanel, guideShownPos, false, null, guideSlideDistance));
    }

    // =========================
    // Internals
    // =========================

    private IEnumerator GuideRoutine(string text)
    {
        // Si ya había un texto visible, sale primero y después entra el nuevo.
        if (guideGroup.alpha > 0.01f)
            yield return FadeRoutine(guideGroup, guidePanel, guideShownPos, false, null, guideSlideDistance);

        guideText.text = text;
        yield return FadeRoutine(guideGroup, guidePanel, guideShownPos, true, guideSound, guideSlideDistance);
    }

    private IEnumerator ProcessAchievementQueue()
    {
        showingAchievement = true;

        while (achievementQueue.Count > 0)
        {
            AchievementEntry entry = achievementQueue.Dequeue();
            achievementText.text = entry.text;

            if (achievementIcon != null)
            {
                achievementIcon.sprite = entry.icon;
                achievementIcon.gameObject.SetActive(entry.icon != null);
            }

            yield return FadeRoutine(achievementGroup, achievementPanel, achievementShownPos, true, achievementSound, slideDistance);
            yield return new WaitForSeconds(achievementVisibleTime);
            yield return FadeRoutine(achievementGroup, achievementPanel, achievementShownPos, false, null, slideDistance);
        }

        showingAchievement = false;
    }

    private IEnumerator FadeRoutine(CanvasGroup group, RectTransform panel, Vector2 shownPos, bool fadeIn, AudioClip clip, float slide)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);

        float from = group.alpha;
        float to = fadeIn ? 1f : 0f;

        Vector2 hiddenPos = shownPos + Vector2.down * slide;
        Vector2 startPos = fadeIn ? hiddenPos : shownPos;
        Vector2 endPos = fadeIn ? shownPos : hiddenPos;

        if (fadeIn)
            group.blocksRaycasts = true;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime; // unscaled: sigue funcionando si el juego llega a pausarse
            float k = ease.Evaluate(Mathf.Clamp01(t / fadeDuration));

            group.alpha = Mathf.Lerp(from, to, k);
            panel.anchoredPosition = Vector2.Lerp(startPos, endPos, k);

            yield return null;
        }

        group.alpha = to;
        panel.anchoredPosition = endPos;

        if (!fadeIn)
            group.blocksRaycasts = false;
    }

    private void SetGroupHidden(CanvasGroup group, RectTransform panel, Vector2 shownPos, float slide)
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        panel.anchoredPosition = shownPos + Vector2.down * slide;
    }
}