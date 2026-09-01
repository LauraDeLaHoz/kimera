using System;
using System.Collections.Generic;
using UnityEngine;

public enum QuestState
{
    NotStarted,
    InProgress,
    Completed
}

/// <summary>
/// Controla el avance de las misiones "formales" (las que tienen tipo,
/// progreso y recompensas). No controla los beats narrativos ambientales
/// sueltos como "descubriste algo interesante" del carnicero: para eso
/// usa OneShotTrigger + StoryFlags, que son más livianos.
///
/// Diseñado para juego LINEAL: hay una sola quest "activa" a la vez
/// (currentQuest), igual que tu versión original, pero ahora:
///  - el estado no se escribe sobre el ScriptableObject
///  - no se puede reiniciar una quest que ya está en curso o completada
///  - dispara eventos para que la UI (y el diálogo) reaccionen solos
///  - puede autoencadenar la siguiente quest de la historia
/// </summary>
public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [Header("Debug (solo lectura)")]
    [SerializeField] private QuestData currentQuest;
    [SerializeField] private int currentProgress;

    private readonly HashSet<string> completedQuestIDs = new HashSet<string>();

    // La UI (NotificationManager) y el sistema de diálogo se suscriben a esto.
    // Así QuestManager no necesita saber que existe una UI: solo avisa.
    public event Action<QuestData> OnQuestStarted;
    public event Action<QuestData, int, int> OnQuestProgress; // quest, progreso actual, objetivo
    public event Action<QuestData> OnQuestCompleted;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public QuestState GetState(QuestData quest)
    {
        if (quest == null)
            return QuestState.NotStarted;

        if (completedQuestIDs.Contains(quest.questID))
            return QuestState.Completed;

        if (currentQuest == quest)
            return QuestState.InProgress;

        return QuestState.NotStarted;
    }

    public bool IsQuestActive(QuestData quest) => GetState(quest) == QuestState.InProgress;
    public bool IsQuestCompleted(QuestData quest) => GetState(quest) == QuestState.Completed;

    /// <summary>
    /// Inicia la quest. Si ya está en curso o completada, no hace nada
    /// (por eso hablar de nuevo con la guía NO puede reiniciar la misión,
    /// aunque el nodo de diálogo vuelva a llamar a este método por error).
    /// </summary>
    public bool StartQuest(QuestData quest)
    {
        if (quest == null)
            return false;

        if (GetState(quest) != QuestState.NotStarted)
        {
            Debug.Log($"QuestManager: '{quest.questName}' ya está iniciada o completada, se ignora StartQuest.");
            return false;
        }

        currentQuest = quest;
        currentProgress = 0;

        Debug.Log("Quest iniciada: " + quest.questName);

        OnQuestStarted?.Invoke(quest);
        return true;
    }

    /// <summary>Progreso por acción (comer, comprar, hablar, etc.) — igual que tu versión original.</summary>
    public void AddProgress(QuestType type, int amount = 1)
    {
        if (currentQuest == null || currentQuest.questType != type)
            return;

        if (GetState(currentQuest) != QuestState.InProgress)
            return;

        currentProgress += amount;

        OnQuestProgress?.Invoke(currentQuest, currentProgress, currentQuest.targetAmount);

        if (currentProgress >= currentQuest.targetAmount)
        {
            CompleteCurrentQuest();
        }
    }

    /// <summary>
    /// Para objetivos que no se completan por "cantidad" sino por evento externo
    /// (ej: el temporizador de "Explora la ciudad", o un trigger de zona).
    /// Solo completa si 'quest' es la quest activa en este momento.
    /// </summary>
    public void CompleteQuestManually(QuestData quest)
    {
        if (quest != currentQuest || GetState(quest) != QuestState.InProgress)
            return;

        CompleteCurrentQuest();
    }

    private void CompleteCurrentQuest()
    {
        QuestData finished = currentQuest;

        completedQuestIDs.Add(finished.questID);
        currentQuest = null;
        currentProgress = 0;

        Debug.Log("Quest completada: " + finished.questName);

        GiveRewards(finished);

        OnQuestCompleted?.Invoke(finished);

        if (finished.autoStartNextQuest != null)
        {
            StartQuest(finished.autoStartNextQuest);
        }
    }

    private void GiveRewards(QuestData quest)
    {
        if (quest.rewards == null)
            return;

        foreach (RewardData reward in quest.rewards)
        {
            switch (reward.rewardType)
            {
                case RewardType.Stones:
                    Debug.Log("Dar piedras: " + reward.amount);
                    break;

                case RewardType.Item:
                    Debug.Log("Dar item: " + reward.item.itemName);
                    break;
            }
        }
    }
}