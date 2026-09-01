using System.Collections;
using UnityEngine;
using UnityEngine.Playables; // opcional, solo si usas Timeline para la cámara

/// <summary>
/// Ejemplo del patrón para "algo pasa 5 segundos después de que empieza el
/// motín" SIN cargar otra escena (por eso no perdés el progreso de las
/// misiones: todo tu Canvas/GameManager/QuestManager/StoryFlags ya viven en
/// la misma escena "Juego", según tu Hierarchy).
///
/// La idea: en vez de LoadScene, simplemente quitás el control del jugador
/// un momento, opcionalmente reproducís un PlayableDirector (Timeline) para
/// la cámara, y cuando termina devolvés el control y seguís la cadena
/// (aparece la guía, empieza el diálogo, etc.).
///
/// Adaptá los métodos SetPlayerControlEnabled / cámara a como ya lo hacés
/// en NPCInteract al entrar en diálogo (mismo patrón, reusalo).
/// </summary>
public class NarrativeSequencer : MonoBehaviour
{
    [Header("Motín")]
    public string motinFlagId = "motin_inicio";

    [Header("Jugador / cámara")]
    public MonoBehaviour playerMovement;
    [Tooltip("Opcional: si tenés una cinemática de cámara armada en Timeline.")]
    public PlayableDirector cutsceneDirector;

    public float delayBeforeContext = 5f;

    [Header("Siguiente paso")]
    [Tooltip("GameObject de la guía, desactivado hasta este punto.")]
    public GameObject guideNPC;

    /// <summary>
    /// Llamalo desde el external function que se dispara al SALIR de la
    /// carnicería / terminar el diálogo con el vendedor (el "segundo trigger"
    /// que mencionabas). Es one-shot por sí mismo.
    /// </summary>
    public void StartMotin()
    {
        if (!StoryFlags.Instance.TryConsume(motinFlagId))
            return;

        StartCoroutine(MotinRoutine());
    }

    private IEnumerator MotinRoutine()
    {
        GameManager.Instance.EnterCombat(); // o el estado que uses para "motín en curso"

        if (playerMovement != null)
            playerMovement.enabled = false;

        yield return new WaitForSeconds(delayBeforeContext);

        if (cutsceneDirector != null)
        {
            cutsceneDirector.Play();
            yield return new WaitForSeconds((float)cutsceneDirector.duration);
        }

        // Aparece la guía y arranca el diálogo con ella.
        if (guideNPC != null)
            guideNPC.SetActive(true);

        if (playerMovement != null)
            playerMovement.enabled = true;

        GameManager.Instance.EnterExploration();

        // A partir de acá, el diálogo con la guía lo dispara tu NPCInteract
        // normal (E para hablar) o, si querés que arranque solo, llamá acá
        // mismo a guideNPC.GetComponent<NPCInteract>().StartDialogue() si lo hacés público.
    }
}