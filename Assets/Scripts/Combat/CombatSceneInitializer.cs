using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Punto de entrada de CombatScene.
// Adjuntar a un GameObject vacío "_Initializer" en la escena de combate.
[DefaultExecutionOrder(10)]
public class CombatSceneInitializer : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private CharacterStats playerStats;

    [Header("UI")]
    [SerializeField] private CombatUI combatUI;
    [SerializeField] private Slider   hungerBarInCombat;

    [Header("Items iniciales del jugador")]
    [SerializeField] private List<ItemData> startingItems;

    [Header("Enemigos de prueba (si no hay datos de transferencia)")]
    [SerializeField] private EnemyData[] fallbackEnemies;

    private void Start()
    {
        EnemyData[] toLoad = CombatDataTransfer.EnemiesToLoad;
        if (toLoad == null || toLoad.Length == 0)
        {
            Debug.LogWarning("CombatSceneInitializer: sin datos de transferencia. Usando fallbackEnemies.");
            toLoad = fallbackEnemies;
        }

        if (toLoad == null || toLoad.Length == 0)
        {
            Debug.LogError("CombatSceneInitializer: no hay enemigos. Abortando.");
            return;
        }

        // ── Inicializar combate ────────────────────────────────────────────────
        CombatManager.Instance.InitializeCombat(playerStats, toLoad.ToList());

        PlayerCombatant player = CombatManager.Instance.Player;

        // ── Aplicar vida transferida ───────────────────────────────────────────
        // TakeDamage aplica defensa: usamos (daño_deseado + defensa_base) como offset.
        if (CombatDataTransfer.PlayerHealthOnEntry > 0)
        {
            int missing = playerStats.maxHealth - CombatDataTransfer.PlayerHealthOnEntry;
            if (missing > 0)
                player.TakeDamage(missing + playerStats.defense);
        }

        // ── Aplicar energía transferida ────────────────────────────────────────
        if (CombatDataTransfer.PlayerEnergyOnEntry > 0)
        {
            int missing = playerStats.maxEnergy - CombatDataTransfer.PlayerEnergyOnEntry;
            if (missing > 0)
                player.SpendEnergy(missing);
        }

        // ── Aplicar hambre ─────────────────────────────────────────────────────
        player.HungerPercent = CombatDataTransfer.HungerOnEntry;

        // ── Conectar HungerSystem ──────────────────────────────────────────────
        HungerSystem hunger = HungerSystem.Instance;
        if (hunger != null)
        {
            hunger.RegisterHungerBar(hungerBarInCombat);
            hunger.EnterCombat(player);
        }

        // ── Aplicar level up si viene de un combate anterior ──────────────────
        if (LevelUpData.PendingLevelUp)
        {
            player.ApplyLevelUpBonus(
                LevelUpData.BonusMaxHP,
                LevelUpData.BonusAttack,
                LevelUpData.BonusMaxEnergy);
            LevelUpData.PendingLevelUp = false;
        }

        // ── Aplicar bonificación de comida (elegida antes del boss) ───────────
        if (LevelUpData.MealHPRestore > 0 || LevelUpData.MealHungerRestore > 0)
        {
            // Hambre: restaurar en HungerSystem y sincronizar con el jugador
            if (HungerSystem.Instance != null)
            {
                HungerSystem.Instance.Eat(LevelUpData.MealHungerRestore);
                player.HungerPercent = HungerSystem.Instance.HungerPercent;
            }
            else
            {
                // Sin HungerSystem (test directo de CombatScene): aplicar al jugador directamente
                player.HungerPercent = Mathf.Clamp01(
                    player.HungerPercent + LevelUpData.MealHungerRestore / 100f);
            }

            player.Heal(LevelUpData.MealHPRestore);
            if (LevelUpData.MealEnergyRestore > 0)
                player.RecoverEnergy(LevelUpData.MealEnergyRestore);

            LevelUpData.ResetMeal();
        }

        // ── Inicializar UI ─────────────────────────────────────────────────────
        if (combatUI != null)
        {
            combatUI.InitEnemyHUDs(toLoad.Length);   // ocultar HUDs sobrantes
            combatUI.SetInventory(new List<ItemData>(startingItems));
            combatUI.UpdateHUD();
            combatUI.ShowCombatEntrance();
        }

        // Limpiar transferencia para evitar datos obsoletos en próximas cargas
        CombatDataTransfer.EnemiesToLoad = null;
    }
}
