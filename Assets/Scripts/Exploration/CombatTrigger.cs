using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Coloca este componente en un trigger collider del mundo de exploración.
// Asigna en Inspector: enemies[] con los EnemyData que debe cargar ese encuentro.
// fadeOverlay: CanvasGroup de un panel negro full-screen (opcional, recomendado).
public class CombatTrigger : MonoBehaviour
{
    [SerializeField] private EnemyData[]  enemies;
    [SerializeField] private CanvasGroup  fadeOverlay;
    [SerializeField] private float        fadeDuration = 0.5f;
    [SerializeField] private string       combatSceneName = "CombatScene";

    private bool _triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered || !other.CompareTag("Player")) return;
        _triggered = true;

        CombatDataTransfer.EnemiesToLoad       = enemies;
        CombatDataTransfer.HungerOnEntry       = HungerSystem.Instance != null
                                                 ? HungerSystem.Instance.HungerPercent
                                                 : 1f;
        // Health/Energy: -1 indica "usar máximo" (sin sistema de exploración de vida aún)
        CombatDataTransfer.PlayerHealthOnEntry = -1;
        CombatDataTransfer.PlayerEnergyOnEntry = -1;

        StartCoroutine(FadeAndLoad());
    }

    private IEnumerator FadeAndLoad()
    {
        if (fadeOverlay != null)
        {
            float elapsed = 0f;
            fadeOverlay.blocksRaycasts = true;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                fadeOverlay.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                yield return null;
            }
        }
        else
        {
            yield return null;
        }

        SceneManager.LoadScene(combatSceneName);
    }
}
