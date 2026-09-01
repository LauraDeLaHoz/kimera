using cherrydev;
using UnityEngine;

/// <summary>
/// Punto único donde se registran las "external functions" que vas a poder
/// llamar desde CUALQUIER nodo de sentencia en el editor de NBDS (poniendo
/// el mismo nombre en "Func Name"). Mantené los nombres EXACTOS entre el
/// bind de acá y lo que escribas en el nodo.
/// </summary>
public class DialogEventsBinder : MonoBehaviour
{
    public static DialogEventsBinder Instance;

    public DialogBehaviour dialogBehaviour;

    [Header("Triggers narrativos referenciados desde diálogo")]
    [Tooltip("Ej: el OneShotTrigger de 'descubriste algo interesante' en la carnicería.")]
    public OneShotTrigger discoveryTrigger;

    [Tooltip("El secuenciador del motín, para llamarlo al terminar el diálogo con el vendedor.")]
    public NarrativeSequencer narrativeSequencer;

    private NPCInteract currentNPC;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        dialogBehaviour.BindExternalFunction("StartQuest", StartQuest);
        dialogBehaviour.BindExternalFunction("EndDialogue", EndDialogue);

        dialogBehaviour.BindExternalFunction("CompleteTalkNPC", CompleteTalkNPC);
        dialogBehaviour.BindExternalFunction("FireDiscovery", FireDiscovery);
        dialogBehaviour.BindExternalFunction("StartMotin", StartMotin);
    }

    public void SetCurrentNPC(NPCInteract npc)
    {
        currentNPC = npc;
    }

    // Rama "primer encuentro" del guía, al final del diálogo -> arranca la misión.
    private void StartQuest()
    {
        currentNPC?.StartQuest();
    }

    private void EndDialogue()
    {
        currentNPC?.EndDialogueExternally();
    }

    // Para quests de tipo TalkNPC: llamalo desde el nodo donde el NPC
    // "confirma" la conversación (no en cualquier línea, solo en la última).
    private void CompleteTalkNPC()
    {
        QuestManager.Instance.AddProgress(QuestType.TalkNPC, 1);
    }

    // Nodo del carnicero: "descubriste algo interesante". One-shot real,
    // aunque el jugador le hable varias veces, solo la primera cuenta.
    private void FireDiscovery()
    {
        discoveryTrigger?.Fire();
    }

    // Nodo donde el vendedor te da el ítem, justo antes de salir -> motín.
    private void StartMotin()
    {
        narrativeSequencer?.StartMotin();
    }
}