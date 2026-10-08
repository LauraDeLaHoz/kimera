using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Para beats narrativos que NO son una quest formal por sí mismos: el
/// carnicero que da su ítem, la tienda, el monumento, el motín. Cosas que
/// solo deben pasar UNA vez en toda la partida y opcionalmente:
///   - entregan un ItemData al inventario del jugador
///   - entregan/activan una MutationData (el poder de Instinto)
///   - completan una quest formal de QuestManager (ej. el motín completa
///     "Explora la ciudad")
///   - encadenan lo que sigue (activar el siguiente punto del recorrido,
///     spawnear algo, etc.) vía UnityEvent
///
/// No lo actives directamente desde OnTriggerEnter si depende de una acción
/// del jugador (como "hablar con el vendedor"): llamá a Fire() desde el punto
/// exacto donde ocurre esa acción (por ejemplo, desde un external function del
/// diálogo, ver DialogEventsBinder). Así evitás el problema de "doble trigger":
/// un solo punto de entrada, un solo flag.
/// </summary>
public class OneShotTrigger : MonoBehaviour
{
    [Tooltip("ID único en todo el proyecto. Ej: 'carniceria_completada', 'motin_completado'.")]
    public string flagId;

    [Header("Logro / notificación de item (opcional)")]
    [Tooltip("Si se llena, se muestra como toast al dispararse por primera vez. " +
             "Sirve tanto para logros de historia como para 'Obtuviste tu primer ítem!'.")]
    [TextArea]
    public string achievementText;

    [Header("Recompensa (opcional)")]
    [Tooltip("Si se asigna, se agrega automáticamente a PlayerInventory al dispararse por primera vez.")]
    public ItemData itemReward;

    [Tooltip("Si se asigna, se vuelve la mutación/Instinto activa del jugador " +
             "(PlayerMutationState) al dispararse por primera vez. Ej: el " +
             "carnicero entregando 'Cola de Iguana'.")]
    public MutationData mutationReward;

    [Header("Completar quest (opcional)")]
    [Tooltip("Si se asigna, se llama a QuestManager.CompleteQuestManually con esta quest " +
             "al dispararse por primera vez. Ej: el punto del motín completa 'Explora la ciudad'.")]
    public QuestData completesQuest;

    [Header("Spawnear un objeto físico en el mundo (opcional)")]
    [Tooltip("Para objetos especiales que además de sumarse al inventario/mutación " +
             "necesitan aparecer como prop 3D en la escena (ej. la cola de iguana que " +
             "entrega el carnicero). Se instancia una sola vez, en Spawn Point.")]
    public GameObject spawnPrefab;

    [Tooltip("Dónde aparece spawnPrefab. Si lo dejás vacío, se usa la posición de este mismo GameObject. " +
             "Para una mutación que queda pegada al cuerpo del jugador (ej. la cola de iguana), " +
             "asigná acá un Transform que sea HIJO del Player (ej. un punto cerca de la cadera).")]
    public Transform spawnPoint;

    [Tooltip("Si está activo, el objeto instanciado se vuelve HIJO de 'Spawn Point' " +
             "(en vez de quedar suelto en el mundo) — así se mueve y rota junto con " +
             "el Player para siempre, como una parte más de su cuerpo.")]
    public bool parentToSpawnPoint = false;

    [Header("Encadenado")]
    [Tooltip("Se invoca SOLO la primera vez. Acá conectás: activar el siguiente " +
             "punto del recorrido, spawnear algo, etc.")]
    public UnityEvent onFirstTrigger;

    /// <summary>Llamá esto desde donde ocurra el evento real (fin de diálogo, botón, etc.).</summary>
    public bool Fire()
    {
        if (StoryFlags.Instance == null)
        {
            Debug.LogWarning("OneShotTrigger: no hay StoryFlags en la escena.");
            return false;
        }

        if (!StoryFlags.Instance.TryConsume(flagId))
            return false; // ya se disparó antes, no hacer nada

        if (!string.IsNullOrEmpty(achievementText) && NotificationManager.Instance != null)
        {
            NotificationManager.Instance.ShowAchievement(achievementText);
        }

        if (itemReward != null)
            PlayerInventory.AddItem(itemReward);

        if (mutationReward != null)
            PlayerMutationState.SetMutation(mutationReward);

        if (completesQuest != null && QuestManager.Instance != null)
            QuestManager.Instance.CompleteQuestManually(completesQuest);

        if (spawnPrefab != null)
        {
            Transform point = spawnPoint != null ? spawnPoint : transform;
            GameObject spawned = Instantiate(spawnPrefab, point.position, point.rotation);

            if (parentToSpawnPoint)
                spawned.transform.SetParent(point, true);
        }

        onFirstTrigger?.Invoke();
        return true;
    }

    /// <summary>Versión para usar directo como trigger físico de zona (ej: "llegar a este punto"), sin condición previa.</summary>
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Fire();
        }
    }
}