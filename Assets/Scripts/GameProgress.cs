using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// GameProgress  (clase estática)
// ─────────────────────────────────────────────────────────────────────────────
// Estado global del juego accesible desde cualquier script sin singletons.
// Se resetea al inicio de cada sesión de Play.
//
// FLUJO PREVISTO:
//   Inicio → ShopCompleted = false  → enemigos desactivados
//   Jugador compra en la tienda
//     → ShopCompleted = true
//     → OnShopCompleted?.Invoke()   ← enemigos se activan aquí
// ─────────────────────────────────────────────────────────────────────────────
public static class GameProgress
{
    // ── Estado ─────────────────────────────────────────────────────────────────
    public static bool  ShopCompleted    { get; private set; }
    public static int   ShopItemSelected { get; private set; } = -1;  // 0-2

    // ── Evento: suscribirse para reaccionar cuando se complete la tienda ───────
    public static event System.Action<int> OnShopCompleted;   // int = índice del item

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Llama desde <see cref="ShopInteraction"/> cuando el jugador elige un item.
    /// Dispara el evento <see cref="OnShopCompleted"/> con el índice seleccionado.
    /// </summary>
    public static void CompleteShop(int selectedItemIndex)
    {
        if (ShopCompleted)
        {
            Debug.LogWarning("[GameProgress] CompleteShop llamado pero la tienda ya estaba completada.");
            return;
        }

        ShopCompleted    = true;
        ShopItemSelected = selectedItemIndex;

        Debug.Log($"[GameProgress] Tienda completada. Item seleccionado: {selectedItemIndex}");
        OnShopCompleted?.Invoke(selectedItemIndex);
    }

    /// <summary>Resetea el estado (útil en tests o al reiniciar la escena).</summary>
    public static void Reset()
    {
        ShopCompleted    = false;
        ShopItemSelected = -1;
    }
}
