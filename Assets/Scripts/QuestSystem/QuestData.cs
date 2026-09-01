using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/Quest")]
public class QuestData : ScriptableObject
{
    [Header("Info")]
    public string questID;

    public string questName;

    [TextArea]
    [Tooltip("Texto que se muestra en el panel de OBJETIVO mientras la misión está en curso. Ej: 'Encuentra al guía'")]
    public string description;

    [TextArea]
    [Tooltip("Texto del toast de LOGRO al completarla. Si lo dejas vacío se usa 'Completado: ' + questName. Ej: 'Disfruta tu estancia en Kimera City'")]
    public string achievementText;

    [Header("Goal")]
    public QuestType questType;

    public int targetAmount = 1;

    [Header("Rewards")]
    public List<RewardData> rewards;

    [Header("Quest Spawn")]
    public GameObject questPrefab;

    public Vector3 spawnOffset;

    [Header("Narrativa lineal")]
    [Tooltip("Si se asigna, QuestManager la inicia automáticamente en cuanto esta termina. Útil para encadenar la historia sin scripts extra por cada paso.")]
    public QuestData autoStartNextQuest;

    // NOTA: el viejo campo "completed" se eliminó a propósito.
    // El estado de la misión (NotStarted/InProgress/Completed) ya no vive
    // en el asset: lo controla QuestManager en runtime. Esto evita que el
    // ScriptableObject quede "recordando" progreso entre partidas/sesiones.
}

public enum QuestType
{
    EatFood,
    BuyItem,
    TalkNPC,
    ReachLocation,
    Explore // nuevo: para objetivos ambientales basados en tiempo, no en acción directa
}