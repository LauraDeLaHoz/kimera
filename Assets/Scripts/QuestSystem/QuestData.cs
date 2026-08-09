using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/Quest")]
public class QuestData : ScriptableObject
{
    [Header("Info")]
    public string questID;

    public string questName;

    [TextArea]
    public string description;

    [Header("Goal")]
    public QuestType questType;

    public int targetAmount = 1;

    [Header("Rewards")]
    public List<RewardData> rewards;

    [Header("Quest Spawn")]
    public GameObject questPrefab;

    public Vector3 spawnOffset;

    [HideInInspector]
    public bool completed;
}

public enum QuestType
{
    EatFood,
    BuyItem,
    TalkNPC,
    ReachLocation
}