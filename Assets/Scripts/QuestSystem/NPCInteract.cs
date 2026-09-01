using UnityEngine;
using UnityEngine.InputSystem;
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

    private bool playerInside;
    private bool inDialogue;

    [Header("Quest")]
    public QuestData questToStart;
    public Transform questSpawnPoint;

    [Header("Variable NBDS (opcional)")]
    [Tooltip("Nombre de la variable int en el Variable Config del diálogo. " +
             "Se setea automáticamente con el estado de questToStart (0=NotStarted, 1=InProgress, 2=Completed) " +
             "antes de abrir el diálogo, para poder ramificar con un Variable Condition Node " +
             "y así NO repetir la conversación de introducción si ya hablaste antes.")]
    public string questStateVariableName;

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

    public void EndDialogueExternally()
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