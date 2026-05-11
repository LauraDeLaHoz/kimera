using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public enum GameState
    {
        Exploration,
        Combat
    }

    public GameState currentState;

    private void Awake()
    {
        // Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        currentState = GameState.Exploration;
    }

    public bool IsExploration()
    {
        return currentState == GameState.Exploration;
    }

    public bool IsCombat()
    {
        return currentState == GameState.Combat;
    }

    public void EnterCombat()
    {
        currentState = GameState.Combat;

        Debug.Log("Modo Combate");
    }

    public void EnterExploration()
    {
        currentState = GameState.Exploration;

        Debug.Log("Modo Exploracion");
    }
}
