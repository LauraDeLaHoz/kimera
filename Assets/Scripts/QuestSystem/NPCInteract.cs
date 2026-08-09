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

    private void Update()
    {
        if (!playerInside)
            return;

        if (inDialogue)
            return;

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            StartDialogue();
        }
    }

    void StartDialogue()
    {
        inDialogue = true;

        playerMovement.enabled = false;

        DialogEventsBinder.Instance.SetCurrentNPC(this);

        // mover c�mara
        dialogueCamera.transform.position =
            dialogueCameraPoint.position;

        dialogueCamera.transform.rotation =
            dialogueCameraPoint.rotation;

        gameplayCamera.gameObject.SetActive(false);

        dialogueCamera.gameObject.SetActive(true);

        dialogBehaviour.StartDialog(
            dialogGraph,
            null,
            null
        );
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
        {
            playerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;
        }
    }


    public void StartQuest()
    {
        QuestManager.Instance.StartQuest(
       questToStart);

        SpawnQuestPrefab();
    }

    void SpawnQuestPrefab()
    {
        if (questToStart == null)
            return;

        if (questToStart.questPrefab == null)
            return;

        if (questSpawnPoint == null)
            return;

        Instantiate(
            questToStart.questPrefab,
            questSpawnPoint.position +
            questToStart.spawnOffset,
            Quaternion.identity
        );
    }
}