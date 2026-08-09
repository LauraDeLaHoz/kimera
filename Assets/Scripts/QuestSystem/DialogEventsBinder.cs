using cherrydev;
using UnityEngine;

public class DialogEventsBinder : MonoBehaviour
{
    public static DialogEventsBinder Instance;

    public DialogBehaviour dialogBehaviour;

    private NPCInteract currentNPC;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        dialogBehaviour.BindExternalFunction(
            "StartQuest",
            StartQuest
        );

        dialogBehaviour.BindExternalFunction(
            "EndDialogue",
            EndDialogue
        );
    }

    public void SetCurrentNPC(
        NPCInteract npc)
    {
        currentNPC = npc;
    }

    void StartQuest()
    {
        if (currentNPC != null)
        {
            currentNPC.StartQuest();
        }
    }

    void EndDialogue()
    {
        if (currentNPC != null)
        {
            currentNPC.EndDialogueExternally();
        }
    }
}