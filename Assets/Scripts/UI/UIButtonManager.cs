using UnityEngine;

public class UIButtonManager : MonoBehaviour
{
    [Header("Referencias")]
    public UITransitionManager transitionManager;
    public UIAudioManager audioManager;

    [Header("Escena")]
    public string gameSceneName = "juego";

    [Header("Panel options")]
    public GameObject optionsPanel;

    // ─── Menú inicio ───────────────────────────────────

    public void OnInicioPlay() => transitionManager.TransitionToFinal();

    // ─── Menú final (ventana del bus) ──────────────────

    public void OnFinalPlay() => transitionManager.FadeOutThenLoad(gameSceneName);

    public void OnFinalOptions()
    {
        audioManager?.PlayClick();
        if (optionsPanel != null)
            optionsPanel.SetActive(!optionsPanel.activeSelf);
    }

    public void OnFinalVolver() => transitionManager.TransitionToStart();

    public void OnQuit()
    {
        audioManager?.PlayClick();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}