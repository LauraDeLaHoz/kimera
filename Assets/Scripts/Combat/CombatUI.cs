

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// ── Serializable helpers ───────────────────────────────────────────────────────

/// <summary>
/// Estilo visual de un botón de acción.
/// Cambia texto, color de fondo y sprite desde el Inspector de CombatUI.
/// </summary>
[Serializable]
public class ActionButtonStyle
{
    [Tooltip("Texto que muestra el botón")]
    public string label         = "";
    [Tooltip("Color de fondo del botón")]
    public Color  backgroundColor = Color.white;
    [Tooltip("Sprite del fondo (null = sin sprite, usa solo el color)")]
    public Sprite backgroundSprite = null;
}

[Serializable]
public class EnemyHUDElement
{
    public Slider healthBar;
    public TextMeshProUGUI nameLabel;
    public GameObject weaknessIndicator;
    public GameObject selectionHighlight;

    public void UpdateHealth(int current, int max)
    {
        if (healthBar == null) return;
        healthBar.maxValue = max;
        healthBar.value    = current;
    }
}

[Serializable]
public class EnemyCombatView
{
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public ParticleSystem instinctEffect;
}

// ── CombatUI ───────────────────────────────────────────────────────────────────

[DefaultExecutionOrder(-50)]
public class CombatUI : MonoBehaviour
{
    // ── Player HUD ─────────────────────────────────────────────────────────────
    [Header("Player HUD")]
    [SerializeField] private Slider            playerHealthBar;
    [SerializeField] private Slider            playerEnergyBar;
    [SerializeField] private TextMeshProUGUI   playerHealthText;
    [SerializeField] private Image             playerPortrait;
    [SerializeField] private Animator          playerAnimator;

    // ── Enemy HUDs & views ─────────────────────────────────────────────────────
    [Header("Enemy HUDs")]
    [SerializeField] private EnemyHUDElement[] enemyHUDs;
    [SerializeField] private EnemyCombatView[] enemyViews;

    // ── Action Panel ────────────────────────────────────────────────────────────
    [Header("Action Panel")]
    [SerializeField] private GameObject actionPanel;
    [SerializeField] private Button     btnAttack;
    [SerializeField] private Button     btnInstinct;
    [SerializeField] private Button     btnItem;
    [SerializeField] private Button     btnDefend;

    [Header("Estilos de botones (edita aquí → se aplica en Play)")]
    [Tooltip("Estilo del botón ATACAR")]
    public ActionButtonStyle attackStyle   = new ActionButtonStyle { label = "ATACAR",   backgroundColor = new Color(0.65f, 0.13f, 0.13f) };
    [Tooltip("Estilo del botón INSTINTO")]
    public ActionButtonStyle instinctStyle = new ActionButtonStyle { label = "INSTINTO", backgroundColor = new Color(0.13f, 0.38f, 0.65f) };
    [Tooltip("Estilo del botón ÍTEM")]
    public ActionButtonStyle itemStyle     = new ActionButtonStyle { label = "ÍTEM",     backgroundColor = new Color(0.13f, 0.52f, 0.22f) };
    [Tooltip("Estilo del botón DEFENDER")]
    public ActionButtonStyle defendStyle   = new ActionButtonStyle { label = "DEFENDER", backgroundColor = new Color(0.38f, 0.30f, 0.52f) };

    [Header("Sprite retrato del jugador")]
    [Tooltip("Sprite que se muestra en el retrato de Mike en el HUD")]
    public Sprite playerPortraitSprite;

    // ── Item Panel ──────────────────────────────────────────────────────────────
    [Header("Item Panel")]
    [SerializeField] private GameObject itemPanel;
    [SerializeField] private Transform  itemListContainer;
    [SerializeField] private GameObject itemButtonPrefab;

    // ── Analysis Panel ──────────────────────────────────────────────────────────
    [Header("Analysis Panel")]
    [SerializeField] private GameObject      analysisPanel;
    [SerializeField] private TextMeshProUGUI analysisMainText;
    [SerializeField] private TextMeshProUGUI analysisHintText;

