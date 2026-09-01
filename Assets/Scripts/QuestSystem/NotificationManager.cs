using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Reemplaza a ObjectiveUI. Dos canales, como en tu mockup:
///
///  - OBJETIVO (panel persistente, ej. la caja morada/oscura "Encuentra al guía"
///    o "Explora la ciudad"): se muestra con fade in y se queda visible mientras
///    la misión esté activa. Se oculta con fade out al completarla.
///
///  - LOGRO (toast naranja tipo "Bienvenido a Kimera City!" / "Disfruta tu
///    estancia..."): aparece, espera 'visibleTime' y se oculta solo. Si llegan
///    varios logros seguidos se ENCOLAN en vez de pisarse.
///
/// No necesitas llamarlo manualmente desde tus scripts de misión: se
/// suscribe a QuestManager.OnQuestStarted / OnQuestCompleted en OnEnable.
/// Los beats narrativos sueltos (OneShotTrigger) sí lo llaman directo con
/// ShowAchievement().
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

    [Header("Animación")]
    [Tooltip("Ease in / ease out. Con esta curva controlas ambos tramos (0->1 entrando, 1->0 saliendo).")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float fadeDuration = 0.35f;
    public float achievementVisibleTime = 3f;
    [Tooltip("Cuánto se desliza el panel además del fade (0 = solo fade).")]
    public float slideDistance = 40f;

    private AudioSource audioSource;
    private Coroutine objectiveRoutine;
    private readonly Queue<string> achievementQueue = new Queue<string>();
    private bool showingAchievement;

    private Vector2 objectiveShownPos;
    private Vector2 achievementShownPos;

    private void Awake()
    {
        Instance = this;
        audioSource = GetComponent<AudioSource>();

        objectiveShownPos = objectivePanel.anchoredPosition;
        achievementShownPos = achievementPanel.anchoredPosition;

        SetGroupHidden(objectiveGroup, objectivePanel, objectiveShownPos);
        SetGroupHidden(achievementGroup, achievementPanel, achievementShownPos);
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

        objectiveRoutine = StartCoroutine(FadeRoutine(objectiveGroup, objectivePanel, objectiveShownPos, true, objectiveSound));
    }

    public void HideObjective()
    {
        if (objectiveRoutine != null)
            StopCoroutine(objectiveRoutine);

        objectiveRoutine = StartCoroutine(FadeRoutine(objectiveGroup, objectivePanel, objectiveShownPos, false, null));
    }

    public void ShowAchievement(string text)
    {
        achievementQueue.Enqueue(text);

        if (!showingAchievement)
            StartCoroutine(ProcessAchievementQueue());
    }

    // =========================
    // Internals
    // =========================

    private IEnumerator ProcessAchievementQueue()
    {
        showingAchievement = true;

        while (achievementQueue.Count > 0)
        {
            string text = achievementQueue.Dequeue();
            achievementText.text = text;

            yield return FadeRoutine(achievementGroup, achievementPanel, achievementShownPos, true, achievementSound);
            yield return new WaitForSeconds(achievementVisibleTime);
            yield return FadeRoutine(achievementGroup, achievementPanel, achievementShownPos, false, null);
        }

        showingAchievement = false;
    }

    private IEnumerator FadeRoutine(CanvasGroup group, RectTransform panel, Vector2 shownPos, bool fadeIn, AudioClip clip)
    {
        if (clip != null)
            audioSource.PlayOneShot(clip);

        float from = group.alpha;
        float to = fadeIn ? 1f : 0f;

        Vector2 hiddenPos = shownPos + Vector2.down * slideDistance;
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

    private void SetGroupHidden(CanvasGroup group, RectTransform panel, Vector2 shownPos)
    {
        group.alpha = 0f;
        group.blocksRaycasts = false;
        panel.anchoredPosition = shownPos + Vector2.down * slideDistance;
    }
}