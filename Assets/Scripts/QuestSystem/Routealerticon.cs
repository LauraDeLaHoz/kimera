using UnityEngine;

/// <summary>
/// Ícono flotando sobre un NPC del recorrido (ej. la flechita/marca roja que
/// ya tenés) que le indica al jugador "acá es donde tenés que ir ahora".
///
/// Se muestra SOLO cuando:
///   - el requisito previo de ese NPC ya se cumplió (es su turno), Y
///   - ese NPC todavía no fue completado (no le hablaste todavía).
///
/// Cuando lo hagas más adelante, este mismo componente es el que le va a
/// avisar al minimapa "che, marcá acá" — por ahora solo prende/apaga un
/// objeto visual en el mundo.
///
/// Poné este componente en un GO PADRE (ej. 'AlertIcon'), con el ícono visual
/// como HIJO — así el script sigue evaluando cada frame aunque el ícono esté
/// oculto (si apagaras el propio GameObject del script, su Update() dejaría
/// de correr y nunca volvería a prenderse solo).
/// </summary>
public class RouteAlertIcon : MonoBehaviour
{
    [Tooltip("El NPCInteract de ESTE punto del recorrido (el mismo NPC sobre el que flota este ícono).")]
    public NPCInteract npc;

    [Tooltip("El objeto visual del ícono (el sprite/modelo de la flechita). Este es el que se prende/apaga, NO el GameObject del script.")]
    public GameObject visual;

    private void Update()
    {
        if (npc == null || visual == null)
            return;

        bool shouldShow = npc.PrerequisiteMet && !npc.AlreadyCompleted;

        if (visual.activeSelf != shouldShow)
            visual.SetActive(shouldShow);
    }
}