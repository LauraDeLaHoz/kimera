using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// EnemyDisplayManager
// ─────────────────────────────────────────────────────────────────────────────
// Gestiona los displays visuales de enemigos en el Canvas de combate.
//
// DISEÑO:
//   Los displays son GameObjects PERMANENTES en la escena. Este script NO
//   los crea, destruye ni reposiciona en runtime. Solo llama a Refresh() en
//   cada uno al inicio de cada combate para que carguen los datos del enemigo
//   correcto, o se oculten si ese slot no tiene enemigo (boss fight).
//
//   → Mueve, escala y ajusta cada display libremente desde el Editor.
//   → Las posiciones se mantienen entre combates.
//   → Cada display tiene su propio RectTransform, Image y Animator.
//
// SETUP (una sola vez por escena):
//   Ejecutar Kimera/11 para:
//   · Crear EnemyDisplay_0 y EnemyDisplay_1 en la jerarquía del Canvas.
//   · Cablear ambos en el array "displays" de este componente.
//   Luego posiciona los GOs como quieras desde el Inspector o Scene View.
//
// FLUJO DE COMBATE:
//   InSceneCombatController.InitializeCombatLogic()
//     1. CombatManager.InitializeCombat()      ← Enemies lista lista
//     2. EnemyDisplayManager.RefreshForCombat()← muestra/oculta + carga sprites
//     3. CombatUI.InitEnemyHUDs()              ← sincroniza HUD de vida/nombre
// ─────────────────────────────────────────────────────────────────────────────
[DefaultExecutionOrder(-80)]
public class EnemyDisplayManager : MonoBehaviour
{
    public static EnemyDisplayManager Instance { get; private set; }

    [Header("Displays de enemigos — arrastra aquí los EnemyActionDisplay del Canvas")]
    [Tooltip(
        "Cada elemento = un slot de enemigo.\n" +
        "Índice 0 → primer enemigo, índice 1 → segundo, etc.\n\n" +
        "IMPORTANTE: mueve los GOs en la escena desde el Inspector o la Scene View.\n" +
        "Este script NO modifica posición, escala ni tamaño en ningún momento.")]
    [SerializeField] private EnemyActionDisplay[] displays;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── API pública ────────────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve el <see cref="EnemyActionDisplay"/> asignado al slot <paramref name="index"/>,
    /// o <c>null</c> si el índice está fuera de rango o el slot no está asignado.
    /// </summary>
    public EnemyActionDisplay GetDisplayAt(int index)
    {
        if (displays == null || index < 0 || index >= displays.Length) return null;
        return displays[index];
    }

    /// <summary>
    /// Refresca todos los displays para el combate que acaba de inicializarse.
    /// <list type="bullet">
    ///   <item>Displays con enemigo válido en <c>CombatManager.Enemies[i]</c>: se activan y cargan sprites.</item>
    ///   <item>Displays sin enemigo (boss fight → slot 1 vacío): se ocultan.</item>
    /// </list>
    /// <b>No modifica ningún RectTransform.</b>
    /// </summary>
    public void RefreshForCombat()
    {
        if (displays == null || displays.Length == 0)
        {
            Debug.LogWarning("[EnemyDisplayManager] displays[] vacío. " +
                             "Ejecuta Kimera/11 y arrastra los EnemyActionDisplay al Inspector.");
            return;
        }

        Debug.Log($"[EnemyDisplayManager] ── RefreshForCombat  ({displays.Length} slot(s)) ─────────");

        // ── Ocultar displays NO gestionados ───────────────────────────────────
        // Si el usuario ejecutó el menú de setup varias veces, pueden quedar GOs
        // de EnemyActionDisplay "huérfanos" (no están en displays[]) que se ven
        // en pantalla y provocan el bug de "dos cuadros en boss fight".
        var allDisplays = FindObjectsByType<EnemyActionDisplay>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var d in allDisplays)
        {
            bool managed = false;
            for (int i = 0; i < displays.Length; i++)
                if (displays[i] == d) { managed = true; break; }
            if (!managed)
            {
                d.gameObject.SetActive(false);
                Debug.Log($"  [cleanup] Ocultado display no gestionado: '{d.gameObject.name}'");
            }
        }

        for (int i = 0; i < displays.Length; i++)
        {
            var d = displays[i];
            if (d == null)
            {
                Debug.LogWarning($"  [slot {i}] NULL — asigna el display en el Inspector.");
                continue;
            }

            // EnemyActionDisplay.Refresh():
            //   • Enemies[i] existe  → SetActive(true), carga sprite/animación del enemigo
            //   • Enemies[i] no existe (boss, solo 1 enemigo) → SetActive(false)
            d.Refresh();

            bool active = d.gameObject.activeSelf;
            string tracked = "(oculto — sin enemigo en este slot)";
            if (active && CombatManager.Instance?.Enemies != null &&
                i < CombatManager.Instance.Enemies.Count)
            {
                string name = CombatManager.Instance.Enemies[i]?.Data?.enemyName ?? "?";
                tracked = $"→ '{name}'";
            }

            Debug.Log($"  [slot {i}]  GO='{d.gameObject.name}'  {(active ? "ACTIVO " + tracked : tracked)}");
        }

        Debug.Log("[EnemyDisplayManager] ──────────────────────────────────────────────────────");

        // Avisar a CombatHitFeedback para que actualice su caché de displays
        var hitFeedback = FindFirstObjectByType<CombatHitFeedback>();
        if (hitFeedback != null)
        {
            // hitFeedback.RefreshDisplayCache();
            Debug.Log("[EnemyDisplayManager] ✓ CombatHitFeedback.RefreshDisplayCache() actualizado.");
        }
        else
        {
            Debug.LogWarning("[EnemyDisplayManager] CombatHitFeedback no encontrado en escena.");
        }
    }
}
