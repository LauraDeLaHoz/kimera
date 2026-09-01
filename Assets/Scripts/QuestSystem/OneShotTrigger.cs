using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Para beats narrativos que NO son una quest formal: el carnicero que te dice
/// "descubriste algo interesante", el inicio del motín, etc. Cosas que solo
/// deben pasar UNA vez en toda la partida y opcionalmente encadenan la
/// siguiente parte de la escena (UnityEvent).
///
/// No lo actives directamente desde OnTriggerEnter si depende de una acción
/// del jugador (como "hablar con el vendedor"): llama a Fire() desde el punto
/// exacto donde ocurre esa acción (por ejemplo, desde un external function del
/// diálogo, ver DialogEventsBinder). Así evitas el problema de "doble trigger"
/// que mencionabas: un solo punto de entrada, un solo flag.
/// </summary>
public class OneShotTrigger : MonoBehaviour
{
    [Tooltip("ID único en todo el proyecto. Ej: 'carniceria_hablo_vendedor', 'motin_inicio'.")]
    public string flagId;

    [Header("Logro (opcional)")]
    [Tooltip("Si se llena, se muestra como toast de logro al dispararse por primera vez.")]
    [TextArea]
    public string achievementText;

    [Header("Encadenado")]
    [Tooltip("Se invoca SOLO la primera vez. Aquí conectas: dar el item, activar el trigger del motín, spawnear a la guía, etc.")]
    public UnityEvent onFirstTrigger;

    /// <summary>Llama esto desde donde ocurra el evento real (fin de diálogo, botón, etc.).</summary>
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