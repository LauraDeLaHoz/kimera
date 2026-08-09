using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Maneja la transición post-combate:
//   Combate normal  →  pantalla nivel subido  →  pantalla comida  →  boss
//   Boss (victoria) →  pantalla "ganaste de suerte"  →  Continuará  →  botones
//   Boss (derrota)  →  pantalla "es normal que pierdas"  →  Continuará  →  botones
[DefaultExecutionOrder(-50)]
public class PostCombatFlow : MonoBehaviour
{
    // ── Referencias a pantallas pre-construidas ────────────────────────────────
    // Ejecuta Kimera/15 para crear los GOs en _CombatCanvas → _PostCombatScreens.
    // Una vez creados puedes seleccionar cada GO en la Jerarquía y cambiar los
    // sprites de los componentes Image (fondos, botones, artwork) en el Inspector.

    [System.Serializable]
    public class PCF_LevelUpScreen
    {
        [Tooltip("Raíz de la pantalla (Kimera/15 la crea). Empieza inactiva.")]
        public GameObject      root;
        [Tooltip("Slot de artwork. Asigna tu sprite aquí en el Inspector.")]
        public Image           artworkSlot;
        public TextMeshProUGUI titleLabel;
        public TextMeshProUGUI statsLabel;
        public TextMeshProUGUI flavorLabel;
        public Button          continueButton;
    }

    [System.Serializable]
    public class PCF_MealScreen
    {
        [Tooltip("Raíz de la pantalla (Kimera/15 la crea). Empieza inactiva.")]
        public GameObject        root;
        [Tooltip("Slot de artwork de La Guía (lado derecho).")]
        public Image             artworkSlot;
        public TextMeshProUGUI   headerLabel;
        public TextMeshProUGUI   dialogLabel;
        public TextMeshProUGUI   sectionLabel;
        public TextMeshProUGUI   commentLabel;
        public Button[]          mealButtons;       // [3]
        public TextMeshProUGUI[] mealNameLabels;    // [3]
        public TextMeshProUGUI[] mealDescLabels;    // [3]
    }

    [System.Serializable]
    public class PCF_DefeatScreen
    {
        [Tooltip("Raíz de la pantalla (Kimera/15 la crea). Empieza inactiva.")]
        public GameObject      root;
        [Tooltip("Slot de artwork de derrota.")]
        public Image           artworkSlot;
        public Button          retryButton;
        public Button          homeButton;
    }

    [System.Serializable]
    public class PCF_BossEndScreen
    {
        [Tooltip("Raíz de la pantalla (Kimera/15 la crea). Empieza inactiva.")]
        public GameObject      root;
        [Tooltip("Slot de artwork del boss end.")]
        public Image           artworkSlot;
        public TextMeshProUGUI titleLabel;
        public TextMeshProUGUI storyLabel;
        [Tooltip("'— C O N T I N U A R Á —'. Empieza con alpha 0 en el editor.")]
        public TextMeshProUGUI continuedLabel;
        public Button          homeButton;
        public Button          quitButton;
    }

    // ── Campos de configuración ────────────────────────────────────────────────

    [Header("Escena inicial (botón 'Volver al inicio')")]
    [SerializeField] private string startSceneName = "SampleScene";

    [Header("Canvas donde se muestran los overlays (asignar _CombatCanvas)")]
    [Tooltip("Arrastra aquí el GO '_CombatCanvas'. Si está vacío, se busca automáticamente.")]
    [SerializeField] private Canvas postCombatCanvas;

    // ── Pantallas pre-construidas ──────────────────────────────────────────────
    // Ejecuta Kimera/15 para crear los GOs y cablear estas referencias automáticamente.
    //
    // Para cambiar sprites en la Jerarquía:
    //   1. Expande _CombatCanvas → _PostCombatScreens en la Jerarquía.
    //   2. Selecciona el GO de la pantalla (ej. LevelUpScreen → ContinueButton).
    //   3. En el Inspector, cambia el sprite del componente Image.
    //
    // Si estos campos están vacíos, se usa el sistema dinámico de fallback (igual que antes).

    [Header("Pantallas pre-construidas (ejecuta Kimera/15 para crearlas)")]
    [SerializeField] private PCF_LevelUpScreen levelUpScreenRefs  = new PCF_LevelUpScreen();
    [SerializeField] private PCF_MealScreen    mealScreenRefs     = new PCF_MealScreen();
    [SerializeField] private PCF_DefeatScreen  defeatScreenRefs   = new PCF_DefeatScreen();
    [SerializeField] private PCF_BossEndScreen bossWinScreenRefs  = new PCF_BossEndScreen();
    [SerializeField] private PCF_BossEndScreen bossLoseScreenRefs = new PCF_BossEndScreen();

    // ── Artwork por pantalla (Kimera/13 — compatibilidad) ─────────────────────
    // Si usas pantallas pre-construidas puedes asignar el sprite directamente en
    // artworkSlot de la Jerarquía. Estos campos son fallback para el sistema dinámico.

