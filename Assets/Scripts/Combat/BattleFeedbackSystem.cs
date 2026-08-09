using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Muestra: indicador de turno, log de batalla y panel tutorial al inicio.
// Crear un GameObject "BattleFeedback" en el Canvas y adjuntar este script.
// Asignar las referencias en el Inspector.
[DefaultExecutionOrder(-50)]
public class BattleFeedbackSystem : MonoBehaviour
{
    [Header("Indicador de turno")]
    [SerializeField] private TextMeshProUGUI turnIndicatorText;

    [Header("Log de batalla (últimas acciones)")]
    [SerializeField] private TextMeshProUGUI battleLogText;
    [SerializeField] private int             maxLogLines = 6;

    [Header("Panel tutorial")]
    [SerializeField] private GameObject      tutorialPanel;
    [SerializeField] private Button          closeTutorialBtn;

    private readonly Queue<string> _log = new Queue<string>();

    private static readonly Color ColorPlayerTurn = new Color(0.3f,  1f,   0.4f);
    private static readonly Color ColorEnemyTurn  = new Color(1f,   0.55f, 0.1f);
    private static readonly Color ColorSystem     = new Color(0.85f, 0.85f, 0.85f);

    private IEnumerator Start()
    {
        while (CombatManager.Instance == null) yield return null;

        CombatManager.Instance.onPlayerTurnStarted += OnPlayerTurn;
        CombatManager.Instance.onActionPerformed   += OnAction;
        CombatManager.Instance.onPlayerTookDamage  += OnPlayerDamage;
        CombatManager.Instance.onVictory           += OnVictory;
        CombatManager.Instance.onDefeat            += OnDefeat;

        SetTurnText("Iniciando combate…", ColorSystem);
        ShowTutorial();
    }

    private void OnDestroy()
    {
        if (CombatManager.Instance == null) return;
        CombatManager.Instance.onPlayerTurnStarted -= OnPlayerTurn;
        CombatManager.Instance.onActionPerformed   -= OnAction;
        CombatManager.Instance.onPlayerTookDamage  -= OnPlayerDamage;
        CombatManager.Instance.onVictory           -= OnVictory;
        CombatManager.Instance.onDefeat            -= OnDefeat;
    }

    // ── Handlers ───────────────────────────────────────────────────────────────

    private void OnPlayerTurn()
    {
        SetTurnText("► TU TURNO ◄  Elige una acción", ColorPlayerTurn);
        AppendLog("── Tu turno ──");
    }

    private void OnAction(string message)
    {
        // Detectar si el mensaje viene del turno enemigo (no empieza por "Mike", "Usaste" ni "──")
        bool isEnemy = !message.StartsWith("Mike") &&
                       !message.StartsWith("Usaste") &&
                       !message.StartsWith("──");

        if (isEnemy)
        {
            // Actualizar indicador de turno con el nombre del enemigo
            int colon = message.IndexOf(':');
            string enemyName = colon > 0 ? message.Substring(0, colon).Trim() : "Enemigo";
            SetTurnText($"Turno de {enemyName}…  Espera.", ColorEnemyTurn);
        }

        AppendLog(message);
    }

    private void OnPlayerDamage(int dmg)
    {
        // Mensaje de daño recibido con color rojo suave (compatible con rich text de TMP)
        AppendLog($"<color=#FF6B6B>Mike recibe {dmg} de daño</color>");
    }

    private void OnVictory()
    {
        SetTurnText("¡VICTORIA!", ColorPlayerTurn);
        AppendLog("─── ¡VICTORIA! Todos los enemigos derrotados ───");
    }

    private void OnDefeat()
    {
        SetTurnText("DERROTA...", ColorEnemyTurn);
        AppendLog("─── DERROTA… Mike ha caído en combate ───");
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>Limpia el log de batalla al reiniciar un combate.</summary>
    public void ResetLog()
    {
        _log.Clear();
        if (battleLogText != null) battleLogText.text = "";
        SetTurnText("Iniciando combate…", ColorSystem);
    }

    // ── Turn indicator ─────────────────────────────────────────────────────────

    private void SetTurnText(string text, Color color)
    {
        if (turnIndicatorText == null) return;
        turnIndicatorText.text  = text;
        turnIndicatorText.color = color;
    }

    // ── Battle log ─────────────────────────────────────────────────────────────

    private void AppendLog(string line)
    {
        if (battleLogText == null) return;
        _log.Enqueue(line);
        while (_log.Count > maxLogLines) _log.Dequeue();
        battleLogText.text = string.Join("\n", _log);
    }

    // ── Tutorial ───────────────────────────────────────────────────────────────

    private void ShowTutorial()
    {
        if (tutorialPanel == null) return;
        tutorialPanel.SetActive(true);
        closeTutorialBtn?.onClick.AddListener(() => tutorialPanel.SetActive(false));
    }
}
