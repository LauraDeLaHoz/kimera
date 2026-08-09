using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// NPC_CombatTrigger
// ─────────────────────────────────────────────────────────────────────────────
// Trigger de combate del NPC negro.
// Permanece DORMIDO hasta que GameProgress.OnShopCompleted dispara;
// solo entonces el jugador puede activar el combate tocando este NPC.
// ─────────────────────────────────────────────────────────────────────────────
public class NPC_CombatTrigger : MonoBehaviour
{
    [Tooltip("EnemyData que se cargarán al entrar en combate.")]
    public EnemyData[] enemies;

    private bool _active    = false;   // false hasta que la tienda termine
    private bool _triggered = false;

    private void Start()
    {
        if (GameProgress.ShopCompleted)
            _active = true;
        // Suscripción permanente: OnShopDone se llama en cada compra
        GameProgress.OnShopCompleted += OnShopDone;
    }

    private void OnDestroy()
    {
        GameProgress.OnShopCompleted -= OnShopDone;
    }

    private void OnShopDone(int _)
    {
        _active = true;
        _triggered = false;   // listo para un nuevo combate
        Debug.Log($"[NPC_CombatTrigger] '{name}' activado tras completar la tienda.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_active)   return;   // tienda no completada aún
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;
        if (InSceneCombatController.Instance == null)
        {
            Debug.LogWarning("[NPC_CombatTrigger] InSceneCombatController no encontrado.");
            return;
        }
        if (enemies == null || enemies.Length == 0)
        {
            Debug.LogWarning("[NPC_CombatTrigger] Sin EnemyData asignados.", this);
            return;
        }

        _triggered = true;
        _active = false;   // se duerme hasta la próxima compra en la tienda
        Debug.Log($"[NPC_CombatTrigger] Combate iniciado — {enemies.Length} enemigo(s).");
        InSceneCombatController.Instance.StartCombat(enemies, null);
    }

    public void ResetTrigger() => _triggered = false;
}
