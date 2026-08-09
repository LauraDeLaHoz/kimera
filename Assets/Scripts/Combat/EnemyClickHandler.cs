using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Adjuntar al GameObject de imagen/retrato de cada enemigo en el Canvas.
// Asigna enemyIndex (0, 1, 2…) en el Inspector.
// Al hacer click selecciona ese enemigo como objetivo activo.
[RequireComponent(typeof(Button))]
public class EnemyClickHandler : MonoBehaviour
{
    [SerializeField] private int enemyIndex;

    // Referencia opcional al texto que indica "Seleccionado"
    [SerializeField] private TextMeshProUGUI selectionLabel;

    private Button _btn;

    private void Awake()
    {
        _btn = GetComponent<Button>();
        _btn.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (CombatManager.Instance == null) return;
        if (CombatManager.Instance.State != CombatState.PlayerTurn) return;

        var enemies = CombatManager.Instance.Enemies;
        if (enemyIndex >= enemies.Count) return;

        EnemyCombatant target = enemies[enemyIndex];
        if (!target.IsAlive) return;

        // Notificar a todos los handlers para que deseleccionen, luego este selecciona
        EnemyClickHandler[] all = FindObjectsByType<EnemyClickHandler>(FindObjectsSortMode.None);
        foreach (var h in all) h.SetSelected(false);
        SetSelected(true);

        // Guardar como objetivo en CombatUI si existe
        CombatUI ui = FindFirstObjectByType<CombatUI>();
        ui?.SelectEnemy(target);
    }

    public void SetSelected(bool selected)
    {
        if (selectionLabel != null)
            selectionLabel.text = selected ? "◄ OBJETIVO" : "";
    }
}
