using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Tooltip de ítems.  Se auto-crea como hijo del Canvas principal.
// Llamar ItemTooltip.Show(text, buttonRect) / ItemTooltip.Hide() desde cualquier sitio.
// Aparece siempre AL LADO del botón que se está hovereando, nunca cruzado a otra zona.
[DefaultExecutionOrder(-60)]
public class ItemTooltip : MonoBehaviour
{
    public static ItemTooltip Instance { get; private set; }

    private Canvas          _canvas;
    private RectTransform   _canvasRect;
    private GameObject      _panel;
    private RectTransform   _panelRect;
    private TextMeshProUGUI _label;

    private const float PanelWidth  = 240f;
    private const float PanelHeight = 90f;
    private const float Gap         = 6f;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _canvas     = GetComponentInParent<Canvas>();
        _canvasRect = _canvas != null ? _canvas.GetComponent<RectTransform>() : null;

        BuildPanel();
    }

    // ── API estática ───────────────────────────────────────────────────────────

    /// <summary>Muestra el tooltip pegado al lado derecho (o izquierdo) del botón dado.</summary>
    public static void Show(string description, RectTransform buttonRect)
    {
        if (Instance == null || string.IsNullOrEmpty(description)) return;
        Instance._label.text = description;
        Instance._panel.SetActive(true);
        Instance.PositionNextTo(buttonRect);
    }

    public static void Hide()
    {
        if (Instance != null) Instance._panel.SetActive(false);
    }

    // ── Posicionamiento ────────────────────────────────────────────────────────

    private void PositionNextTo(RectTransform anchor)
    {
        if (_canvasRect == null || anchor == null) return;

        Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : _canvas.worldCamera;

        // Obtener las 4 esquinas del botón en world-space
        Vector3[] corners = new Vector3[4];
        anchor.GetWorldCorners(corners);
        // corners[0]=BL  [1]=TL  [2]=TR  [3]=BR

        // Convertir esquina derecha (TR y BR) a canvas local
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            RectTransformUtility.WorldToScreenPoint(cam, corners[2]),
            cam, out Vector2 tr);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            RectTransformUtility.WorldToScreenPoint(cam, corners[1]),
            cam, out Vector2 tl);

        float halfW = _canvasRect.rect.width  * 0.5f;
        float halfH = _canvasRect.rect.height * 0.5f;

        // Intentar colocar a la derecha
        float x = tr.x + Gap;
        if (x + PanelWidth > halfW)          // no cabe a la derecha → izquierda
            x = tl.x - PanelWidth - Gap;

        // Alinear verticalmente con la parte superior del botón, clamped
        float y = tr.y;
        if (y - PanelHeight < -halfH)
            y = -halfH + PanelHeight;

        _panelRect.anchoredPosition = new Vector2(x, y);
    }

    // ── Construcción del panel ─────────────────────────────────────────────────

    private void BuildPanel()
    {
        _panel = new GameObject("TooltipPanel");
        _panel.transform.SetParent(transform, false);

        // Anchor al centro del canvas; pivot top-left del panel
        _panelRect            = _panel.AddComponent<RectTransform>();
        _panelRect.anchorMin  = new Vector2(0.5f, 0.5f);
        _panelRect.anchorMax  = new Vector2(0.5f, 0.5f);
        _panelRect.pivot      = new Vector2(0f,   1f);
        _panelRect.sizeDelta  = new Vector2(PanelWidth, PanelHeight);

        Image bg   = _panel.AddComponent<Image>();
        bg.color   = new Color(0.04f, 0.04f, 0.12f, 0.95f);

        Outline ol      = _panel.AddComponent<Outline>();
        ol.effectColor  = new Color(0.5f, 0.5f, 0.75f, 0.8f);
        ol.effectDistance = new Vector2(1.5f, -1.5f);

        // Texto interior
        GameObject tGO = new GameObject("Text");
        tGO.transform.SetParent(_panel.transform, false);
        RectTransform tRT = tGO.AddComponent<RectTransform>();
        tRT.anchorMin = Vector2.zero;
        tRT.anchorMax = Vector2.one;
        tRT.offsetMin = new Vector2(8f,  6f);
        tRT.offsetMax = new Vector2(-8f, -6f);

        _label                    = tGO.AddComponent<TextMeshProUGUI>();
        _label.fontSize           = 13f;
        _label.color              = new Color(0.9f, 0.88f, 0.82f);
        _label.alignment          = TextAlignmentOptions.TopLeft;
        _label.enableWordWrapping = true;
        _label.overflowMode       = TextOverflowModes.Truncate;

        _panel.SetActive(false);
    }
}
