using UnityEngine;
using UnityEngine.UI;

public class PlayerEnergy : MonoBehaviour
{
    [Header("Energy")]
    public float currentEnergy;
    public float maxEnergy = 100f;

    [Header("Drain")]
    public float energyDrainPerSecond = 1f;

    [Header("UI")]
    public Slider energySlider;

    private void Start()
    {
        currentEnergy = 25f;

        // Configurar slider
        energySlider.maxValue = maxEnergy;
        energySlider.value = currentEnergy;
    }

    private void Update()
    {
        if (!GameManager.Instance.IsExploration())
            return;

        DrainEnergy();

        // Actualizar UI
        energySlider.value = currentEnergy;
    }

    void DrainEnergy()
    {
        currentEnergy -= energyDrainPerSecond * Time.deltaTime;

        currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
    }

    public void AddEnergy(float amount)
    {
        currentEnergy += amount;

        currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);

        Debug.Log("Energia actual: " + currentEnergy);
    }
}
