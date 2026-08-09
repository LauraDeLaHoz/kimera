using UnityEngine;

public class TutorialEvents : MonoBehaviour
{
    [Header("Quest")]
    public QuestData questToStart;

    [Header("NPC")]
    public NPCInteract npcInteract;

    [Header("Spawn")]
    public Transform spawnPoint;

    public void StartQuest()
    {
        QuestManager.Instance.StartQuest(
            questToStart
        );

        // SPAWN DEL PREFAB
        if (questToStart.questPrefab != null
            && spawnPoint != null)
        {
            Instantiate(
                questToStart.questPrefab,
                spawnPoint.position,
                spawnPoint.rotation
            );
        }
    }

    public void EndDialogue()
    {
        npcInteract.EndDialogueExternally();
    }
}
