using UnityEngine;

[CreateAssetMenu(menuName = "Kimera/MutationData")]
public class MutationData : ScriptableObject
{
    public string mutationName;
    public int energyCost = 10;
    public string[] analysisTexts; // índice por enemigo
    public bool revealsWeakness = true;
    public float weaknessWindowTurns = 3f;
}
