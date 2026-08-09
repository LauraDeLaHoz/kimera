using UnityEngine;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance;

    [Header("Current Quest")]
    public QuestData currentQuest;

    private int currentProgress;


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

    public void StartQuest(QuestData quest)
    {
        if (quest == null)
            return;

        currentQuest = quest;

        currentQuest.completed = false;

        currentProgress = 0;

        ObjectiveUI.Instance.ShowObjective(
            quest.description
        );


        Debug.Log(
            "Quest iniciada: " +
            quest.questName
        );
    }

    // AGREGAR PROGRESO
    public void AddProgress(
        QuestType type,
        int amount = 1)
    {
        if (currentQuest == null)
            return;

        if (currentQuest.completed)
            return;

        if (currentQuest.questType != type)
            return;

        currentProgress += amount;

        Debug.Log(
            "Progreso: " +
            currentProgress
        );

        if (currentProgress >=
            currentQuest.targetAmount)
        {
            CompleteQuest();
        }
    }

    void CompleteQuest()
    {
        currentQuest.completed = true;

        ObjectiveUI.Instance.ShowAchievement(
            currentQuest.questName
        );

        ObjectiveUI.Instance.HideObjective();

        GiveRewards();

        Debug.Log(
            "Quest completada: " +
            currentQuest.questName
        );
    }

    void GiveRewards()
    {
        foreach (RewardData reward
            in currentQuest.rewards)
        {
            switch (reward.rewardType)
            {
                case RewardType.Stones:

                    Debug.Log(
                        "Dar piedras: " +
                        reward.amount
                    );

                    break;

                case RewardType.Item:

                    Debug.Log(
                        "Dar item: " +
                        reward.item.itemName
                    );

                    break;
            }
        }
    }



}