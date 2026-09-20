using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inventario del jugador acumulado durante la exploración (carnicero, tienda,
/// monumento, motín, guía, etc.). Reemplaza la vieja mecánica de "elegís 1 item
/// en la tienda y eso determina tu loadout": ahora cada punto del recorrido
/// entrega un item real, y este inventario los va acumulando.
///
/// InSceneCombatController lee de acá para armar la lista de items disponibles
/// en combate, sumándolos a los 'Starting Items' fijos que tengas en el
/// Inspector (si los tenés, por ejemplo para pruebas).
///
/// Estático a propósito, igual que GameProgress — no necesita vivir en un
/// GameObject ni sobrevivir entre escenas con DontDestroyOnLoad.
/// </summary>
public static class PlayerInventory
{
    private static readonly List<ItemData> items = new List<ItemData>();

    public static IReadOnlyList<ItemData> Items => items;

    /// <summary>Agrega un item al inventario del jugador (ej. al completar el diálogo de un punto del recorrido).</summary>
    public static void AddItem(ItemData item)
    {
        if (item == null) return;

        items.Add(item);
        Debug.Log($"[PlayerInventory] Item agregado: {item.itemName}");
    }

    public static bool HasItem(ItemData item)
    {
        return item != null && items.Contains(item);
    }

    /// <summary>Útil para reiniciar partida/tests.</summary>
    public static void Reset()
    {
        items.Clear();
    }
}