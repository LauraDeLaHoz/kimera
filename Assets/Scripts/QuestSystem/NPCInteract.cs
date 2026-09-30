using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using cherrydev;

public class NPCInteract : MonoBehaviour
{
    [Header("Dialogue")]
    public DialogBehaviour dialogBehaviour;
    public DialogNodeGraph dialogGraph;

    [Header("Player")]
    public MonoBehaviour playerMovement;

    [Header("Camera")]
    public Camera gameplayCamera;
    public Camera dialogueCamera;
    public Transform dialogueCameraPoint;

    [Header("Cinemática ANTES del diálogo (opcional)")]
    public PlayableDirector preDialogueCinematic;

    [Header("Cinemática DESPUÉS del diálogo (opcional)")]
    public PlayableDirector postDialogueCinematic;

    [Tooltip("Cámara que usan las cinemáticas de arriba (ej. _CinematicCamera).")]
    public Camera cinematicCamera;

    private bool playerInside;
    private bool inDialogue;

    [Header("Quest")]
    public QuestData questToStart;
    public Transform questSpawnPoint;

    [Header("Variable NBDS (opcional) — estado de QUEST formal")]
    public string questStateVariableName;

    [Header("Variable NBDS (opcional) — STORY FLAG propio")]
    [Tooltip("El flag que marca si YA completaste ESTE punto (ej. 'tienda_completada'). " +
             "También se usa para saber cuándo apagar el ícono de alerta sobre este NPC.")]
    public string storyFlagId;
    [Tooltip("Variable int en el grafo: 0 = primera vez, 1 = ya lo hiciste antes.")]
    public string storyFlagVariableName;

    [Header("Requisito previo (bloquea la interacción, NO toca el diálogo)")]
    [Tooltip("El flag del PUNTO ANTERIOR de la cadena (ej. si esto es la Estatua, " +
             "poné 'tienda_completada'). Mientras este flag no esté puesto, presionar " +
             "E acá NO HACE NADA — ni siquiera abre el diálogo. Dejalo vacío si este " +
             "punto no depende de ningún otro (ej. la Tienda, el primero de la cadena).")]
    public string prerequisiteFlagId;

    /// <summary>Público para que RouteAlertIcon pueda leerlo sin duplicar el flag id.</summary>
    public bool PrerequisiteMet =>
        string.IsNullOrEmpty(prerequisiteFlagId) ||
        (StoryFlags.Instance != null && StoryFlags.Instance.IsSet(prerequisiteFlagId));

    /// <summary>Público para que RouteAlertIcon sepa si este punto ya se completó.</summary>
    public bool AlreadyCompleted =>
        !string.IsNullOrEmpty(storyFlagId) &&
        StoryFlags.Instance != null && StoryFlags.Instance.IsSet(storyFlagId);

    private void Update()
    {
        if (!playerInside || inDialogue)
            return;

        // Bloqueo puro: si el punto anterior de la cadena no está cumplido,
        // presionar E acá no hace absolutamente nada — sin diálogo, sin
        // mensaje, sin tocar el grafo para nada.
        if (!PrerequisiteMet)
            return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartDialogue();
        }
    }

    public void StartDialogue()
    {
        inDialogue = true;
        playerMovement.enabled = false;

        if (preDialogueCinematic != null)
        {
            StartCoroutine(PlayPreDialogueCinematicThenBegin());
        }
        else
        {
            BeginActualDialogue();
        }
    }

    private IEnumerator PlayPreDialogueCinematicThenBegin()
    {
        if (cinematicCamera != null)
            cinematicCamera.gameObject.SetActive(true);

        if (gameplayCamera != null)
            gameplayCamera.gameObject.SetActive(false);

        bool finished = false;
        void OnStopped(PlayableDirector d) => finished = true;

        preDialogueCinematic.extrapolationMode = DirectorWrapMode.None;
        preDialogueCinematic.stopped += OnStopped;
        preDialogueCinematic.Play();

        yield return new WaitUntil(() => finished);

        preDialogueCinematic.stopped -= OnStopped;
        preDialogueCinematic.enabled = false;

        if (cinematicCamera != null)
            cinematicCamera.gameObject.SetActive(false);

        BeginActualDialogue();
    }

    private void BeginActualDialogue()
    {
        DialogEventsBinder.Instance.SetCurrentNPC(this);

        dialogueCamera.transform.position = dialogueCameraPoint.position;
        dialogueCamera.transform.rotation = dialogueCameraPoint.rotation;

        if (gameplayCamera != null)
            gameplayCamera.gameObject.SetActive(false);

        dialogueCamera.gameObject.SetActive(true);

        // Las variables se setean DENTRO del callback 'onVariablesHandlerInitialized' —
        // nunca antes de StartDialog (ver explicación completa en versiones anteriores
        // de este archivo). Ya NO sincronizamos ningún flag de "requisito previo" acá
        // — eso ahora se resuelve ANTES de llegar a este método, en Update().
        dialogBehaviour.StartDialog(
            dialogGraph,
            onVariablesHandlerInitialized: _ =>
            {
                SyncQuestStateVariable();
                SyncStoryFlagVariable();
            },
            onDialogFinished: null
        );
    }

    private void SyncQuestStateVariable()
    {
        if (string.IsNullOrEmpty(questStateVariableName) || questToStart == null)
            return;

        int state = (int)QuestManager.Instance.GetState(questToStart);
        dialogBehaviour.SetVariableValue(questStateVariableName, state);
    }

    private void SyncStoryFlagVariable()
    {
        if (string.IsNullOrEmpty(storyFlagVariableName) || string.IsNullOrEmpty(storyFlagId))
            return;

        bool alreadySet = StoryFlags.Instance != null && StoryFlags.Instance.IsSet(storyFlagId);
        dialogBehaviour.SetVariableValue(storyFlagVariableName, alreadySet ? 1 : 0);
    }

    public void EndDialogueExternally()
    {
        if (postDialogueCinematic != null)
        {
            StartCoroutine(PlayPostCinematicThenEndDialogue());
            return;
        }

        FinishDialogue();
    }

    private IEnumerator PlayPostCinematicThenEndDialogue()
    {
        if (gameplayCamera != null)
            gameplayCamera.gameObject.SetActive(false);

        dialogueCamera.gameObject.SetActive(false);

        if (cinematicCamera != null)
            cinematicCamera.gameObject.SetActive(true);

        bool finished = false;
        void OnStopped(PlayableDirector d) => finished = true;

        postDialogueCinematic.extrapolationMode = DirectorWrapMode.None;
        postDialogueCinematic.stopped += OnStopped;
        postDialogueCinematic.Play();

        yield return new WaitUntil(() => finished);

        postDialogueCinematic.stopped -= OnStopped;
        postDialogueCinematic.enabled = false;

        if (cinematicCamera != null)
            cinematicCamera.gameObject.SetActive(false);

        FinishDialogue();
    }

    private void FinishDialogue()
    {
        inDialogue = false;
        playerMovement.enabled = true;

        if (gameplayCamera != null)
            gameplayCamera.gameObject.SetActive(true);

        dialogueCamera.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }

    public void StartQuest()
    {
        bool started = QuestManager.Instance.StartQuest(questToStart);

        if (started)
            SpawnQuestPrefab();
    }

    private void SpawnQuestPrefab()
    {
        if (questToStart == null || questToStart.questPrefab == null || questSpawnPoint == null)
            return;

        Instantiate(
            questToStart.questPrefab,
            questSpawnPoint.position + questToStart.spawnOffset,
            Quaternion.identity
        );
    }
}