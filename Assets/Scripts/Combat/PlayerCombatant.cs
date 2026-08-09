using UnityEngine;

public class PlayerCombatant : ICombatant
{
    private readonly CharacterStats _stats;
    private float _hungerPercent = 1f;

    // Bonificaciones de nivel aplicadas en runtime (no modifican el ScriptableObject)
    private int _bonusMaxHP;
    private int _bonusAttack;
    private int _bonusMaxEnergy;

    public int CurrentHealth { get; private set; }
    public int CurrentEnergy { get; private set; }
    public int CurrentDefense { get; private set; }
    public bool IsDefending { get; set; }
    public bool HasExtraAction { get; set; }
    public int DamageBoostTurns { get; private set; }
    public int DamageBoostAmount { get; private set; }

    public int Speed => _stats.speed;
    public bool IsAlive => CurrentHealth > 0;
    public int MaxHealth => _stats.maxHealth + _bonusMaxHP;
    public CharacterStats Data => _stats;

    public float HungerPercent
    {
        get => _hungerPercent;
        set => _hungerPercent = Mathf.Clamp01(value);
    }

    public PlayerCombatant(CharacterStats stats)
    {
        _stats = stats;
        CurrentHealth = stats.maxHealth;
        CurrentEnergy = stats.maxEnergy;
        CurrentDefense = stats.defense;
    }

    public void TakeDamage(int amount)
    {
        int reduced;
        if (IsDefending)
            // Defending blocks 50 % of incoming damage
            reduced = Mathf.Max(1, Mathf.RoundToInt(amount * 0.50f));
        else
            reduced = Mathf.Max(1, amount - CurrentDefense);

        CurrentHealth = Mathf.Max(0, CurrentHealth - reduced);
        IsDefending = false;
        CurrentDefense = _stats.defense;
    }

    public void Heal(int amount)
    {
        CurrentHealth = Mathf.Min(_stats.maxHealth, CurrentHealth + amount);
    }

    public bool SpendEnergy(int amount)
    {
        if (CurrentEnergy < amount) return false;
        CurrentEnergy -= amount;
        return true;
    }

    public void RecoverEnergy(int amount)
    {
        CurrentEnergy = Mathf.Min(_stats.maxEnergy + _bonusMaxEnergy, CurrentEnergy + amount);
    }

    public void OnTurnStart()
    {
        RecoverEnergy(3);
        if (DamageBoostTurns > 0)
            DamageBoostTurns--;
        IsDefending = false;
        ApplyHungerEffects();
    }

    public int GetAttackPower()
    {
        float power = _stats.attackPower + _bonusAttack;
        if (DamageBoostTurns > 0)
            power += DamageBoostAmount;
        if (_hungerPercent < 0.30f)
            power *= 0.9f;
        return Mathf.RoundToInt(power);
    }

    // Llamar desde CombatSceneInitializer si LevelUpData.PendingLevelUp == true
    public void ApplyLevelUpBonus(int bonusHp, int bonusAtk, int bonusEnergy)
    {
        _bonusMaxHP     = bonusHp;
        _bonusAttack    = bonusAtk;
        _bonusMaxEnergy = bonusEnergy;

        // Curar por los HP ganados y rellenar la energía extra
        CurrentHealth = Mathf.Min(CurrentHealth + bonusHp, MaxHealth);
        CurrentEnergy = Mathf.Min(_stats.maxEnergy + bonusEnergy, CurrentEnergy + bonusEnergy);
    }

    public void ApplyDamageBoost(int amount, int turns)
    {
        DamageBoostAmount = amount;
        DamageBoostTurns = turns;
    }

    public void ApplyHungerEffects()
    {
        if (_hungerPercent < 0.30f)
            CurrentEnergy = Mathf.Max(0, CurrentEnergy - 5);
        else if (_hungerPercent < 0.60f)
            CurrentEnergy = Mathf.Max(0, CurrentEnergy - 2);
    }
}
