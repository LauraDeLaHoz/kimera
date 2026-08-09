using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum CombatState { Initializing, PlayerTurn, EnemyTurn, Victory, Defeat, Animating }

[DefaultExecutionOrder(-100)]
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    private PlayerCombatant _player;
    private List<EnemyCombatant> _enemies;
    private List<ICombatant> _turnOrder;
    private int _currentTurnIndex = -1;

    public CombatState State { get; private set; } = CombatState.Initializing;

    // UI hooks — subscribe in CombatUI (Parte 3)
    public event Action<string> onShowAnalysisPanel;
    public event Action<string> onActionPerformed;
    public event Action onPlayerTurnStarted;
    public event Action onVictory;
    public event Action onDefeat;

    // Feedback de daño — suscritos en CombatHitFeedback
    public event Action<EnemyCombatant> onEnemyTookDamage;
    public event Action<int>            onPlayerTookDamage;   // int = daño recibido

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Initialization ────────────────────────────────────────────────────────

    public void InitializeCombat(CharacterStats playerStats, List<EnemyData> enemyDataList)
    {
        _player  = new PlayerCombatant(playerStats);
        _enemies = enemyDataList.Select(d => new EnemyCombatant(d)).ToList();
        BuildTurnOrder();
        StartNextTurn();
    }

    private void BuildTurnOrder()
    {
        _turnOrder = new List<ICombatant> { _player };
        _turnOrder.AddRange(_enemies);
        _turnOrder.Sort((a, b) => b.Speed.CompareTo(a.Speed));
        _currentTurnIndex = -1;
    }


    private void StartNextTurn()
    {
        _currentTurnIndex = (_currentTurnIndex + 1) % _turnOrder.Count;

        int attempts = 0;
        while (!_turnOrder[_currentTurnIndex].IsAlive)
        {
            _currentTurnIndex = (_currentTurnIndex + 1) % _turnOrder.Count;
            if (++attempts > _turnOrder.Count) return;
        }

        ICombatant current = _turnOrder[_currentTurnIndex];

        if (current is PlayerCombatant player)
        {
            player.OnTurnStart();
            State = CombatState.PlayerTurn;
            onPlayerTurnStarted?.Invoke();
        }
        else if (current is EnemyCombatant enemy)
        {
            State = CombatState.EnemyTurn;
            StartCoroutine(ProcessEnemyTurn(enemy));
        }
    }

    private void EndCurrentTurn()
    {
        if (State == CombatState.Victory || State == CombatState.Defeat) return;
        _player.HasExtraAction = false;
        StartNextTurn();
    }


    public void PlayerAttack(EnemyCombatant target)
    {
        if (State != CombatState.PlayerTurn) return;

        int damage = CalculateDamage(_player.GetAttackPower(), target.Data.defense);

        if (target.WeaknessExposed)
        {
            switch (target.Data.weakness)
            {
                case WeaknessType.HeavyAttack:
                    damage = Mathf.RoundToInt(damage * 1.5f);
                    break;
                case WeaknessType.Interrupt:
                    target.IsPreparing = false;
                    break;
                case WeaknessType.Pressure:
                    damage = Mathf.RoundToInt(damage * 1.3f);
                    break;
            }
        }

        target.TakeDamage(damage);
        onEnemyTookDamage?.Invoke(target);
        onActionPerformed?.Invoke($"Mike ataca a {target.Data.enemyName}: -{damage} HP");
        HungerSystem.Instance?.DrainOnAction(5f);   // atacar gasta 5 de hambre

        CheckCombatEnd();
        if (State != CombatState.Victory && State != CombatState.Defeat)
            EndCurrentTurn();
    }

    public void PlayerUseInstinct(EnemyCombatant target)
    {
        if (State != CombatState.PlayerTurn) return;

        MutationData mutation = _player.Data.activeMutation;
        if (mutation == null) return;
        if (!_player.SpendEnergy(mutation.energyCost)) return;

        target.WeaknessExposed = true;
        target.WeaknessTimer   = mutation.weaknessWindowTurns;

        int enemyIndex = _enemies.IndexOf(target);
        string text = (enemyIndex >= 0 && enemyIndex < mutation.analysisTexts.Length)
            ? mutation.analysisTexts[enemyIndex]
            : target.Data.analysisDescription;

        onShowAnalysisPanel?.Invoke(text);
        onActionPerformed?.Invoke($"Mike usa Instinto — {target.Data.enemyName} analizado");
        HungerSystem.Instance?.DrainOnAction(8f);   // usar instinto gasta 8 de hambre
        EndCurrentTurn();
    }

    public void PlayerDefend()
    {
        if (State != CombatState.PlayerTurn) return;
        _player.IsDefending = true;
        _player.RecoverEnergy(15);
        onActionPerformed?.Invoke("Mike se pone en guardia. (bloquea 50 % del daño — +15 energía)");
        HungerSystem.Instance?.DrainOnAction(2f);   // defender gasta poco hambre
        EndCurrentTurn();
    }

    public void PlayerUseItem(ItemData item)
    {
        if (State != CombatState.PlayerTurn) return;

        bool endTurn = true;

        switch (item.effect)
        {
            case ItemEffect.HealHealth:
                _player.Heal(item.value);
                onActionPerformed?.Invoke($"Usaste {item.itemName}: +{item.value} HP");
                break;

            case ItemEffect.ExtraAction:
                _player.HasExtraAction = true;
                endTurn = false;
                onActionPerformed?.Invoke($"Usaste {item.itemName}: acción extra concedida");
                break;

            case ItemEffect.RestoreEnergy:
                _player.RecoverEnergy(item.value);
                onActionPerformed?.Invoke($"Usaste {item.itemName}: +{item.value} energía");
                break;

            case ItemEffect.ReduceEnemyAccuracy:
                float reduction = 1f - item.value / 100f;
                foreach (EnemyCombatant e in _enemies.Where(e => e.IsAlive))
                    e.AccuracyModifier *= reduction;
                onActionPerformed?.Invoke($"Usaste {item.itemName}: precisión enemiga reducida");
                break;

            case ItemEffect.BoostDamage:
                _player.ApplyDamageBoost(item.value, 2);
                onActionPerformed?.Invoke($"Usaste {item.itemName}: +{item.value} daño por 2 turnos");
                break;
        }

        HungerSystem.Instance?.DrainOnAction(2f);   // usar ítem gasta 2 de hambre
        if (endTurn)
            EndCurrentTurn();
    }

    // ── Enemy turn ────────────────────────────────────────────────────────────

    private IEnumerator ProcessEnemyTurn(EnemyCombatant enemy)
    {
        yield return new WaitForSeconds(0.8f);

        enemy.OnTurnStart();
        EnemyActionResult action = enemy.DecideAction(_player);

        State = CombatState.Animating;
        onActionPerformed?.Invoke($"{enemy.Data.enemyName}: {action.actionName}");

        if (!action.isDodge && !action.isDefend && action.damage > 0)
        {
            if (UnityEngine.Random.value <= enemy.AccuracyModifier)
            {
                _player.TakeDamage(action.damage);
                onPlayerTookDamage?.Invoke(action.damage);
            }
            else
            {
                onActionPerformed?.Invoke($"{enemy.Data.enemyName} falló el ataque");
            }
        }

        yield return new WaitForSeconds(0.4f);

        CheckCombatEnd();
        if (State != CombatState.Victory && State != CombatState.Defeat)
            EndCurrentTurn();
    }

    // ── Combat end ────────────────────────────────────────────────────────────

    private void CheckCombatEnd()
    {
        if (!_player.IsAlive)
        {
            State = CombatState.Defeat;
            onDefeat?.Invoke();
            return;
        }

        if (_enemies.All(e => !e.IsAlive))
        {
            State = CombatState.Victory;
            onVictory?.Invoke();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private int CalculateDamage(int attack, int defense)
    {
        return Mathf.Max(1, attack - defense + UnityEngine.Random.Range(-3, 4));
    }

    public PlayerCombatant Player => _player;
    public IReadOnlyList<EnemyCombatant> Enemies => _enemies;
}
