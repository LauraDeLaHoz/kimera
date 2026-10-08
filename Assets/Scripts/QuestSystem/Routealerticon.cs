using UnityEngine;

public class RouteAlertIcon : MonoBehaviour
{
    public NPCInteract npc;

    [Tooltip("Ícono sobre el NPC en el mundo.")]
    public GameObject visual;

    [Tooltip("Marcador que solo ve la cámara del minimapa.")]
    public GameObject minimapMarker;

    private void Update()
    {
        if (npc == null) return;

        bool shouldShow = npc.PrerequisiteMet && !npc.AlreadyCompleted;

        if (visual != null && visual.activeSelf != shouldShow)
            visual.SetActive(shouldShow);

        if (minimapMarker != null && minimapMarker.activeSelf != shouldShow)
            minimapMarker.SetActive(shouldShow);
    }
}