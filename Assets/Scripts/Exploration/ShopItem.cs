using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
// ShopItem  (UI)
// ─────────────────────────────────────────────────────────────────────────────
// Pon este componente en cada botón de la tienda (uno por opción).
// El botón es un elemento normal de la UI de Unity — se puede ver, escalar
// y diseñar libremente desde el Inspector/Canvas.
//
// ShopInteraction se suscribe automáticamente al Button.onClick de cada item.
// ─────────────────────────────────────────────────────────────────────────────
[RequireComponent(typeof(Button))]
public class ShopItem : MonoBehaviour
{
    [Header("Datos del item")]
    [Tooltip("Nombre visible del item (se muestra en la etiqueta del botón si hay un Text/TMP hijo)")]
    public string itemName  = "Objeto";

    [Tooltip("Índice 0, 1 ó 2. ShopInteraction lo asigna automáticamente en orden.")]
    public int    itemIndex = 0;

    // ── Referencia al Button (auto-detectada) ─────────────────────────────────
    [System.NonSerialized] public Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        // Si hay un hijo con TextMeshProUGUI o Text, poner el nombre del item
        var tmp = GetComponentInChildren<TextMeshProUGUI>();
        if (tmp != null && string.IsNullOrEmpty(tmp.text))
            tmp.text = itemName;

        var txt = GetComponentInChildren<Text>();
        if (txt != null && string.IsNullOrEmpty(txt.text))
            txt.text = itemName;
    }
}
