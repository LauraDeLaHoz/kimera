using UnityEngine;

/// <summary>
/// Mutación/Instinto activa del jugador en runtime (ej. "Cola de Iguana" que
/// entrega el carnicero). Se guarda ACÁ y no en CharacterStats.activeMutation
/// directamente, por la misma razón que QuestData ya no guarda "completed" en
/// el asset: si escribiéramos sobre el ScriptableObject del jugador en
/// runtime, el cambio se quedaría "pegado" al asset entre sesiones de Play en
/// el Editor.
///
/// CombatManager.PlayerUseInstinct debe leer de acá primero, y si no hay
/// ninguna asignada todavía, cae de vuelta a CharacterStats.activeMutation
/// (útil para seguir probando combate suelto sin tener que pasar por todo el
/// recorrido de exploración cada vez).
/// </summary>
public static class PlayerMutationState
{
    public static MutationData Current { get; private set; }

    public static void SetMutation(MutationData mutation)
    {
        Current = mutation;
        Debug.Log($"[PlayerMutationState] Mutación activa: {(mutation != null ? mutation.mutationName : "ninguna")}");
    }
}