using System.Collections;
using UnityEngine;

public class RouteAlertIcon : MonoBehaviour
{
    public NPCInteract npc;

    [Tooltip("Ícono sobre el NPC en el mundo.")]
    public GameObject visual;

    [Tooltip("Marcador que solo ve la cámara del minimapa.")]
    public GameObject minimapMarker;

    [Header("Texto guía (opcional)")]
    [Tooltip("Texto que aparece arriba con fade in cuando ESTE NPC pasa a ser el siguiente punto " +
             "del recorrido. Ej: 'Dirígete a la carnicería...'. Desaparece solo al completar este punto. " +
             "Vacío = no muestra nada para este NPC.")]
    [TextArea]
    public string guideText;

    [Tooltip("Segundos de espera antes de mostrar el texto (útil para el primer punto, si querés " +
             "que aparezca después de una intro o cinemática).")]
    public float guideDelay = 0f;

    private bool wasShowing;
    private Coroutine guideRoutine;

    private void Update()
    {
        if (npc == null) return;

        bool shouldShow = npc.PrerequisiteMet && !npc.AlreadyCompleted;

        if (visual != null && visual.activeSelf != shouldShow)
            visual.SetActive(shouldShow);

        if (minimapMarker != null && minimapMarker.activeSelf != shouldShow)
            minimapMarker.SetActive(shouldShow);

        // Solo reaccionamos al CAMBIO (este NPC acaba de volverse el objetivo / acaba de completarse).
        if (shouldShow != wasShowing)
        {
            wasShowing = shouldShow;

            if (shouldShow)
                RequestGuide();
            else
                ReleaseGuide();
        }
    }

    private void RequestGuide()
    {
        if (string.IsNullOrEmpty(guideText)) return;

        if (guideRoutine != null)
            StopCoroutine(guideRoutine);

        guideRoutine = StartCoroutine(ShowGuideAfterDelay());
    }

    private IEnumerator ShowGuideAfterDelay()
    {
        if (guideDelay > 0f)
            yield return new WaitForSeconds(guideDelay);

        // Por si mientras esperaba ya se completó este punto.
        if (wasShowing && NotificationManager.Instance != null)
            NotificationManager.Instance.ShowGuide(guideText, this);

        guideRoutine = null;
    }

    private void ReleaseGuide()
    {
        if (guideRoutine != null)
        {
            StopCoroutine(guideRoutine);
            guideRoutine = null;
        }

        // HideGuide(this) solo oculta si el texto actual es de ESTE NPC;
        // así no borra el texto del siguiente punto si éste ya lo pidió.
        if (NotificationManager.Instance != null)
            NotificationManager.Instance.HideGuide(this);
    }

    private void OnDisable()
    {
        if (wasShowing)
        {
            wasShowing = false;
            ReleaseGuide();
        }
    }
}