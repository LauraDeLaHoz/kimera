using UnityEngine;

[CreateAssetMenu(menuName = "Kimera/CharacterStats")]
public class CharacterStats : ScriptableObject
{
    public string characterName;
    public Sprite portrait;
    public int maxHealth = 100;
    public int maxEnergy = 80;
    public int attackPower = 20;
    public int defense = 5;
    public int speed = 10;
    public MutationData activeMutation; // solo jugador
}
