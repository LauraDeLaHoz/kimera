using UnityEngine;

public enum ItemEffect { HealHealth, ExtraAction, RestoreEnergy, ReduceEnemyAccuracy, BoostDamage }

// Mantenido para compatibilidad con el QuestSystem
public enum ItemType { Food, CombatItem, Material }

[CreateAssetMenu(menuName = "Kimera/ItemData")]
public class ItemData : ScriptableObject
{
    public string itemName;
    public Sprite icon;
    [TextArea] public string description;
    public ItemEffect effect;
    public int value;
    public int quantity;
}
