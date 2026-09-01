using System.Collections;
using UnityEngine;

/// <summary>
/// Orquesta el arranque de la partida: bienvenida -> "Explora la ciudad" a los
/// 5s -> tras 20s de exploración, logro + arranque automático de la quest
/// "Encuentra a la guía". Todo esto pasa UNA sola vez (StoryFlags).
///
/// "Explora la ciudad" se modela como una QuestData real de tipo Explore,
/// no como una llamada suelta a la UI: así el estado también vive en
/// QuestManager y puedes preguntarle a QuestManager si ya se exploró,
/// en vez de tener esa info regada en otro script.
///
/// Poné este componente una sola vez en la escena (ej. junto a GameManager).
/// </summary>
public class IntroSequence : MonoBehaviour
{
    [Header("Quests")]
    [Tooltip("Quest tipo Explore, ej. 'Explora la ciudad'.")]
    public QuestData exploreCityQuest;

    [Tooltip("Se inicia automáticamente cuando termina exploreCityQuest, salvo que ya hayas puesto autoStartNextQuest en el asset (en ese caso dejá esto vacío).")]
    public QuestData findGuideQuest;

    [Header("Tiempos")]
    public float delayBeforeExplorePrompt = 5f;
    public float explorationDuration = 20f;

    [Header("Bienvenida (opcional)")]
    [TextArea] public string welcomeText = "Bienvenido a Kimera City!";

    private void Start()
    {
        if (!StoryFlags.Instance.TryConsume("intro_sequence"))
            return; // ya se corrió esta secuencia antes (ej. volviste a cargar la escena)

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (!string.IsNullOrEmpty(welcomeText))
            NotificationManager.Instance.ShowAchievement(welcomeText);

        yield return new WaitForSeconds(delayBeforeExplorePrompt);

        QuestManager.Instance.StartQuest(exploreCityQuest);
        // Esto ya dispara ShowObjective("Explora la ciudad") solo,
        // porque NotificationManager está suscrito a OnQuestStarted.

        yield return new WaitForSeconds(explorationDuration);

        QuestManager.Instance.CompleteQuestManually(exploreCityQuest);
        // Esto dispara HideObjective() + ShowAchievement(achievementText del asset,
        // ej. "Disfruta tu estancia en Kimera City") automáticamente.

        // Si configuraste exploreCityQuest.autoStartNextQuest en el asset,
        // "Encuentra a la guía" ya arrancó sola en QuestManager.CompleteCurrentQuest().
        // Si preferís no tocar el asset y encadenar desde acá, descomentá:
        // if (findGuideQuest != null) QuestManager.Instance.StartQuest(findGuideQuest);
    }
}