    // ── Message Box ─────────────────────────────────────────────────────────────
    [Header("Message Box")]
    [SerializeField] private GameObject      messageBox;
    [SerializeField] private TextMeshProUGUI messageText;

    // ── Damage Numbers ──────────────────────────────────────────────────────────
    [Header("Damage Numbers")]
    [SerializeField] private GameObject damageNumberPrefab; // needs TextMeshPro (3D)

    // ── End Screens ─────────────────────────────────────────────────────────────
    [Header("End Screens")]
    [SerializeField] private GameObject victoryScreen;
    [SerializeField] private GameObject defeatScreen;

    // ── Entrance ────────────────────────────────────────────────────────────────
    [Header("Entrance")]
    [SerializeField] private CanvasGroup mainCanvasGroup;
    [SerializeField] private float       entranceDuration = 1.2f;

    // ── Runtime state ───────────────────────────────────────────────────────────
    private CombatManager     _combatManager;
    private List<ItemData>    _inventory     = new List<ItemData>();
    private EnemyCombatant    _selectedEnemy;
    private Coroutine         _messageRoutine;
    // Runtime quantity snapshot — never modifies ScriptableObject assets
    private readonly Dictionary<ItemData, int> _runtimeQty = new Dictionary<ItemData, int>();

    private static readonly Color FlashWhite = Color.white;
    private static readonly Color FlashRed   = new Color(1f, 0.2f, 0.2f);

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        ApplyButtonStyles();

        _combatManager = CombatManager.Instance;
        if (_combatManager == null) return;

        _combatManager.onShowAnalysisPanel  += OnAnalysisPanelRequested;
        _combatManager.onActionPerformed    += ShowMessage;
        _combatManager.onPlayerTurnStarted  += OnPlayerTurnStarted;
        _combatManager.onVictory            += ShowVictoryScreen;
        _combatManager.onDefeat             += ShowDefeatScreen;

        WireActionButtons();
        WireEnemySelectionButtons();
        WireAnalysisPanelClose();
    }

#if UNITY_EDITOR
    // Preview en tiempo de edición: los cambios de estilo se ven sin entrar en Play
    private void OnValidate() => ApplyButtonStyles();
