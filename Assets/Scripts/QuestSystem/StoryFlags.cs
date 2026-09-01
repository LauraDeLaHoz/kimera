using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Registro central de "cosas que ya pasaron una vez" en la partida:
/// hablaste con el carnicero, empezó el motín, ya explicaste el objetivo, etc.
///
/// Es el reemplazo del patrón "bool completed dentro del ScriptableObject"
/// que tenías en QuestData. Aquí el estado vive en la partida (runtime),
/// no en el asset, así que no se "ensucia" el asset entre sesiones de Play.
///
/// Uso típico:
///   if (StoryFlags.Instance.TryConsume("carniceria_hablo_vendedor"))
///   {
///       // esto SOLO entra la primera vez que se llama con este id
///   }
/// </summary>
public class StoryFlags : MonoBehaviour
{
    public static StoryFlags Instance;

    private readonly HashSet<string> flagsActivadas = new HashSet<string>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Devuelve true SOLO la primera vez que se llama con este id.
    /// Ideal para triggers de "esto pasa una sola vez en toda la partida".
    /// </summary>
    public bool TryConsume(string flagId)
    {
        if (string.IsNullOrEmpty(flagId))
        {
            Debug.LogWarning("StoryFlags: flagId vacío.");
            return false;
        }

        if (flagsActivadas.Contains(flagId))
            return false;

        flagsActivadas.Add(flagId);
        return true;
    }

    public bool IsSet(string flagId)
    {
        return flagsActivadas.Contains(flagId);
    }

    /// <summary>Marca un flag sin pasar por la lógica de "primera vez" (por si necesitas forzarlo).</summary>
    public void SetFlag(string flagId)
    {
        flagsActivadas.Add(flagId);
    }

    // Si más adelante quieres guardar partida, aquí es donde
    // serializarías flagsActivadas a PlayerPrefs/JSON y lo cargarías en Awake.
    // Lo dejo fuera a propósito para no acoplar guardado con lógica de flags.
}