using UnityEngine;

public enum EnemyType { Tutorial, Standard, MiniBoss }
public enum WeaknessType { HeavyAttack, Interrupt, Pressure, None }

[CreateAssetMenu(menuName = "Kimera/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName;
    public Sprite sprite;
    public EnemyType enemyType;
    public int maxHealth;
    public int attackPower;
    public int defense;
    public int speed;
    [TextArea] public string analysisDescription;
    [TextArea] public string weaknessHint;
    public WeaknessType weakness;
    public EnemySkill[] skills;

    [Header("Animación idle en la UI de combate")]
    [Tooltip("Frames de la animación idle para el display de combate (en orden). " +
             "Si está vacío se usa el sprite estático de arriba.")]
    public Sprite[] idleAnimationFrames;
    [Range(1f, 24f)]
    [Tooltip("Velocidad de la animación idle (frames por segundo)")]
    public float animationFps = 7f;
}

[System.Serializable]
public class EnemySkill
{
    public string skillName;
    public int damage;
}