#endif

    /// <summary>
    /// Aplica los estilos definidos en el Inspector a los botones y al retrato del jugador.
    /// Se llama automáticamente en Awake (en Play) y en OnValidate (en el Editor).
    /// </summary>
    public void ApplyButtonStyles()
    {
        ApplyToButton(btnAttack,   attackStyle);
        ApplyToButton(btnInstinct, instinctStyle);
        ApplyToButton(btnItem,     itemStyle);
        ApplyToButton(btnDefend,   defendStyle);

        if (playerPortrait != null && playerPortraitSprite != null)
            playerPortrait.sprite = playerPortraitSprite;
    }

    private static void ApplyToButton(Button btn, ActionButtonStyle style)
    {
        if (btn == null || style == null) return;

        // Fondo del botón (Image en el propio GO del botón)
        Image bg = btn.GetComponent<Image>();
        if (bg != null)
        {
            bg.color = style.backgroundColor;
            if (style.backgroundSprite != null)
                bg.sprite = style.backgroundSprite;
        }

        // Sincronizar también los colores del ColorBlock del Button
        ColorBlock cb = btn.colors;
        cb.normalColor      = style.backgroundColor;
        cb.highlightedColor = style.backgroundColor + new Color(0.15f, 0.15f, 0.15f);
        cb.pressedColor     = style.backgroundColor - new Color(0.15f, 0.15f, 0.15f);
        btn.colors = cb;

        // Texto (primer TextMeshProUGUI hijo)
        TextMeshProUGUI lbl = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (lbl != null && !string.IsNullOrEmpty(style.label))
            lbl.text = style.label;
    }

    private void OnDestroy()
    {
        if (_combatManager == null) return;
        _combatManager.onShowAnalysisPanel  -= OnAnalysisPanelRequested;
        _combatManager.onActionPerformed    -= ShowMessage;
        _combatManager.onPlayerTurnStarted  -= OnPlayerTurnStarted;
        _combatManager.onVictory            -= ShowVictoryScreen;
        _combatManager.onDefeat             -= ShowDefeatScreen;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void UpdateHUD()
    {
        if (_combatManager?.Player == null) return;
        PlayerCombatant player = _combatManager.Player;

        if (playerHealthBar != null)
        {
            playerHealthBar.maxValue = player.MaxHealth;
            playerHealthBar.value    = player.CurrentHealth;
        }
        else Debug.LogWarning("[CombatUI] playerHealthBar nulo — ejecuta Kimera/10 para reparar.");

        if (playerEnergyBar != null)
        {
            playerEnergyBar.maxValue = player.Data.maxEnergy;
            playerEnergyBar.value    = player.CurrentEnergy;
        }
        else Debug.LogWarning("[CombatUI] playerEnergyBar nulo — ejecuta Kimera/10 para reparar.");

        if (playerHealthText != null)
            playerHealthText.text = $"HP: {player.CurrentHealth} / {player.MaxHealth}";

        for (int i = 0; i < enemyHUDs.Length && i < _combatManager.Enemies.Count; i++)
        {
            EnemyCombatant enemy = _combatManager.Enemies[i];
            enemyHUDs[i].UpdateHealth(enemy.CurrentHealth, enemy.Data.maxHealth);

            if (enemyHUDs[i].weaknessIndicator != null)
                enemyHUDs[i].weaknessIndicator.SetActive(enemy.WeaknessExposed);

            // Mostrar nombre + stats del enemigo en el nameLabel
            if (enemyHUDs[i].nameLabel != null)
            {
                string status = !enemy.IsAlive ? "  [DERROTADO]" :
                                enemy.WeaknessExposed ? "  ★ DÉBIL" : "";
                enemyHUDs[i].nameLabel.text =
                    $"{enemy.Data.enemyName}{status}\n" +
                    $"ATK {enemy.Data.attackPower}   DEF {enemy.Data.defense}   VEL {enemy.Data.speed}";
            }
        }
    }

    public void EnablePlayerActions(bool enable)
    {
        actionPanel.SetActive(enable);
    }

    public void ShowAnalysisPanel(string main, string hint)
    {
        analysisPanel.SetActive(true);
        analysisMainText.text = main;
        analysisHintText.text = hint;
    }

    public void HideAnalysisPanel()
    {
        analysisPanel.SetActive(false);
    }

    public void ShowDamageNumber(Vector3 worldPos, int amount, bool isEnemy)
    {
        if (damageNumberPrefab == null) return;
        GameObject obj = Instantiate(damageNumberPrefab, worldPos, Quaternion.identity);
        TextMeshPro tmp = obj.GetComponentInChildren<TextMeshPro>();
        if (tmp != null)
        {
            tmp.text  = $"-{amount}";
            tmp.color = isEnemy ? Color.red : Color.yellow;
        }
        Destroy(obj, 1.5f);
    }

    public void ShowMessage(string message)
    {
        if (_messageRoutine != null) StopCoroutine(_messageRoutine);
        _messageRoutine = StartCoroutine(MessageRoutine(message));
        UpdateHUD();   // mantiene stats/estado al día en cualquier turno
    }

    public void ShowWeaknessHit(EnemyCombatant enemy)
    {
        int i = IndexOf(enemy);
        if (i >= 0 && i < enemyViews.Length)
            StartCoroutine(WeaknessFlashRoutine(enemyViews[i].spriteRenderer));
        ShowMessage("¡Golpeas en el punto débil!");
    }

    public void HighlightEnemy(EnemyCombatant enemy, bool highlight)
    {
        int i = IndexOf(enemy);
        if (i >= 0 && i < enemyHUDs.Length && enemyHUDs[i].selectionHighlight != null)
            enemyHUDs[i].selectionHighlight.SetActive(highlight);
    }

    public void ShowEnemyActionLabel(string enemyName, string action)
    {
        ShowMessage($"{enemyName}: {action}");
    }

    public void PlayAttackAnimation(PlayerCombatant player, EnemyCombatant target)
    {
        if (playerAnimator != null) playerAnimator.SetTrigger("Attack");
    }

    public void PlayEnemyAttackAnimation(EnemyCombatant enemy, PlayerCombatant target)
    {
        if (enemyViews == null) return;
        int i = IndexOf(enemy);
        if (i < 0 || i >= enemyViews.Length || enemyViews[i] == null) return;
        Animator anim = enemyViews[i].animator;
        if (anim != null) anim.SetTrigger("Attack");
    }

    public void PlayInstinctEffect(EnemyCombatant enemy)
    {
        if (enemyViews == null) return;
        int i = IndexOf(enemy);
        if (i < 0 || i >= enemyViews.Length || enemyViews[i] == null) return;
        ParticleSystem ps = enemyViews[i].instinctEffect;
        if (ps != null) ps.Play();
    }

    public void SelectEnemy(EnemyCombatant enemy)
    {
        if (_selectedEnemy != null) HighlightEnemy(_selectedEnemy, false);
        _selectedEnemy = enemy;
        if (_selectedEnemy != null) HighlightEnemy(_selectedEnemy, true);
    }

    // Ocultar los HUDs de enemigos que no están en el combate actual.
    // Llamar después de InitializeCombat.
    public void InitEnemyHUDs(int activeEnemyCount)
    {
        for (int i = 0; i < enemyHUDs.Length; i++)
        {
            if (enemyHUDs[i] == null) continue;
            Transform root = enemyHUDs[i].nameLabel  != null ? enemyHUDs[i].nameLabel.transform.parent
                           : enemyHUDs[i].healthBar  != null ? enemyHUDs[i].healthBar.transform.parent
                           : null;
            if (root != null) root.gameObject.SetActive(i < activeEnemyCount);
        }

        // Refrescar todos los EnemyActionDisplay para que muestren/oculten
        // según la lista de enemigos del combate actual (normal, reintento o boss).
        foreach (var display in GetComponentsInChildren<EnemyActionDisplay>(includeInactive: true))
            display.Refresh();
    }

    public void ShowCombatEntrance()
    {
        StartCoroutine(FadeInRoutine());
    }

    public void ShowVictoryScreen()
    {
        EnablePlayerActions(false);
        victoryScreen?.SetActive(true);
    }

    public void ShowDefeatScreen()
    {
        EnablePlayerActions(false);
        defeatScreen?.SetActive(true);
    }

    // Llamar desde el inicializador de combate antes de comenzar.
    // Crea un snapshot de cantidades en runtime — nunca modifica el ScriptableObject.
    public void SetInventory(List<ItemData> inventory)
    {
        _inventory = inventory;
        _runtimeQty.Clear();
        foreach (var item in inventory)
            if (item != null) _runtimeQty[item] = item.quantity;
        UpdateItemList();
    }

    /// <summary>
    /// Restablece el estado visual de la UI antes de un nuevo combate.
    /// Llamar desde InSceneCombatController.InitializeCombatLogic() antes de InitializeCombat.
    /// </summary>
    public void ResetForNewCombat()
    {
        // Detener cualquier mensaje en curso
        if (_messageRoutine != null)
        {
            StopCoroutine(_messageRoutine);
            _messageRoutine = null;
        }

        // Ocultar todas las pantallas y paneles
        if (victoryScreen != null) victoryScreen.SetActive(false);
        if (defeatScreen  != null) defeatScreen.SetActive(false);
        if (analysisPanel != null) analysisPanel.SetActive(false);
        if (itemPanel     != null) itemPanel.SetActive(false);
        if (messageBox    != null) messageBox.SetActive(false);

        // Acciones desactivadas hasta que empiece el turno del jugador
        EnablePlayerActions(false);

        // Limpiar selección de enemigo
        if (_selectedEnemy != null)
        {
            HighlightEnemy(_selectedEnemy, false);
            _selectedEnemy = null;
        }

        // Limpiar el log de batalla para que no queden mensajes del combate anterior
        FindFirstObjectByType<BattleFeedbackSystem>()?.ResetLog();

        // Ocultar tooltip si estaba visible
        ItemTooltip.Hide();
    }

    public void UpdateItemList()
    {
        if (itemListContainer == null || itemButtonPrefab == null) return;
        ItemTooltip.Hide();   // asegurar que el tooltip desaparece al reconstruir la lista

        foreach (Transform child in itemListContainer)
            Destroy(child.gameObject);

        foreach (ItemData item in _inventory)
        {
            if (item == null) continue;
            _runtimeQty.TryGetValue(item, out int qty);
            if (qty <= 0) continue;   // agotado este combate — no mostrar

            GameObject btn  = Instantiate(itemButtonPrefab, itemListContainer);
            TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = $"{item.itemName}  ×{qty}";

            Button button = btn.GetComponent<Button>();
            if (button != null)
            {
                ItemData captured = item;
                button.onClick.AddListener(() => OnItemSelected(captured));
            }

            // Tooltip: aparece al lado del botón al pasar el cursor
            if (!string.IsNullOrEmpty(item.description))
            {
                EventTrigger trigger   = btn.GetComponent<EventTrigger>() ?? btn.AddComponent<EventTrigger>();
                RectTransform btnRect  = btn.GetComponent<RectTransform>();
                string desc = item.description;

                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => ItemTooltip.Show(desc, btnRect));
                trigger.triggers.Add(enter);

                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => ItemTooltip.Hide());
                trigger.triggers.Add(exit);
            }
        }
    }

    // ── Event subscribers ──────────────────────────────────────────────────────

    private void OnAnalysisPanelRequested(string mainText)
    {
        // Recupera weaknessHint del primer enemigo con debilidad expuesta
        string hint = _combatManager.Enemies
            .FirstOrDefault(e => e.WeaknessExposed)?.Data.weaknessHint ?? string.Empty;
        ShowAnalysisPanel(mainText, hint);
    }

    private void OnPlayerTurnStarted()
    {
        EnablePlayerActions(true);
        UpdateHUD();
    }

    // ── Button wiring ──────────────────────────────────────────────────────────

    // Hace clicable el contenedor de cada EnemyHUD para seleccionarlo como objetivo.
    private void WireEnemySelectionButtons()
    {
        if (enemyHUDs == null) return;
        for (int i = 0; i < enemyHUDs.Length; i++)
        {
            if (enemyHUDs[i] == null) continue;

            // Busca el contenedor padre del HUD (nameLabel o healthBar son hijos directos)
            Transform container = enemyHUDs[i].nameLabel  != null ? enemyHUDs[i].nameLabel.transform.parent
                                : enemyHUDs[i].healthBar  != null ? enemyHUDs[i].healthBar.transform.parent
                                : null;
            if (container == null) continue;

            int idx = i;
            Button btn = container.GetComponent<Button>();
            if (btn == null) btn = container.gameObject.AddComponent<Button>();

            // Imagen de fondo transparente para que el área sea clickeable
            if (container.GetComponent<Image>() == null)
            {
                Image bg = container.gameObject.AddComponent<Image>();
                bg.color = new Color(0, 0, 0, 0.01f);   // casi transparente pero recibe raycast
            }

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (_combatManager == null || _combatManager.State != CombatState.PlayerTurn) return;
                if (idx >= _combatManager.Enemies.Count) return;
                EnemyCombatant target = _combatManager.Enemies[idx];
                if (!target.IsAlive) return;
                SelectEnemy(target);
                ShowMessage($"Objetivo: {target.Data.enemyName}");
            });
        }
    }

    // Añade un botón "Continuar" al analysis panel para poder cerrarlo.
    private void WireAnalysisPanelClose()
    {
        if (analysisPanel == null) return;

        // Buscar si ya hay un botón hijo
        Button existing = analysisPanel.GetComponentInChildren<Button>();
        if (existing != null)
        {
            existing.onClick.RemoveAllListeners();
            existing.onClick.AddListener(HideAnalysisPanel);
            return;
        }

        // Crear botón "Continuar" en la parte inferior del panel
        GameObject btnGO = new GameObject("BtnContinuar");
        btnGO.transform.SetParent(analysisPanel.transform, false);
        RectTransform rt = btnGO.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.1f, 0.05f);
        rt.anchorMax = new Vector2(0.9f, 0.22f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.15f, 0.55f, 0.15f);

        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(HideAnalysisPanel);

        GameObject lblGO = new GameObject("Label");
        lblGO.transform.SetParent(btnGO.transform, false);
        RectTransform lblRt = lblGO.AddComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;

        var tmp = lblGO.AddComponent<TextMeshProUGUI>();
        tmp.text      = "Continuar →";
        tmp.fontSize  = 16;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color     = Color.white;
    }

    private void WireActionButtons()
    {
        btnAttack?.onClick.AddListener(() =>
        {
            if (_selectedEnemy == null) _selectedEnemy = FirstAliveEnemy();
            if (_selectedEnemy == null) return;
            EnablePlayerActions(false);
            PlayAttackAnimation(_combatManager.Player, _selectedEnemy);
            _combatManager.PlayerAttack(_selectedEnemy);
            UpdateHUD();
        });

        btnInstinct?.onClick.AddListener(() =>
        {
            if (_selectedEnemy == null) _selectedEnemy = FirstAliveEnemy();
            if (_selectedEnemy == null) return;
            EnablePlayerActions(false);
            PlayInstinctEffect(_selectedEnemy);
            _combatManager.PlayerUseInstinct(_selectedEnemy);
        });

        btnItem?.onClick.AddListener(() =>
        {
            bool open = !itemPanel.activeSelf;
            itemPanel.SetActive(open);
            if (open)  UpdateItemList();
            else       ItemTooltip.Hide();
        });

        btnDefend?.onClick.AddListener(() =>
        {
            EnablePlayerActions(false);
            _combatManager.PlayerDefend();
            UpdateHUD();
        });
    }

    // ── Item selection ─────────────────────────────────────────────────────────

    private void OnItemSelected(ItemData item)
    {
        ItemTooltip.Hide();
        if (_runtimeQty.ContainsKey(item)) _runtimeQty[item]--;
        itemPanel.SetActive(false);
        _combatManager.PlayerUseItem(item);
        UpdateItemList();
        UpdateHUD();
    }

    // ── Coroutines ─────────────────────────────────────────────────────────────

    private IEnumerator MessageRoutine(string message)
    {
        messageBox.SetActive(true);
        messageText.text = message;
        yield return new WaitForSeconds(2f);
        messageBox.SetActive(false);
    }

    private IEnumerator WeaknessFlashRoutine(SpriteRenderer sr)
    {
        if (sr == null) yield break;
        Color original = sr.color;
        sr.color = FlashWhite;
        yield return new WaitForSeconds(0.1f);
        sr.color = FlashRed;
        yield return new WaitForSeconds(0.3f);
        sr.color = original;
    }

    private IEnumerator FadeInRoutine()
    {
        if (mainCanvasGroup == null) yield break;
        mainCanvasGroup.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < entranceDuration)
        {
            elapsed += Time.deltaTime;
            mainCanvasGroup.alpha = Mathf.Clamp01(elapsed / entranceDuration);
            yield return null;
        }
        mainCanvasGroup.alpha = 1f;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private int IndexOf(EnemyCombatant enemy)
    {
        for (int i = 0; i < _combatManager.Enemies.Count; i++)
            if (_combatManager.Enemies[i] == enemy) return i;
        return -1;
    }

    private EnemyCombatant FirstAliveEnemy() =>
        _combatManager.Enemies.FirstOrDefault(e => e.IsAlive);
}