    [Header("Artwork de post-combate (ejecuta Kimera/13 para crear los GOs)")]
    [Tooltip("Image del GO 'LevelUpArtwork' bajo PostCombatArtwork.")]
    [SerializeField] private Image levelUpArtworkImage;
    [Tooltip("Image del GO 'MealArtwork' bajo PostCombatArtwork.")]
    [SerializeField] private Image mealArtworkImage;
    [Tooltip("Image del GO 'DefeatArtwork' bajo PostCombatArtwork.")]
    [SerializeField] private Image defeatArtworkImage;
    [Tooltip("Image del GO 'BossWinArtwork' bajo PostCombatArtwork.")]
    [SerializeField] private Image bossWinArtworkImage;
    [Tooltip("Image del GO 'BossLoseArtwork' bajo PostCombatArtwork.")]
    [SerializeField] private Image bossLoseArtworkImage;
    [Tooltip("Image del GO 'BossCharacterSprite' bajo PostCombatArtwork (creado por Kimera/13). " +
             "Si está vacío, se usa 'Boss Sprite' como fallback.")]
    [SerializeField] private Image bossCharacterImage;
    [Tooltip("Sprite directo de la Hiena. Se usa si 'Boss Character Image' no tiene sprite asignado. " +
             "Arrastra aquí el sprite de la Hiena desde el Project.")]
    [SerializeField] private Sprite bossSprite;

    [Header("EnemyData de la Hiena (fuente de sprite y animación para el boss fight)")]
    [Tooltip("Arrastra aquí el asset 'Enemy_Hiena' desde Assets/ScriptableObjects/Data/.\n" +
             "El boss fight usará automáticamente su sprite e idle animation frames.\n" +
             "Tiene prioridad sobre 'Boss Sprite' y 'Boss Character Image'.")]
    [SerializeField] private EnemyData bossEnemyData;

    // ── Opciones de comida ─────────────────────────────────────────────────────

    private struct MealOption
    {
        public string name, description, guideComment;
        public float  hungerRestore;
        public int    hpRestore, energyRestore;
    }

    private static readonly MealOption[] Meals = new MealOption[]
    {
        new MealOption {
            name          = "Carne Asada",
            description   = "+50 Hambre  +10 HP\nSustanciosa. Te mantiene en pie.",
            guideComment  = "\"Es de un animal que cacé al amanecer. Te dará fuerza para aguantar.\"",
            hungerRestore = 50f, hpRestore = 10, energyRestore = 0
        },
        new MealOption {
            name          = "Fruta Kime",
            description   = "+30 Hambre  +15 HP  +20 Energía\nLa fruta de tu mutación. Activa tu instinto.",
            guideComment  = "\"Reconoces el sabor. Kime. Confía en lo que te ha dado.\"",
            hungerRestore = 30f, hpRestore = 15, energyRestore = 20
        },
        new MealOption {
            name          = "Ración de Campo",
            description   = "+45 Hambre  +25 HP\nEquilibrada. Lo mejor antes de una batalla dura.",
            guideComment  = "\"Lo preparé pensando en ti. Come todo, no dejes nada.\"",
            hungerRestore = 45f, hpRestore = 25, energyRestore = 0
        },
    };

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private IEnumerator Start()
    {
        while (CombatManager.Instance == null) yield return null;
        CombatManager.Instance.onVictory += OnVictory;
        CombatManager.Instance.onDefeat  += OnDefeat;
    }

    private void OnDestroy()
    {
        if (CombatManager.Instance == null) return;
        CombatManager.Instance.onVictory -= OnVictory;
        CombatManager.Instance.onDefeat  -= OnDefeat;
    }

    // ── Handlers ───────────────────────────────────────────────────────────────

    private void OnVictory()
    {
        if (LevelUpData.IsBossFight)
            StartCoroutine(ShowBossEndScreen(won: true));
        else
            StartCoroutine(ShowLevelUpScreen());
    }

    private void OnDefeat()
    {
        if (LevelUpData.IsBossFight)
            StartCoroutine(ShowBossEndScreen(won: false));
        else
            StartCoroutine(ShowRegularDefeatScreen());
    }

    // ── Pantalla: NIVEL SUBIDO ─────────────────────────────────────────────────

