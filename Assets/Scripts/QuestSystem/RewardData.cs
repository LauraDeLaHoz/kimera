using UnityEngine;

[System.Serializable]
public class RewardData
{
    public RewardType rewardType;

    public int amount;

    public ItemData item;
}

public enum RewardType
{
    Stones,
    Item
}