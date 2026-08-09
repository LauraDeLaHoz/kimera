using UnityEngine;

public class EnemyActionResult
{
    public string actionName;
    public int damage;
    public bool isDodge;
    public bool isDefend;
}

public class EnemyCombatant : ICombatant
{
    private readonly EnemyData _data;
    private bool _actionToggle;

    public int CurrentHealth { get; private set; }
    public int CurrentDefense { get; private set; }
    public float AccuracyModifier { get; set; } = 1f;
    public bool IsPreparing { get; set; }
    public bool WeaknessExposed { get; set; }
    public float WeaknessTimer { get; set; }

    public int Speed => _data.speed;
    public bool IsAlive => CurrentHealth > 0;
    public EnemyData Data => _data;

    public EnemyCombatant(EnemyData data)
    {
        _data = data;
        CurrentHealth = data.maxHealth;
        CurrentDefense = data.defense;
    }

    public void TakeDamage(int amount)
    {
        int reduced = Mathf.Max(1, amount - CurrentDefense);
        CurrentHealth = Mathf.Max(0, CurrentHealth - reduced);
        CurrentDefense = _data.defense;
    }

    public void OnTurnStart()
    {
        if (WeaknessTimer > 0f)
        {
            WeaknessTimer -= 1f;
            if (WeaknessTimer <= 0f)
                WeaknessExposed = false;
        }
    }

    public EnemyActionResult DecideAction(PlayerCombatant player)
    {
        return _data.enemyType switch
        {
            EnemyType.Tutorial  => DecideRabbitAction(),
            EnemyType.Standard  => DecideBoarAction(),
            EnemyType.MiniBoss  => DecideHyenaAction(player),
            _                   => DecideRabbitAction()
        };
    }

    private EnemyActionResult DecideRabbitAction()
    {
        float healthPercent = (float)CurrentHealth / _data.maxHealth;

        if (healthPercent >= 0.5f)
        {
            _actionToggle = !_actionToggle;
            return _actionToggle
                ? new EnemyActionResult { actionName = "Esquivar", damage = 0, isDodge = true }
                : new EnemyActionResult { actionName = "Ataque Rápido", damage = _data.attackPower };
        }

        return new EnemyActionResult
        {
            actionName = "Ataque Frenético",
            damage = Mathf.RoundToInt(_data.attackPower * 1.3f)
        };
    }

    private EnemyActionResult DecideBoarAction()
    {
        if (IsPreparing)
        {
            IsPreparing = false;
            return new EnemyActionResult
            {
                actionName = "Carga",
                damage = Mathf.RoundToInt(_data.attackPower * 1.8f)
            };
        }

        if (Random.value < 0.6f)
        {
            IsPreparing = true;
            return new EnemyActionResult { actionName = "Preparando Carga...", damage = 0 };
        }

        return new EnemyActionResult { actionName = "Pisotón", damage = _data.attackPower };
    }

    private EnemyActionResult DecideHyenaAction(PlayerCombatant player)
    {
        float playerHealthPercent = (float)player.CurrentHealth / player.MaxHealth;
        float ownHealthPercent    = (float)CurrentHealth / _data.maxHealth;

        if (playerHealthPercent < 0.4f)
        {
            return new EnemyActionResult
            {
                actionName = "Ataque Agresivo",
                damage = Mathf.RoundToInt(_data.attackPower * 1.6f)
            };
        }

        if (ownHealthPercent < 0.3f)
        {
            CurrentDefense = Mathf.RoundToInt(_data.defense * 2f);
            return new EnemyActionResult { actionName = "Defender", damage = 0, isDefend = true };
        }

        return new EnemyActionResult { actionName = "Ataque Normal", damage = _data.attackPower };
    }
}