    private IEnumerator ShowLevelUpScreen()
    {
        yield return new WaitForSeconds(1.2f);

        // ── Pantalla pre-construida ────────────────────────────────────────────
        if (levelUpScreenRefs.root != null)
        {
            levelUpScreenRefs.root.SetActive(true);
            yield return StartCoroutine(FadeIn(levelUpScreenRefs.root, 0.45f));

            // Artwork: usa sprite ya asignado en el slot, o el de Kimera/13 como fallback
            if (levelUpScreenRefs.artworkSlot != null)
            {
                Sprite art = GetSprite(levelUpArtworkImage);
                if (levelUpScreenRefs.artworkSlot.sprite == null && art != null)
                    levelUpScreenRefs.artworkSlot.sprite = art;
                levelUpScreenRefs.artworkSlot.enabled = levelUpScreenRefs.artworkSlot.sprite != null;
            }

            // Textos
            if (levelUpScreenRefs.titleLabel != null)
            {
                levelUpScreenRefs.titleLabel.text  = "¡ NIVEL SUBIDO !";
                levelUpScreenRefs.titleLabel.color = new Color(1f, 0.9f, 0.1f);
                StartCoroutine(PulseColor(levelUpScreenRefs.titleLabel,
                    new Color(1f, 0.9f, 0.1f), new Color(1f, 0.45f, 0f)));
            }
            if (levelUpScreenRefs.statsLabel != null)
                levelUpScreenRefs.statsLabel.text =
                    $"<color=#88ff88>+{LevelUpData.BonusMaxHP} HP máx</color>   " +
                    $"<color=#ff8844>+{LevelUpData.BonusAttack} ATK</color>   " +
                    $"<color=#88aaff>+{LevelUpData.BonusMaxEnergy} Energía máx</color>";
            if (levelUpScreenRefs.flavorLabel != null)
                levelUpScreenRefs.flavorLabel.text =
                    "<size=13><color=#bbbbbb>Mike siente el poder del Kime fluir en sus venas.\n" +
                    "Pero la batalla más dura aún está por venir…</color></size>";

            // Botón
            if (levelUpScreenRefs.continueButton != null)
            {
                levelUpScreenRefs.continueButton.onClick.RemoveAllListeners();
                levelUpScreenRefs.continueButton.onClick.AddListener(() => {
                    StopAllCoroutines();
                    levelUpScreenRefs.root.SetActive(false);
                    StartCoroutine(ShowMealScreen());
                });
            }
            yield break;
        }

        // ── Fallback dinámico ──────────────────────────────────────────────────
        Canvas canvas = GetOverlayCanvas();
        if (canvas == null) yield break;

        GameObject overlay = BuildOverlay(canvas.transform, new Color(0f, 0f, 0.04f, 0.96f));
        yield return StartCoroutine(FadeIn(overlay, 0.45f));

        Sprite levelUpArt = GetSprite(levelUpArtworkImage);
        if (levelUpArt != null)
            Img(overlay.transform, levelUpArt,
                new Vector2(0.25f, 0.55f), new Vector2(0.75f, 0.92f));
        else
            ColorBlock(overlay.transform, "…HIENA DE LA ÉLITE AGUARDA…",
                new Color(0.12f, 0.04f, 0.18f), new Color(1f, 0.3f, 0.05f),
                new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.92f));

        var title = Lbl(overlay.transform, "¡ NIVEL SUBIDO !",
            34, FontStyles.Bold, new Color(1f, 0.9f, 0.1f),
            0f, 0.50f, 1f, 0.62f);
        title.alignment = TextAlignmentOptions.Center;
        StartCoroutine(PulseColor(title, new Color(1f,0.9f,0.1f), new Color(1f,0.45f,0f)));

        Lbl(overlay.transform,
            $"<color=#88ff88>+{LevelUpData.BonusMaxHP} HP máx</color>   " +
            $"<color=#ff8844>+{LevelUpData.BonusAttack} ATK</color>   " +
            $"<color=#88aaff>+{LevelUpData.BonusMaxEnergy} Energía máx</color>",
            18, FontStyles.Normal, Color.white, 0f, 0.41f, 1f, 0.51f)
            .alignment = TextAlignmentOptions.Center;

        Lbl(overlay.transform,
            "<size=13><color=#bbbbbb>Mike siente el poder del Kime fluir en sus venas.\n" +
            "Pero la batalla más dura aún está por venir…</color></size>",
            14, FontStyles.Normal, Color.white, 0.05f, 0.29f, 0.95f, 0.42f)
            .alignment = TextAlignmentOptions.Center;

