using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIAudioManager : MonoBehaviour
{
    [Header("AudioSource")]
    public AudioSource audioSource;

    [Header("Clips")]
    public AudioClip hoverClip;
    public AudioClip clickClip;
    public AudioClip ambienceClip;

    [Range(0f, 1f)] public float hoverVolume = 0.4f;
    [Range(0f, 1f)] public float clickVolume = 0.8f;
    [Range(0f, 1f)] public float ambienceVolume = 0.6f;

    void Start()
    {
        if (ambienceClip != null)
        {
            audioSource.clip = ambienceClip;
            audioSource.loop = true;
            audioSource.volume = ambienceVolume;
            audioSource.Play();
        }

        foreach (Button btn in GetComponentsInChildren<Button>(true))
            RegisterHover(btn.gameObject);
    }

    void RegisterHover(GameObject go)
    {
        var trigger = go.GetComponent<EventTrigger>()
                   ?? go.AddComponent<EventTrigger>();

        var entry = new EventTrigger.Entry
        { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ => PlayHover());
        trigger.triggers.Add(entry);
    }

    public void PlayHover() =>
        audioSource.PlayOneShot(hoverClip, hoverVolume);

    public void PlayClick() =>
        audioSource.PlayOneShot(clickClip, clickVolume);
}