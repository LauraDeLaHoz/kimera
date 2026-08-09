using UnityEngine;
using TMPro;
using System.Collections;

public class ObjectiveUI : MonoBehaviour
{
    public static ObjectiveUI Instance;

    [Header("Objective")]
    public RectTransform objectivePanel;
    public TMP_Text objectiveText;
    public AudioClip objectiveSound;

    [Header("Achievement")]
    public RectTransform achievementPanel;
    public TMP_Text achievementText;
    public AudioClip achievementSound;

    [Header("Animation")]
    public float slideSpeed = 8f;

    public float visibleTime = 3f;

    [Header("Positions")]
    public Vector2 objectiveHiddenPos;
    public Vector2 objectiveVisiblePos;

    public Vector2 achievementHiddenPos;
    public Vector2 achievementVisiblePos;

    private AudioSource audioSource;

    private void Awake()
    {
        Instance = this;

        audioSource = GetComponent<AudioSource>();

        objectivePanel.anchoredPosition =
            objectiveHiddenPos;

        achievementPanel.anchoredPosition =
            achievementHiddenPos;
    }

    // =========================
    // OBJECTIVE
    // =========================

    public void ShowObjective(string text)
    {
        objectiveText.text = text;

        StopCoroutine("ObjectiveRoutine");

        StartCoroutine(
            ObjectiveRoutine()
        );

        if (objectiveSound != null)
        {
            audioSource.PlayOneShot(
                objectiveSound
            );
        }
    }

    // =========================
    // ACHIEVEMENT
    // =========================

    public void ShowAchievement(string text)
    {
        achievementText.text =
            "Completado: " + text;

        StartCoroutine(
            SlideRoutine(
                achievementPanel,
                achievementHiddenPos,
                achievementVisiblePos,
                visibleTime,
                achievementSound
            )
        );
    }

    public void HideObjective()
    {
        StartCoroutine(
            HideObjectiveRoutine()
        );
    }

    // =========================
    // ANIMATION
    // =========================

    IEnumerator SlideRoutine(
        RectTransform panel,
        Vector2 hiddenPos,
        Vector2 visiblePos,
        float waitTime,
        AudioClip clip
    )
    {
        // sonido
        if (clip != null)
        {
            audioSource.PlayOneShot(clip);
        }

        // slide in
        while (Vector2.Distance(
            panel.anchoredPosition,
            visiblePos) > 0.1f)
        {
            panel.anchoredPosition =
                Vector2.Lerp(
                    panel.anchoredPosition,
                    visiblePos,
                    Time.deltaTime * slideSpeed
                );

            yield return null;
        }

        panel.anchoredPosition =
            visiblePos;

        // esperar
        yield return new WaitForSeconds(waitTime);

        // slide out
        while (Vector2.Distance(
            panel.anchoredPosition,
            hiddenPos) > 0.1f)
        {
            panel.anchoredPosition =
                Vector2.Lerp(
                    panel.anchoredPosition,
                    hiddenPos,
                    Time.deltaTime * slideSpeed
                );

            yield return null;
        }

        panel.anchoredPosition =
            hiddenPos;
    }

    IEnumerator ObjectiveRoutine()
    {
        while (Vector2.Distance(
            objectivePanel.anchoredPosition,
            objectiveVisiblePos) > 0.1f)
        {
            objectivePanel.anchoredPosition =
                Vector2.Lerp(
                    objectivePanel.anchoredPosition,
                    objectiveVisiblePos,
                    Time.deltaTime * slideSpeed
                );

            yield return null;
        }

        objectivePanel.anchoredPosition =
            objectiveVisiblePos;
    }

    IEnumerator HideObjectiveRoutine()
    {
        while (Vector2.Distance(
            objectivePanel.anchoredPosition,
            objectiveHiddenPos) > 0.1f)
        {
            objectivePanel.anchoredPosition =
                Vector2.Lerp(
                    objectivePanel.anchoredPosition,
                    objectiveHiddenPos,
                    Time.deltaTime * slideSpeed
                );

            yield return null;
        }

        objectivePanel.anchoredPosition =
            objectiveHiddenPos;
    }
}