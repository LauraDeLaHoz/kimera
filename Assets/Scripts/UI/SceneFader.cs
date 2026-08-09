using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneFader : MonoBehaviour
{
    public Image image;
    public float fadeDuration = 2.5f;

    private void Start()
    {
        StartCoroutine(FadeOut());
    }

    IEnumerator FadeOut()
    {
        float t = 0;

        Color c = image.color;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;

            c.a = 1 - (t / fadeDuration);

            image.color = c;

            yield return null;
        }

        c.a = 0;
        image.color = c;
    }


}
