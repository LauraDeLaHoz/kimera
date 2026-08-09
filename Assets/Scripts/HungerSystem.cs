using UnityEngine;
using UnityEngine.UI;

// Singleton que persiste entre escenas (DontDestroyOnLoad).
// Colocar en la escena de exploración; sobrevive al paso a CombatScene.
public class HungerSystem : MonoBehaviour
{
    public static HungerSystem Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private float maxHunger        = 100f;
    [SerializeField] private float decreasePerTick  = 1f;
    [SerializeField] private float tickInterval     = 8f;   // segundos entre descensos

    [Header("UI (exploración)")]
    [SerializeField] private Slider hungerBar;

    private float _currentHunger;
    private float _timer;
    private bool  _inCombat;
    private PlayerCombatant _combatPlayer;

    // En combate, si hambre = 0, Mike pierde 5 HP por turno de jugador
    private const int StarvationDamage = 5;

    public float CurrentHunger => _currentHunger;
    public float HungerPercent  => _currentHunger / maxHunger;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        _currentHunger = maxHunger;
        RefreshBar();
    }

    private void Update()
    {
        if (_inCombat) return;

        _timer += Time.deltaTime;
        if (_timer >= tickInterval)
        {
            _timer -= tickInterval;
            ReduceHunger(decreasePerTick);
        }
    }

    // ── API pública ────────────────────────────────────────────────────────────

    public void Eat(float amount)
    {
        _currentHunger = Mathf.Min(maxHunger, _currentHunger + amount);
        RefreshBar();
    }

    // Llamar desde CombatSceneInitializer al iniciar el combate
    public void EnterCombat(PlayerCombatant player)
    {
        _inCombat     = true;
        _combatPlayer = player;
        player.HungerPercent = HungerPercent;

        if (CombatManager.Instance != null)
            CombatManager.Instance.onPlayerTurnStarted += OnPlayerTurnStarted;
    }

    // Llamar al salir del combate (en VictoryScreen / DefeatScreen)
    public void ExitCombat()
    {
        _inCombat = false;
        if (CombatManager.Instance != null)
            CombatManager.Instance.onPlayerTurnStarted -= OnPlayerTurnStarted;
        _combatPlayer = null;
    }

    // Permite reasignar el Slider de hambre al entrar en una nueva escena
    public void RegisterHungerBar(Slider bar)
    {
        hungerBar = bar;
        RefreshBar();
    }

    // Llamar desde CombatManager cada vez que el jugador realiza una acción.
    // amount: puntos de hambre que se consumen (escala 0-100).
    public void DrainOnAction(float amount)
    {
        if (!_inCombat) return;
        ReduceHunger(amount);
        // Mantener HungerPercent sincronizado en el combatiente
        if (_combatPlayer != null)
            _combatPlayer.HungerPercent = HungerPercent;
    }

    // ── Privados ───────────────────────────────────────────────────────────────

    private void OnPlayerTurnStarted()
    {
        if (_combatPlayer == null || _currentHunger > 0f) return;

        // Daño por inanición directo (sin defensa).
        // TakeDamage aplica defensa, por eso sumamos defense_base como offset.
        _combatPlayer.TakeDamage(StarvationDamage + _combatPlayer.Data.defense);
    }

    private void ReduceHunger(float amount)
    {
        _currentHunger = Mathf.Max(0f, _currentHunger - amount);
        RefreshBar();
    }

    private void RefreshBar()
    {
        if (hungerBar == null) return;
        hungerBar.maxValue = maxHunger;
        hungerBar.value    = _currentHunger;
    }
}
