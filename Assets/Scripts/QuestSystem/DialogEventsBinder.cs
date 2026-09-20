using cherrydev;
using UnityEngine;

/// <summary>
/// Punto único donde se registran las "external functions" que vas a poder
/// llamar desde CUALQUIER nodo de sentencia en el editor de NBDS (poniendo
/// el mismo nombre en "Func Name"). Mantené los nombres EXACTOS entre el
/// bind de acá y lo que escribas en el nodo.
///
/// Los 5 puntos del recorrido (carnicero, tienda/plantas, monumento, motín,
/// guía) tienen cada uno su propio OneShotTrigger y su propio external
/// function — así cada diálogo dispara SOLO el suyo, sin ambigüedad.
/// </summary>
public class DialogEventsBinder : MonoBehaviour
{
    public static DialogEventsBinder Instance;

    public DialogBehaviour dialogBehaviour;

    [Header("Puntos del recorrido de exploración")]
    [Tooltip("Carnicero: entrega la mutación/Instinto (cola de iguana).")]
    public OneShotTrigger carniceriaTrigger;

    [Tooltip("Tienda / contexto de plantas: entrega el primer ítem de combate " +
             "y muestra 'Obtuviste tu primer ítem!'.")]
    public OneShotTrigger tiendaTrigger;

    [Tooltip("Monumento: contexto de Khemia, entrega los estimulantes mentales.")]
    public OneShotTrigger monumentoTrigger;

    [Tooltip("Personas del motín: contexto + cinemática, entrega los vendajes " +
             "y completa 'Explora la ciudad'.")]
    public OneShotTrigger motinTrigger;

    [Tooltip("Guía: entrega las pastillas de energía al encontrarla.")]
    public OneShotTrigger guiaTrigger;

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

        dialogBehaviour.BindExternalFunction("FireCarniceria", FireCarniceria);
        dialogBehaviour.BindExternalFunction("FireTienda", FireTienda);
        dialogBehaviour.BindExternalFunction("FireMonumento", FireMonumento);
        dialogBehaviour.BindExternalFunction("FireMotin", FireMotin);
        dialogBehaviour.BindExternalFunction("FireGuia", FireGuia);
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

    // Nodo del carnicero, justo donde entrega la cola de iguana.
    private void FireCarniceria()
    {
        carniceriaTrigger?.Fire();
    }

    // Nodo de la tienda/plantas, justo donde entrega el item de combate.
    private void FireTienda()
    {
        tiendaTrigger?.Fire();
    }

    // Nodo del monumento, justo donde entrega los estimulantes.
    private void FireMonumento()
    {
        monumentoTrigger?.Fire();
    }

    // Nodo del motín, justo donde entrega los vendajes (la cinemática la
    // maneja NPCInteract.postDialogueCinematic automáticamente al llamar EndDialogue).
    private void FireMotin()
    {
        motinTrigger?.Fire();
    }

    // Nodo de la guía, justo donde entrega las pastillas de energía.
    private void FireGuia()
    {
        guiaTrigger?.Fire();
    }
}