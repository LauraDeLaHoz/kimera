using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

/// <summary>
/// Controla el slider de volumen general del juego.
/// Puedes usar un AudioMixer o AudioListener directamente.
///
/// Asigna en el Inspector:
///   - volumeSlider   : Slider de la UI
///   - audioMixer     : (Opcional) AudioMixer con parámetro "MasterVolume"
///   - useMixer       : true = AudioMixer, false = AudioListener.volume
///
/// Si usas AudioMixer, expón el parámetro "MasterVolume" en el Mixer
/// y configuralo con rango -80 a 0 dB.
/// </summary>
public class VolumeSlider : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider volumeSlider;

    [Header("Audio Mixer (opcional)")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string mixerParameter = "MasterVolume";
    [SerializeField] private bool useMixer = false;

    private const string VolumeKey = "MasterVolume";

    private void Start()
    {
        // Carga el valor guardado (default 0.75)
        float savedVolume = PlayerPrefs.GetFloat(VolumeKey, 0.75f);
        volumeSlider.value = savedVolume;
        ApplyVolume(savedVolume);

        volumeSlider.onValueChanged.AddListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        ApplyVolume(value);
        PlayerPrefs.SetFloat(VolumeKey, value);
        PlayerPrefs.Save();
    }

    private void ApplyVolume(float normalizedValue)
    {
        if (useMixer && audioMixer != null)
        {
            // Convierte 0-1 a dB (-80 a 0)
            float dB = normalizedValue > 0.001f
                ? Mathf.Log10(normalizedValue) * 20f
                : -80f;
            audioMixer.SetFloat(mixerParameter, dB);
        }
        else
        {
            AudioListener.volume = normalizedValue;
        }
    }

    private void OnDestroy()
    {
        volumeSlider.onValueChanged.RemoveListener(OnSliderChanged);
    }
}