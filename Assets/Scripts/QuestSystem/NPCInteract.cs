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

    [Header("Cinemática post-diálogo (opcional)")]
    [Tooltip("Para puntos tipo 'diálogo - cinemática' (ej. el motín): si se " +
             "asigna, se reproduce automáticamente al llamar a EndDialogue " +
             "desde el grafo, ANTES de devolver el control al jugador. " +
             "Dejalo vacío para el comportamiento normal (sin cinemática).")]
    public PlayableDirector postDialogueCinematic;

    private bool playerInside;
    private bool inDialogue;

    [Header("Quest")]
    public QuestData questToStart;
    public Transform questSpawnPoint;

    [Header("Variable NBDS (opcional) — estado de QUEST formal")]
    [Tooltip("Nombre de la variable int en el Variable Config del diálogo. " +
             "Se setea automáticamente con el estado de questToStart (0=NotStarted, 1=InProgress, 2=Completed) " +
             "antes de abrir el diálogo, para poder ramificar con un Variable Condition Node " +
             "y así NO repetir la conversación de introducción si ya hablaste antes.")]
    public string questStateVariableName;

    [Header("Variable NBDS (opcional) — STORY FLAG suelto")]
    [Tooltip("Para NPCs cuyo 'ya pasó o no' NO es una quest formal (ej. el carnicero " +
             "que entrega un ítem una sola vez). Poné acá el mismo flagId que usa el " +
             "OneShotTrigger de ese evento (ej. 'carniceria_completada').")]
    public string storyFlagId;

    [Tooltip("Nombre de la variable int en el Variable Config del diálogo para este " +
             "story flag: 0 = todavía no pasó, 1 = ya pasó. Ramificá con un Variable " +
             "Condition Node igual que con questStateVariableName.")]
    public string storyFlagVariableName;

    private void Update()
    {
        if (!playerInside || inDialogue)
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

        DialogEventsBinder.Instance.SetCurrentNPC(this);

        SyncQuestStateVariable();
        SyncStoryFlagVariable();

        dialogueCamera.transform.position = dialogueCameraPoint.position;
        dialogueCamera.transform.rotation = dialogueCameraPoint.rotation;

        gameplayCamera.gameObject.SetActive(false);
        dialogueCamera.gameObject.SetActive(true);

        dialogBehaviour.StartDialog(dialogGraph, null, null);
    }

    /// <summary>
    /// Deja en la variable NBDS el estado actual de la quest asociada a este
    /// NPC. En el grafo, poné un Variable Condition Node justo al inicio que
    /// compare esta variable:
    ///   0 (NotStarted)  -> rama de "primer encuentro" (termina llamando a StartQuest)
    ///   1 (InProgress)  -> rama de "recordatorio" (solo repite el objetivo actual)
    ///   2 (Completed)   -> rama de diálogo post-misión
    /// Así hablarle de nuevo a la guía NUNCA vuelve a disparar StartQuest.
    /// </summary>
    private void SyncQuestStateVariable()
    {
        if (string.IsNullOrEmpty(questStateVariableName) || questToStart == null)
            return;

        int state = (int)QuestManager.Instance.GetState(questToStart);
        dialogBehaviour.SetVariableValue(questStateVariableName, state);
    }

    /// <summary>
    /// Igual que SyncQuestStateVariable, pero para eventos narrativos sueltos
    /// que viven en StoryFlags (OneShotTrigger) en vez de en QuestManager.
    /// Ej: el carnicero, que entrega un ítem una sola vez y no es una quest
    /// formal con panel de objetivo.
    ///   0 = el flag todavía NO está puesto -> rama de "primera vez" (entrega el ítem)
    ///   1 = el flag YA está puesto -> rama de "ya te di el ítem, no me queda más"
    /// </summary>
    private void SyncStoryFlagVariable()
    {
        if (string.IsNullOrEmpty(storyFlagVariableName) || string.IsNullOrEmpty(storyFlagId))
            return;

        bool alreadySet = StoryFlags.Instance != null && StoryFlags.Instance.IsSet(storyFlagId);
        dialogBehaviour.SetVariableValue(storyFlagVariableName, alreadySet ? 1 : 0);
    }

    /// <summary>
    /// Llamar desde el external function "EndDialogue" del grafo (el de
    /// siempre). Si este NPC tiene una cinemática post-diálogo asignada
    /// (ej. el motín), la reproduce primero y recién al terminar devuelve el
    /// control — todo transparente para el grafo, no hace falta un external
    /// function distinto para los puntos que la usan.
    /// </summary>
    public void EndDialogueExternally()
    {
        if (postDialogueCinematic != null)
        {
            StartCoroutine(PlayCinematicThenEndDialogue());
            return;
        }

        FinishDialogue();
    }

    private IEnumerator PlayCinematicThenEndDialogue()
    {
        gameplayCamera.gameObject.SetActive(false);
        dialogueCamera.gameObject.SetActive(false);

        postDialogueCinematic.Play();
        yield return new WaitForSeconds((float)postDialogueCinematic.duration);

        FinishDialogue();
    }

    private void FinishDialogue()
    {
        inDialogue = false;
        playerMovement.enabled = true;

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

    /// <summary>
    /// Se llama desde un external function del nodo de diálogo (solo en la
    /// rama de "primer encuentro"). QuestManager.StartQuest ya es idempotente,
    /// pero mantener la llamada solo en esa rama evita incluso intentarlo.
    /// </summary>
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