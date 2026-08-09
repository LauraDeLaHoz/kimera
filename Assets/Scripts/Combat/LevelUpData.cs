// Datos que persisten entre la escena de combate normal y la del boss.
// No es MonoBehaviour — vive en memoria mientras la aplicación está abierta.
public static class LevelUpData
{
    public static bool IsBossFight    = false;
    public static bool PendingLevelUp = false;

    // Bonificaciones de subida de nivel
    public const int BonusMaxHP      = 20;
    public const int BonusAttack     =  5;
    public const int BonusMaxEnergy  = 10;

    // Bonificaciones de comida (elegidas por el jugador antes del boss)
    public static float MealHungerRestore = 0f;  // puntos absolutos de hambre (0-100)
    public static int   MealHPRestore     = 0;
    public static int   MealEnergyRestore = 0;

    public static void ResetMeal()
    {
        MealHungerRestore = 0f;
        MealHPRestore     = 0;
        MealEnergyRestore = 0;
    }
}
