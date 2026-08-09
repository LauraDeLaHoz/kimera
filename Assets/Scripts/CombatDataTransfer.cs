// Puente estático entre la escena de exploración y CombatScene.
// Los datos persisten en memoria durante la carga de escena.
public static class CombatDataTransfer
{
    public static EnemyData[] EnemiesToLoad;
    public static int   PlayerHealthOnEntry; // -1 = usar máximo
    public static int   PlayerEnergyOnEntry; // -1 = usar máximo
    public static float HungerOnEntry = 1f;  // 0-1
}