        Btn(overlay.transform, "Continuar  →", new Color(0.15f, 0.45f, 0.15f),
            0.25f, 0.05f, 0.75f, 0.18f, () => {
                StopAllCoroutines();
                Destroy(overlay);
                StartCoroutine(ShowMealScreen());
            });
    }

    // ── Pantalla: LA GUÍA OFRECE COMIDA ───────────────────────────────────────

    private IEnumerator ShowMealScreen()
    {
        // ── Pantalla pre-construida ────────────────────────────────────────────
        if (mealScreenRefs.root != null)
        {
            mealScreenRefs.root.SetActive(true);
            yield return StartCoroutine(FadeIn(mealScreenRefs.root, 0.35f));

            // Artwork
            if (mealScreenRefs.artworkSlot != null)
            {
                Sprite art = GetSprite(mealArtworkImage);
                if (mealScreenRefs.artworkSlot.sprite == null && art != null)
                    mealScreenRefs.artworkSlot.sprite = art;
                mealScreenRefs.artworkSlot.enabled = mealScreenRefs.artworkSlot.sprite != null;
            }

            // Textos fijos
            if (mealScreenRefs.headerLabel  != null) mealScreenRefs.headerLabel.text  = "— La Guía —";
            if (mealScreenRefs.dialogLabel  != null) mealScreenRefs.dialogLabel.text  =
                "\"Espera, Mike. Antes de que entres…\n" +
                "la Hiena trabaja para la Élite. Es la más peligrosa que has enfrentado.\n" +
                "Toma, come algo. Necesitarás fuerzas.\"";
            if (mealScreenRefs.sectionLabel != null) mealScreenRefs.sectionLabel.text = "── Elige qué comer ──";
            if (mealScreenRefs.commentLabel != null) mealScreenRefs.commentLabel.text = "";

            // Botones de comida
            bool hasBtns = mealScreenRefs.mealButtons != null &&
                           mealScreenRefs.mealButtons.Length >= Meals.Length;

            for (int i = 0; i < Meals.Length; i++)
            {
                if (!hasBtns) break;
                var btn = mealScreenRefs.mealButtons[i];
                if (btn == null) continue;

                MealOption meal = Meals[i];

                // Etiquetas de nombre y descripción
                bool hasNames = mealScreenRefs.mealNameLabels != null &&
                                mealScreenRefs.mealNameLabels.Length > i;
                bool hasDescs = mealScreenRefs.mealDescLabels != null &&
                                mealScreenRefs.mealDescLabels.Length > i;
                if (hasNames && mealScreenRefs.mealNameLabels[i] != null)
                    mealScreenRefs.mealNameLabels[i].text = $"<b>{meal.name}</b>";
                if (hasDescs && mealScreenRefs.mealDescLabels[i] != null)
                    mealScreenRefs.mealDescLabels[i].text = meal.description.Split('\n')[0];

                // Hover: EventTrigger dinámico (los sprites del botón ya están pre-configurados)
                var bg          = btn.GetComponent<Image>();
                var cmt         = mealScreenRefs.commentLabel;
                Color normalCol = bg != null ? bg.color : new Color(0.18f, 0.12f, 0.06f, 0.95f);
                Color hoverCol  = new Color(Mathf.Clamp01(normalCol.r + 0.10f),
                                            Mathf.Clamp01(normalCol.g + 0.06f),
                                            Mathf.Clamp01(normalCol.b + 0.02f), normalCol.a);
                string comment  = meal.guideComment;

                // Reutilizar EventTrigger existente o añadir uno nuevo
                var trig = btn.gameObject.GetComponent<UnityEngine.EventSystems.EventTrigger>()
                        ?? btn.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
                trig.triggers.Clear();
                AddTrigger(trig, UnityEngine.EventSystems.EventTriggerType.PointerEnter,
                    _ => { if (cmt != null) cmt.text = comment; if (bg != null) bg.color = hoverCol; });
                AddTrigger(trig, UnityEngine.EventSystems.EventTriggerType.PointerExit,
                    _ => { if (cmt != null) cmt.text = "";       if (bg != null) bg.color = normalCol; });

                // Click
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => {
                    LevelUpData.MealHungerRestore = meal.hungerRestore;
                    LevelUpData.MealHPRestore     = meal.hpRestore;
                    LevelUpData.MealEnergyRestore = meal.energyRestore;
                    mealScreenRefs.root.SetActive(false);
                    StartBossFight();
                });
            }
            yield break;
        }

        // ── Fallback dinámico ──────────────────────────────────────────────────
        Canvas canvas = GetOverlayCanvas();
        if (canvas == null) yield break;

        GameObject overlay = BuildOverlay(canvas.transform, new Color(0.04f, 0.02f, 0f, 0.97f));
        yield return StartCoroutine(FadeIn(overlay, 0.35f));

        // Artwork de La Guía — lado derecho si está asignado
        Sprite mealArt = GetSprite(mealArtworkImage);
        if (mealArt != null)
            Img(overlay.transform, mealArt,
                new Vector2(0.58f, 0.22f), new Vector2(1.00f, 0.98f));

        // Encabezado y diálogo — ocupa todo el ancho si no hay artwork, solo la mitad izquierda si hay
        float textRight = mealArt != null ? 0.56f : 0.94f;

        Lbl(overlay.transform, "— La Guía —",
            16, FontStyles.Italic, new Color(0.85f, 0.75f, 0.55f),
            0f, 0.84f, textRight, 0.92f).alignment = TextAlignmentOptions.Center;

        Lbl(overlay.transform,
            "\"Espera, Mike. Antes de que entres…\n" +
            "la Hiena trabaja para la Élite. Es la más peligrosa que has enfrentado.\n" +
            "Toma, come algo. Necesitarás fuerzas.\"",
            15, FontStyles.Normal, new Color(0.95f, 0.90f, 0.80f),
            0.06f, 0.66f, textRight, 0.85f).alignment = TextAlignmentOptions.Center;

        // Si hay artwork a la derecha, los botones se estrechan a la mitad izquierda
        float btnLeft  = 0.04f;
        float btnRight = mealArt != null ? 0.54f : 0.92f;

        Lbl(overlay.transform, "── Elige qué comer ──",
            13, FontStyles.Normal, new Color(0.65f, 0.65f, 0.55f),
            0f, 0.60f, textRight, 0.67f).alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI commentTMP = Lbl(overlay.transform, "",
            13, FontStyles.Italic, new Color(0.8f, 0.75f, 0.6f),
            btnLeft, 0.10f, btnRight, 0.22f);
        commentTMP.alignment = TextAlignmentOptions.Center;

        float[] yS = { 0.50f, 0.34f, 0.18f };
        float[] yE = { 0.62f, 0.48f, 0.32f };

        for (int i = 0; i < Meals.Length; i++)
        {
            MealOption meal = Meals[i];

            GameObject btnGO = new GameObject($"MealBtn_{i}");
            btnGO.transform.SetParent(overlay.transform, false);
            RectTransform rt = btnGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(btnLeft,  yS[i]);
            rt.anchorMax = new Vector2(btnRight, yE[i]);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            Image bg  = btnGO.AddComponent<Image>();
            bg.color  = new Color(0.18f, 0.12f, 0.06f, 0.95f);
            Button btn = btnGO.AddComponent<Button>();

            Lbl(btnGO.transform, $"<b>{meal.name}</b>", 16, FontStyles.Normal,
                new Color(1f, 0.88f, 0.6f), 0f, 0.50f, 1f, 1f, 12, 2, -12, -2);
            Lbl(btnGO.transform, meal.description.Split('\n')[0], 12, FontStyles.Normal,
                new Color(0.7f, 0.9f, 0.7f), 0f, 0f, 1f, 0.52f, 12, 2, -12, -2);

            var trig = btnGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            string cmt = meal.guideComment;
            AddTrigger(trig, UnityEngine.EventSystems.EventTriggerType.PointerEnter,
                _ => { commentTMP.text = cmt; bg.color = new Color(0.28f, 0.18f, 0.08f); });
            AddTrigger(trig, UnityEngine.EventSystems.EventTriggerType.PointerExit,
                _ => { commentTMP.text = ""; bg.color = new Color(0.18f, 0.12f, 0.06f); });

            btn.onClick.AddListener(() => {
                LevelUpData.MealHungerRestore = meal.hungerRestore;
                LevelUpData.MealHPRestore     = meal.hpRestore;
                LevelUpData.MealEnergyRestore = meal.energyRestore;
                Destroy(overlay);   // limpiar overlay antes de la transición
                StartBossFight();
            });
        }
    }

    // ── Pantalla: DERROTA en combate normal (no boss) ─────────────────────────

    private IEnumerator ShowRegularDefeatScreen()
    {
        yield return new WaitForSeconds(1.0f);

        bool   inScene   = InSceneCombatController.Instance != null;
        string sceneName = startSceneName;

        // ── Pantalla pre-construida ────────────────────────────────────────────
        if (defeatScreenRefs.root != null)
        {
            defeatScreenRefs.root.SetActive(true);
            yield return StartCoroutine(FadeIn(defeatScreenRefs.root, 0.4f));

            // Artwork
            if (defeatScreenRefs.artworkSlot != null)
            {
                Sprite art = GetSprite(defeatArtworkImage);
                if (defeatScreenRefs.artworkSlot.sprite == null && art != null)
                    defeatScreenRefs.artworkSlot.sprite = art;
                defeatScreenRefs.artworkSlot.enabled = defeatScreenRefs.artworkSlot.sprite != null;
            }

            // Botón Reintentar
            if (defeatScreenRefs.retryButton != null)
            {
                defeatScreenRefs.retryButton.onClick.RemoveAllListeners();
                defeatScreenRefs.retryButton.onClick.AddListener(() => {
                    defeatScreenRefs.root.SetActive(false);
                    if (inScene) InSceneCombatController.Instance.RetryCombat();
                    else         SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                });
            }

            // Botón Ir al inicio
            if (defeatScreenRefs.homeButton != null)
            {
                defeatScreenRefs.homeButton.onClick.RemoveAllListeners();
                defeatScreenRefs.homeButton.onClick.AddListener(() => {
                    defeatScreenRefs.root.SetActive(false);
                    if (inScene) InSceneCombatController.Instance.ReturnToExploration(sceneName);
                    else         SceneManager.LoadScene(sceneName);
                });
            }
            yield break;
        }

        // ── Fallback dinámico ──────────────────────────────────────────────────
        Canvas canvas = GetOverlayCanvas();
        if (canvas == null) yield break;

        GameObject overlay = BuildOverlay(canvas.transform, new Color(0.07f, 0f, 0f, 0.95f));
        yield return StartCoroutine(FadeIn(overlay, 0.4f));

        Lbl(overlay.transform, "Caíste en combate.",
            32, FontStyles.Bold, new Color(1f, 0.3f, 0.2f),
            0f, 0.68f, 1f, 0.82f).alignment = TextAlignmentOptions.Center;

        // Artwork o texto de consejo (se excluyen mutuamente)
        Sprite defeatArt = GetSprite(defeatArtworkImage);
        if (defeatArt != null)
            Img(overlay.transform, defeatArt,
                new Vector2(0.12f, 0.37f), new Vector2(0.88f, 0.68f));
        else
            Lbl(overlay.transform,
                "No te preocupes — todavía estás aprendiendo.\n" +
                "Prueba a defender cuando el enemigo se prepare\n" +
                "y usa el Instinto para exponer su punto débil.",
                15, FontStyles.Normal, new Color(0.88f, 0.82f, 0.78f),
                0.08f, 0.42f, 0.92f, 0.68f).alignment = TextAlignmentOptions.Center;

        // Botón: Reintentar
        Btn(overlay.transform, "Reintentar combate",
            new Color(0.15f, 0.35f, 0.55f),
            0.08f, 0.22f, 0.92f, 0.37f,
            () => {
                Destroy(overlay);
                if (inScene) InSceneCombatController.Instance.RetryCombat();
                else         SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });

        // Botón: Ir al inicio
        Btn(overlay.transform, "Ir al inicio",
            new Color(0.22f, 0.22f, 0.22f),
            0.25f, 0.05f, 0.75f, 0.18f,
            () => {
                Destroy(overlay);
                if (inScene) InSceneCombatController.Instance.ReturnToExploration(sceneName);
                else         SceneManager.LoadScene(sceneName);
            });
    }

    // ── Pantalla: FIN DEL BOSS (ganar o perder) ────────────────────────────────

    private IEnumerator ShowBossEndScreen(bool won)
    {
        yield return new WaitForSeconds(1.5f);

        var    refs      = won ? bossWinScreenRefs : bossLoseScreenRefs;
        bool   inScene   = InSceneCombatController.Instance != null;
        string sceneName = startSceneName;

        // ── Pantalla pre-construida ────────────────────────────────────────────
        if (refs.root != null)
        {
            refs.root.SetActive(true);
            yield return StartCoroutine(FadeIn(refs.root, 0.5f));

            // Artwork
            if (refs.artworkSlot != null)
            {
                Sprite endArt = won ? GetSprite(bossWinArtworkImage) : GetSprite(bossLoseArtworkImage);
                if (refs.artworkSlot.sprite == null && endArt != null)
                    refs.artworkSlot.sprite = endArt;
                refs.artworkSlot.enabled = refs.artworkSlot.sprite != null;
            }

            // Título
            if (refs.titleLabel != null)
            {
                refs.titleLabel.text  = won ? "Ganaste." : "Perdiste.";
                refs.titleLabel.color = won ? new Color(0.85f, 1f, 0.55f) : new Color(1f, 0.35f, 0.25f);
            }

            // Texto narrativo — solo si no hay artwork que lo tape
            if (refs.storyLabel != null)
            {
                bool hasArt = refs.artworkSlot != null && refs.artworkSlot.enabled;
                refs.storyLabel.enabled = !hasArt;
                refs.storyLabel.text = won
                    ? "Fue de suerte, y lo sabes.\n\n" +
                      "La Hiena no era ni la mitad de lo que te espera.\n" +
                      "Hay criaturas en las sombras que harán temblar todo\n" +
                      "lo que crees saber sobre el Kime.\n\n" +
                      "El verdadero camino apenas comienza.\nPrepárate, Mike."
                    : "Es normal que pierdas.\n\n" +
                      "No has practicado nada todavía.\n" +
                      "El mundo de KIMERA está lleno de criaturas mucho más\n" +
                      "poderosas que esta Hiena.\n\n" +
                      "Entrenarás. Aprenderás. Y cuando vuelvas,\nserá diferente.";
            }

            // "CONTINUARÁ" — aparece con fade lento
            yield return new WaitForSeconds(0.6f);
            if (refs.continuedLabel != null)
            {
                refs.continuedLabel.gameObject.SetActive(true);
                refs.continuedLabel.text  = "— C O N T I N U A R Á —";
                refs.continuedLabel.color = new Color(1f, 0.88f, 0.4f, 0f);
                yield return StartCoroutine(FadeText(refs.continuedLabel, 0f, 1f, 1.4f));
                StartCoroutine(PulseColor(refs.continuedLabel,
                    new Color(1f, 0.88f, 0.4f), new Color(1f, 0.55f, 0.1f)));
            }

            yield return new WaitForSeconds(0.5f);

            // Botón Volver al inicio
            if (refs.homeButton != null)
            {
                refs.homeButton.onClick.RemoveAllListeners();
                refs.homeButton.onClick.AddListener(() => {
                    StopAllCoroutines();
                    refs.root.SetActive(false);
                    if (inScene) InSceneCombatController.Instance.ReturnToExploration(sceneName);
                    else         SceneManager.LoadScene(sceneName);
                });
            }

            // Botón Salir
            if (refs.quitButton != null)
            {
                refs.quitButton.onClick.RemoveAllListeners();
                refs.quitButton.onClick.AddListener(() => {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                });
            }
            yield break;
        }

        // ── Fallback dinámico ──────────────────────────────────────────────────
        Canvas canvas = GetOverlayCanvas();
        if (canvas == null) yield break;

        Color bgColor = won
            ? new Color(0.01f, 0.04f, 0.01f, 0.97f)
            : new Color(0.06f, 0f,    0f,    0.97f);

        GameObject overlay = BuildOverlay(canvas.transform, bgColor);
        yield return StartCoroutine(FadeIn(overlay, 0.5f));

        // ── Título ─────────────────────────────────────────────────────────────
        string titleStr = won ? "Ganaste." : "Perdiste.";
        Color  titleCol = won ? new Color(0.85f, 1f, 0.55f) : new Color(1f, 0.35f, 0.25f);

        var titleTmp = Lbl(overlay.transform, titleStr,
            46, FontStyles.Bold, titleCol, 0f, 0.72f, 1f, 0.88f);
        titleTmp.alignment = TextAlignmentOptions.Center;

        // ── Artwork o texto narrativo (se excluyen mutuamente) ────────────────
        Sprite endArtwork = won ? GetSprite(bossWinArtworkImage) : GetSprite(bossLoseArtworkImage);

        if (endArtwork != null)
        {
            Img(overlay.transform, endArtwork,
                new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.72f));
        }
        else
        {
            string story = won
                ? "Fue de suerte, y lo sabes.\n\n" +
                  "La Hiena no era ni la mitad de lo que te espera.\n" +
                  "Hay criaturas en las sombras que harán temblar todo\n" +
                  "lo que crees saber sobre el Kime.\n\n" +
                  "El verdadero camino apenas comienza.\nPrepárate, Mike."
                : "Es normal que pierdas.\n\n" +
                  "No has practicado nada todavía.\n" +
                  "El mundo de KIMERA está lleno de criaturas mucho más\n" +
                  "poderosas que esta Hiena.\n\n" +
                  "Entrenarás. Aprenderás. Y cuando vuelvas,\nserá diferente.";

            Lbl(overlay.transform, story,
                15, FontStyles.Normal, new Color(0.88f, 0.85f, 0.82f),
                0.06f, 0.30f, 0.94f, 0.72f).alignment = TextAlignmentOptions.Center;
        }

        // ── "CONTINUARÁ" — aparece con fade lento ─────────────────────────────
        yield return new WaitForSeconds(0.6f);

        var contTmp = Lbl(overlay.transform, "— C O N T I N U A R Á —",
            22, FontStyles.Bold, new Color(1f, 0.88f, 0.4f),
            0f, 0.19f, 1f, 0.30f);
        contTmp.alignment = TextAlignmentOptions.Center;
        contTmp.color     = new Color(1f, 0.88f, 0.4f, 0f);

        yield return StartCoroutine(FadeText(contTmp, 0f, 1f, 1.4f));
        StartCoroutine(PulseColor(contTmp,
            new Color(1f, 0.88f, 0.4f), new Color(1f, 0.55f, 0.1f)));

        // ── Botones ────────────────────────────────────────────────────────────
        yield return new WaitForSeconds(0.5f);

        // Volver al inicio
        Btn(overlay.transform, "Volver al inicio",
            new Color(0.15f, 0.30f, 0.15f),
            0.08f, 0.04f, 0.46f, 0.16f,
            () => {
                Destroy(overlay);
                if (inScene) InSceneCombatController.Instance.ReturnToExploration(sceneName);
                else         SceneManager.LoadScene(sceneName);
            });

        // Salir del juego
        Btn(overlay.transform, "Salir",
            new Color(0.30f, 0.10f, 0.10f),
            0.54f, 0.04f, 0.92f, 0.16f,
            () => {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            });
    }

    // ── Boss fight setup ───────────────────────────────────────────────────────

    private void StartBossFight()
    {
        EnemyData boss           = ScriptableObject.CreateInstance<EnemyData>();
        boss.enemyName           = "Hiena de la Élite — JEFA";
        boss.maxHealth           = 100;
        boss.attackPower         = 22;
        boss.defense             = 8;
        boss.speed               = 14;
        boss.enemyType           = EnemyType.MiniBoss;
        boss.weakness            = WeaknessType.Pressure;
        boss.analysisDescription =
            "La Hiena lleva implantes de la Élite. Su musculatura aumentada la hace brutal, " +
            "pero sus sensores son vulnerables a ataques de presión sostenida.";
        boss.weaknessHint        = "Aplica Presión: el siguiente ataque hace +30 % de daño.";
        boss.skills = new EnemySkill[]
        {
            new EnemySkill { skillName = "Ataque Agresivo",   damage = 22 },
            new EnemySkill { skillName = "Mordida Frenética", damage = 28 },
        };

        // ── Sprite y animación del boss ───────────────────────────────────────
        // Prioridad:
        //   1. bossEnemyData (Enemy_Hiena.asset) — fuente canónica, la más robusta.
        //   2. bossCharacterImage (Image en Canvas, creado por Kimera/13).
        //   3. bossSprite (Sprite directo en Inspector, modo legacy).
        if (bossEnemyData != null)
        {
            // Copiar sprite e idle animation frames del asset de la Hiena.
            if (bossEnemyData.sprite != null)
                boss.sprite = bossEnemyData.sprite;
            if (bossEnemyData.idleAnimationFrames != null &&
                bossEnemyData.idleAnimationFrames.Length >= 2)
            {
                boss.idleAnimationFrames = bossEnemyData.idleAnimationFrames;
                boss.animationFps        = bossEnemyData.animationFps;
            }
        }

        // Overrides explícitos (tienen prioridad sobre bossEnemyData para el sprite)
        Sprite bs = GetSprite(bossCharacterImage);
        if (bs == null) bs = bossSprite;
        if (bs != null) boss.sprite = bs;

        if (boss.sprite == null)
            Debug.LogWarning("[PostCombatFlow] Boss sin sprite — arrastra 'Enemy_Hiena' al campo " +
                             "'Boss Enemy Data' en el componente PostCombatFlow.");

        LevelUpData.PendingLevelUp = true;
        LevelUpData.IsBossFight    = true;

        if (InSceneCombatController.Instance != null)
        {
            // Modo en escena: iniciar el boss directamente sin recargar la escena
            InSceneCombatController.Instance.StartBossFight(boss);
        }
        else
        {
            // Modo con recarga de escena (legacy)
            CombatDataTransfer.EnemiesToLoad = new EnemyData[] { boss };
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    // ── Artwork helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Extrae el Sprite de un Image de forma segura frente al fake-null de Unity.
    /// Devuelve null si el Image no existe o no tiene sprite asignado.
    /// </summary>
    private static Sprite GetSprite(Image img)
    {
        if (img == null) return null;
        return img.sprite;
    }

    // ── Coroutines ─────────────────────────────────────────────────────────────

    private static IEnumerator FadeIn(GameObject go, float duration)
    {
        if (go == null) yield break;
        // ?? no usa el null-check de Unity — usar if explícito para evitar fake-null
        CanvasGroup cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        if (cg == null) yield break;
        cg.alpha = 0f;
        float t = 0f;
        while (t < duration) { t += Time.deltaTime; cg.alpha = Mathf.Clamp01(t / duration); yield return null; }
        cg.alpha = 1f;
    }

    private static IEnumerator FadeText(TextMeshProUGUI tmp, float from, float to, float duration)
    {
        float t = 0f;
        Color c = tmp.color;
        while (t < duration)
        {
            t += Time.deltaTime;
            c.a    = Mathf.Lerp(from, to, t / duration);
            tmp.color = c;
            yield return null;
        }
        c.a = to; tmp.color = c;
    }

    private IEnumerator PulseColor(TextMeshProUGUI tmp, Color a, Color b)
    {
        while (tmp != null)
        {
            tmp.color = Color.Lerp(a, b, Mathf.PingPong(Time.time * 1.2f, 1f));
            yield return null;
        }
    }

    // ── Canvas helper ──────────────────────────────────────────────────────────

    /// <summary>
    /// Devuelve el Canvas donde deben colocarse los overlays post-combate.
    /// Prioridad: campo asignado en Inspector → Canvas que contiene CombatUI →
    /// primer Canvas de la escena (fallback).
    /// </summary>
    private Canvas GetOverlayCanvas()
    {
        if (postCombatCanvas != null) return postCombatCanvas;

        // Buscar el canvas que tiene CombatUI (el canvas de combate)
        CombatUI cui = FindFirstObjectByType<CombatUI>();
        if (cui != null)
        {
            Canvas c = cui.GetComponentInParent<Canvas>();
            if (c != null) return c;
        }

        // Último recurso
        return FindFirstObjectByType<Canvas>();
    }

    // ── UI helpers ─────────────────────────────────────────────────────────────

    private static GameObject BuildOverlay(Transform parent, Color color)
    {
        GameObject go = new GameObject("PostCombatOverlay");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
        go.transform.SetAsLastSibling();
        return go;
    }

    private static void ColorBlock(Transform parent, string text, Color bg, Color fg,
        Vector2 aMin, Vector2 aMax)
    {
        GameObject go = new GameObject("Block");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = bg;
        Lbl(go.transform, text, 20, FontStyles.Bold, fg, 0f,0f,1f,1f,8,8,-8,-8)
            .alignment = TextAlignmentOptions.Center;
    }

    private static void Img(Transform parent, Sprite sprite, Vector2 aMin, Vector2 aMax)
    {
        GameObject go = new GameObject("Art");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = sprite; img.preserveAspect = true;
    }

    private static void Btn(Transform parent, string text, Color color,
        float x0, float y0, float x1, float y1,
        UnityEngine.Events.UnityAction action)
    {
        GameObject go = new GameObject("Btn");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0,y0); rt.anchorMax = new Vector2(x1,y1);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = color;
        go.AddComponent<Button>().onClick.AddListener(action);
        Lbl(go.transform, text, 17, FontStyles.Bold, Color.white, 0f,0f,1f,1f,8,4,-8,-4)
            .alignment = TextAlignmentOptions.Center;
    }

    // Lbl con anchorMin/Max como floats (conveniente para inline)
    private static TextMeshProUGUI Lbl(Transform parent, string text,
        int size, FontStyles style, Color color,
        float x0, float y0, float x1, float y1,
        float ox0=0, float oy0=0, float ox1=0, float oy1=0)
    {
        GameObject go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(x0,y0); rt.anchorMax = new Vector2(x1,y1);
        rt.offsetMin = new Vector2(ox0,oy0); rt.offsetMax = new Vector2(ox1,oy1);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style;
        tmp.color = color; tmp.enableWordWrapping = true;
        return tmp;
    }

    private static void AddTrigger(UnityEngine.EventSystems.EventTrigger et,
        UnityEngine.EventSystems.EventTriggerType type,
        UnityEngine.Events.UnityAction<UnityEngine.EventSystems.BaseEventData> cb)
    {
        var entry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(cb);
        et.triggers.Add(entry);
    }
}
