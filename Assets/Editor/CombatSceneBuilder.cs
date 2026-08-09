// ─────────────────────────────────────────────────────────────────────────────
// CombatSceneBuilder.cs  —  Assets/Editor/
// Menú Unity:  Kimera ▶ Construir CombatScene   (Ctrl+Shift+B)
//
// Qué hace:
//   • Crea Assets/Scenes/CombatScene.unity
//   • Crea todos los ScriptableObjects (Stats, Mutation, 3 enemigos, 5 ítems)
//   • Construye la jerarquía Canvas completa con layout sketch
//   • Crea prefabs ItemButton y DamageNumber
//   • Conecta todos los campos serializados automáticamente
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

public static class CombatSceneBuilder
{
    const string SCENES = "Assets/Scenes";
    const string DATA   = "Assets/ScriptableObjects/Data";
    const string PREF   = "Assets/Prefabs";

    // ── Entry point ────────────────────────────────────────────────────────────

    [MenuItem("Kimera/Construir CombatScene %#b")]
    public static void Build()
    {
        EnsureFolder(SCENES);
        EnsureFolder("Assets/ScriptableObjects");
        EnsureFolder(DATA);
        EnsureFolder(PREF);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── ScriptableObjects ──────────────────────────────────────────────────
        var (mikeSO, kimeSO)              = BuildCharacterSOs();
        var (conejoSO, jabaliSO, hienaSO) = BuildEnemySOs();
        var itemSOs                       = BuildItemSOs();
        var itemBtnPrefab                 = BuildItemButtonPrefab();
        var dmgNumPrefab                  = BuildDamageNumberPrefab();

        // ── Camera & lights ────────────────────────────────────────────────────
        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        var c = cam.AddComponent<Camera>();
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.07f, 0.07f, 0.11f);
        c.orthographic = true;
        c.orthographicSize = 5f;
        cam.AddComponent<AudioListener>();

        var lightGO = new GameObject("Directional Light");
        lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var l = lightGO.AddComponent<Light>();
        l.type = LightType.Directional;
        l.intensity = 1f;

        // ── HungerSystem (persiste entre escenas) ──────────────────────────────
        var hungerGO = new GameObject("HungerSystem");
        hungerGO.AddComponent<HungerSystem>();

        // ── MANAGERS — creado ANTES que Canvas para que CombatManager.Awake
        //   se ejecute antes que CombatUI.Awake (el singleton debe existir primero)
        var managersGO = new GameObject("_Managers");
        managersGO.AddComponent<CombatManager>();

        // ── Canvas ─────────────────────────────────────────────────────────────
        var canvasGO = new GameObject("Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        var rootCG = canvasGO.AddComponent<CanvasGroup>(); // para fade-in de entrada

        // Background
        var bgGO = Img(canvasGO, "Background", new Color(0.07f, 0.07f, 0.11f));
        Stretch(bgGO);

        // ── ÁREA DE PERSONAJES ─────────────────────────────────────────────────
        //   Mike a la izquierda — enemigos a la derecha
        var charArea = Child(canvasGO, "CharacterArea");
        RectAt(charArea, 0, 70, 1920, 620);

        // ·· Mike ··
        var mikeArea = Child(charArea, "MikeArea");
        RectAt(mikeArea, -580, 0, 300, 420);

        var mikePortraitGO = Img(mikeArea, "MikePortrait", new Color(0.18f, 0.52f, 0.18f));
        RectAt(mikePortraitGO, 0, 30, 220, 290);
        TMP(mikeArea, "MikeLabel", "MIKE", 16, Color.white, 0, -175, 220, 28);
        TMP(mikeArea, "MikeSubLabel", "[Ternero Mutado]", 11,
            new Color(0.6f, 0.8f, 0.6f), 0, -198, 220, 22);

        // ·· Enemigos ··
        var enemyArea = Child(charArea, "EnemiesArea");
        RectAt(enemyArea, 260, 0, 760, 420);

        // Enemy 0 — Conejo (gris claro)
        var e0GO = Child(enemyArea, "EnemyDisplay_0");
        RectAt(e0GO, -215, 30, 210, 360);
        var e0Img = e0GO.AddComponent<Image>();
        e0Img.color = new Color(0.62f, 0.62f, 0.65f);
        var disp0 = e0GO.AddComponent<EnemyActionDisplay>();
        TMP(e0GO, "StateBadge_0", "IDLE", 11, new Color(0.8f, 0.8f, 0.8f), 0, -195, 210, 24);

        // Enemy 1 — Jabalí (marrón)
        var e1GO = Child(enemyArea, "EnemyDisplay_1");
        RectAt(e1GO, 115, 30, 210, 360);
        var e1Img = e1GO.AddComponent<Image>();
        e1Img.color = new Color(0.52f, 0.33f, 0.13f);
        var disp1 = e1GO.AddComponent<EnemyActionDisplay>();
        TMP(e1GO, "StateBadge_1", "IDLE", 11, new Color(0.8f, 0.75f, 0.65f), 0, -195, 210, 24);

        // ── HUDs DE ENEMIGOS (encima de cada retrato) ──────────────────────────
        var enemyHUDsRoot = Child(canvasGO, "EnemyHUDs");
        RectAt(enemyHUDsRoot, 260, 400, 760, 80);

        GameObject eHUD0    = BuildEnemyHUD(enemyHUDsRoot, "EnemyHUD_0", -215,
                              "Conejo Mutado",   out var eHP0,
                              out var eName0TMP, out var eWeak0, out var eSel0);
        GameObject eHUD1    = BuildEnemyHUD(enemyHUDsRoot, "EnemyHUD_1",  115,
                              "Jabalí Infectado", out var eHP1,
                              out var eName1TMP, out var eWeak1, out var eSel1);

        // ── HUD DEL JUGADOR (izquierda inferior) ───────────────────────────────
        var playerHUD = Child(canvasGO, "PlayerHUD");
        RectAt(playerHUD, -530, -310, 620, 170);
        Img(playerHUD, "PlayerHUDBG", new Color(0f, 0f, 0f, 0.55f));
        Stretch(playerHUD.transform.GetChild(0).gameObject);

        var healthBarGO = BuildStatRow(playerHUD, "HP", "HealthBar",  0,   50, new Color(0.9f, 0.2f, 0.2f), out var hpTMP);
        var energyBarGO = BuildStatRow(playerHUD, "EN", "EnergyBar",  0,   10, new Color(0.3f, 0.6f, 1f),   out var _);
        var hungerBarGO = BuildStatRow(playerHUD, "HG", "HungerBar",  0,  -30, new Color(0.9f, 0.6f, 0.1f), out var _);

        // ── ACTION PANEL (centro inferior) ────────────────────────────────────
        var actionPanel = Child(canvasGO, "ActionPanel");
        RectAt(actionPanel, 200, -450, 880, 86);
        var apBg = Img(actionPanel, "ActionBG", new Color(0.08f, 0.08f, 0.12f, 0.96f));
        Stretch(apBg);

        var btnAttack   = Btn(actionPanel, "BtnAttack",   "ATACAR",   new Color(0.65f, 0.13f, 0.13f), -320, 0, 190, 66);
        var btnInstinct = Btn(actionPanel, "BtnInstinct", "INSTINTO", new Color(0.13f, 0.38f, 0.65f), -100, 0, 190, 66);
        var btnItem     = Btn(actionPanel, "BtnItem",     "ÍTEM",     new Color(0.13f, 0.52f, 0.22f),  120, 0, 190, 66);
        var btnDefend   = Btn(actionPanel, "BtnDefend",   "DEFENDER", new Color(0.38f, 0.30f, 0.52f),  340, 0, 190, 66);

        // ── ITEM PANEL (oculto) ────────────────────────────────────────────────
        var itemPanel = Child(canvasGO, "ItemPanel");
        RectAt(itemPanel, 200, -240, 520, 420);
        var ipBg = Img(itemPanel, "ItemBG", new Color(0.06f, 0.06f, 0.1f, 0.97f));
        Stretch(ipBg);
        TMP(itemPanel, "ItemTitle", "── ÍTEMS ──", 16, new Color(0.7f, 0.85f, 0.7f), 0, 180, 500, 30);

        var scrollGO = Child(itemPanel, "ScrollView");
        RectAt(scrollGO, 0, -20, 500, 350);
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        var viewport = Child(scrollGO, "Viewport");
        Stretch(viewport);
        viewport.AddComponent<RectMask2D>();
        var content = Child(viewport, "Content");
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1);
        contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition = Vector2.zero;
        contentRT.sizeDelta = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(10, 10, 6, 6);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content   = contentRT;
        scrollRect.viewport  = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;
        itemPanel.SetActive(false);

        // ── ANALYSIS PANEL (oculto) ────────────────────────────────────────────
        var analysisPanel = Child(canvasGO, "AnalysisPanel");
        RectAt(analysisPanel, 0, 0, 860, 430);
        var aBg = Img(analysisPanel, "ABG", new Color(0.04f, 0.04f, 0.08f, 0.97f));
        Stretch(aBg);
        Img(analysisPanel, "ABorder", new Color(0.3f, 0.55f, 0.8f, 0.4f));
        Stretch(analysisPanel.transform.GetChild(1).gameObject);  // border
        var analysisMain = TMP(analysisPanel, "AnalysisMainText", "[Descripción corporal]",
            16, new Color(0.9f, 0.85f, 0.72f), 0, 80, 820, 210);
        analysisMain.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.TopLeft;
        var analysisHint = TMP(analysisPanel, "AnalysisHintText", "[Pista de debilidad]",
            13, new Color(0.55f, 0.72f, 0.9f), 0, -135, 820, 100);
        TMP(analysisPanel, "AnalysisLabel", "[ MIRADA INSTINTIVA ]",
            11, new Color(0.3f, 0.55f, 0.8f), 0, 195, 820, 22);
        analysisPanel.SetActive(false);

        // ── MESSAGE BOX (oculto, banner superior) ─────────────────────────────
        var messageBox = Child(canvasGO, "MessageBox");
        RectAt(messageBox, 0, 420, 1100, 62);
        var mBg = Img(messageBox, "MBG", new Color(0f, 0f, 0f, 0.88f));
        Stretch(mBg);
        var msgText = TMP(messageBox, "MessageText", "", 18, Color.white, 0, 0, 1060, 62);
        messageBox.SetActive(false);

        // ── VICTORY / DEFEAT ──────────────────────────────────────────────────
        var victoryScreen = Child(canvasGO, "VictoryScreen");
        Stretch(victoryScreen);
        Img(victoryScreen, "VBG", new Color(0f, 0.04f, 0f, 0.92f));
        Stretch(victoryScreen.transform.GetChild(0).gameObject);
        TMP(victoryScreen, "VText", "¡VICTORIA!", 80, new Color(0.9f, 0.85f, 0.18f), 0, 60, 900, 130);
        TMP(victoryScreen, "VSubText", "Pulsa cualquier tecla", 22,
            new Color(0.7f, 0.7f, 0.7f), 0, -50, 900, 40);
        victoryScreen.SetActive(false);

        var defeatScreen = Child(canvasGO, "DefeatScreen");
        Stretch(defeatScreen);
        Img(defeatScreen, "DBG", new Color(0.06f, 0f, 0f, 0.92f));
        Stretch(defeatScreen.transform.GetChild(0).gameObject);
        TMP(defeatScreen, "DText", "HAS CAÍDO...", 80, new Color(0.9f, 0.25f, 0.25f), 0, 60, 900, 130);
        TMP(defeatScreen, "DSubText", "Pulsa cualquier tecla", 22,
            new Color(0.7f, 0.7f, 0.7f), 0, -50, 900, 40);
        defeatScreen.SetActive(false);

        // ── FADE PANEL ─────────────────────────────────────────────────────────
        var fadePanelGO = Img(canvasGO, "FadePanel", Color.black);
        Stretch(fadePanelGO);
        var fadeCG = fadePanelGO.AddComponent<CanvasGroup>();
        fadeCG.alpha = 0f;
        fadeCG.blocksRaycasts = false;

        // CombatSceneInitializer se agrega aquí (después de que Canvas existe)
        var initializer = managersGO.AddComponent<CombatSceneInitializer>();

        // ── EVENT SYSTEM ──────────────────────────────────────────────────────
        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<InputSystemUIInputModule>();

        // ── WIRING ────────────────────────────────────────────────────────────
        // CombatUI
        var combatUI = canvasGO.AddComponent<CombatUI>();
        var uiSO = new SerializedObject(combatUI);

        Set(uiSO, "playerHealthBar",  healthBarGO.GetComponent<Slider>());
        Set(uiSO, "playerEnergyBar",  energyBarGO.GetComponent<Slider>());
        Set(uiSO, "playerHealthText", hpTMP.GetComponent<TextMeshProUGUI>());
        Set(uiSO, "playerPortrait",   mikePortraitGO.GetComponent<Image>());
        Set(uiSO, "actionPanel",      actionPanel);
        Set(uiSO, "btnAttack",        btnAttack.GetComponent<Button>());
        Set(uiSO, "btnInstinct",      btnInstinct.GetComponent<Button>());
        Set(uiSO, "btnItem",          btnItem.GetComponent<Button>());
        Set(uiSO, "btnDefend",        btnDefend.GetComponent<Button>());
        Set(uiSO, "itemPanel",        itemPanel);
        Set(uiSO, "itemListContainer",contentRT);
        Set(uiSO, "itemButtonPrefab", itemBtnPrefab);
        Set(uiSO, "damageNumberPrefab", dmgNumPrefab);
        Set(uiSO, "analysisPanel",    analysisPanel);
        Set(uiSO, "analysisMainText", analysisMain.GetComponent<TextMeshProUGUI>());
        Set(uiSO, "analysisHintText", analysisHint.GetComponent<TextMeshProUGUI>());
        Set(uiSO, "messageBox",       messageBox);
        Set(uiSO, "messageText",      msgText.GetComponent<TextMeshProUGUI>());
        Set(uiSO, "victoryScreen",    victoryScreen);
        Set(uiSO, "defeatScreen",     defeatScreen);
        Set(uiSO, "mainCanvasGroup",  rootCG);   // fade-in de toda la UI

        // enemyHUDs array
        var hudsProp = uiSO.FindProperty("enemyHUDs");
        hudsProp.arraySize = 2;
        var h0 = hudsProp.GetArrayElementAtIndex(0);
        h0.FindPropertyRelative("healthBar").objectReferenceValue    = eHP0;
        h0.FindPropertyRelative("nameLabel").objectReferenceValue    = eName0TMP;
        h0.FindPropertyRelative("weaknessIndicator").objectReferenceValue = eWeak0;
        h0.FindPropertyRelative("selectionHighlight").objectReferenceValue = eSel0;
        var h1 = hudsProp.GetArrayElementAtIndex(1);
        h1.FindPropertyRelative("healthBar").objectReferenceValue    = eHP1;
        h1.FindPropertyRelative("nameLabel").objectReferenceValue    = eName1TMP;
        h1.FindPropertyRelative("weaknessIndicator").objectReferenceValue = eWeak1;
        h1.FindPropertyRelative("selectionHighlight").objectReferenceValue = eSel1;

        // enemyViews array (animators null — sketch sin Animator)
        uiSO.FindProperty("enemyViews").arraySize = 2;
        uiSO.ApplyModifiedProperties();

        // EnemyActionDisplay wiring
        WireDisplay(disp0, e0Img, "Conejo Mutado",    new Color(0.62f, 0.62f, 0.65f));
        WireDisplay(disp1, e1Img, "Jabalí Infectado",  new Color(0.52f, 0.33f, 0.13f));

        // CombatSceneInitializer
        var initSO = new SerializedObject(initializer);
        Set(initSO, "playerStats",       mikeSO);
        Set(initSO, "combatUI",          combatUI);
        Set(initSO, "hungerBarInCombat", hungerBarGO.GetComponent<Slider>());

        var startItems = initSO.FindProperty("startingItems");
        startItems.arraySize = itemSOs.Length;
        for (int i = 0; i < itemSOs.Length; i++)
            startItems.GetArrayElementAtIndex(i).objectReferenceValue = itemSOs[i];

        var fallback = initSO.FindProperty("fallbackEnemies");
        fallback.arraySize = 2;
        fallback.GetArrayElementAtIndex(0).objectReferenceValue = conejoSO;
        fallback.GetArrayElementAtIndex(1).objectReferenceValue = jabaliSO;
        initSO.ApplyModifiedProperties();

        // ── Save scene ────────────────────────────────────────────────────────
        EditorSceneManager.SaveScene(scene, $"{SCENES}/CombatScene.unity");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("✓ CombatScene creada → Assets/Scenes/CombatScene.unity");
    }

    // ── ScriptableObjects ──────────────────────────────────────────────────────

    static (CharacterStats, MutationData) BuildCharacterSOs()
    {
        var kime = Load<MutationData>($"{DATA}/Kime_Mutation.asset");
        var kSO  = new SerializedObject(kime);
        kSO.FindProperty("mutationName").stringValue      = "Mirada Instintiva";
        kSO.FindProperty("energyCost").intValue           = 10;
        kSO.FindProperty("revealsWeakness").boolValue     = true;
        kSO.FindProperty("weaknessWindowTurns").floatValue= 3f;
        var ta = kSO.FindProperty("analysisTexts");
        ta.arraySize = 3;
        ta.GetArrayElementAtIndex(0).stringValue =
            "El conejo comprime sus músculos glúteos izquierdos antes de saltar. Una apertura clara.";
        ta.GetArrayElementAtIndex(1).stringValue =
            "La columna del jabalí se arquea hacia la derecha justo antes de cargar. El costado queda expuesto.";
        ta.GetArrayElementAtIndex(2).stringValue =
            "La hiena ladea la cabeza 40 grados a la izquierda cuando evalúa retroceder. Una grieta en su armadura.";
        kSO.ApplyModifiedProperties();

        var mike = Load<CharacterStats>($"{DATA}/Mike_Stats.asset");
        var mSO  = new SerializedObject(mike);
        mSO.FindProperty("characterName").stringValue = "Mike";
        mSO.FindProperty("maxHealth").intValue        = 100;
        mSO.FindProperty("maxEnergy").intValue        = 80;
        mSO.FindProperty("attackPower").intValue      = 20;
        mSO.FindProperty("defense").intValue          = 5;
        mSO.FindProperty("speed").intValue            = 10;
        mSO.FindProperty("activeMutation").objectReferenceValue = kime;
        mSO.ApplyModifiedProperties();

        return (mike, kime);
    }

    static (EnemyData, EnemyData, EnemyData) BuildEnemySOs()
    {
        var conejo = Load<EnemyData>($"{DATA}/Enemy_Conejo.asset");
        SetEnemy(conejo, "Conejo Mutado", EnemyType.Tutorial, 40, 12, 2, 15,
            WeaknessType.HeavyAttack,
            "Sus patas traseras vibran con energía nerviosa. Esquiva antes de contraatacar.",
            "Un impacto contundente interrumpiría su danza errática.",
            new[]{ ("Ataque Rápido", 12) });

        var jabali = Load<EnemyData>($"{DATA}/Enemy_Jabali.asset");
        SetEnemy(jabali, "Jabalí Infectado", EnemyType.Standard, 80, 22, 8, 5,
            WeaknessType.Interrupt,
            "Rasca el suelo con la pezuña derecha. Sus flancos se hinchan antes de moverse.",
            "Ese momento de tensión justo antes del movimiento parece una oportunidad única.",
            new[]{ ("Pisotón", 22), ("Carga", 40) });

        var hiena = Load<EnemyData>($"{DATA}/Enemy_Hiena.asset");
        SetEnemy(hiena, "Hiena de la Élite", EnemyType.MiniBoss, 65, 18, 6, 12,
            WeaknessType.Pressure,
            "Te estudia. Cuando la ves dudar, su quijada se tensa y las pupilas se dilatan.",
            "La constancia parece agotarla de una forma que el golpe único no logra.",
            new[]{ ("Ataque Normal", 18), ("Ataque Agresivo", 29) });

        return (conejo, jabali, hiena);
    }

    static void SetEnemy(EnemyData data, string name, EnemyType type,
        int hp, int atk, int def, int spd, WeaknessType wk,
        string analysis, string hint, (string n, int d)[] skills)
    {
        var so = new SerializedObject(data);
        so.FindProperty("enemyName").stringValue          = name;
        so.FindProperty("enemyType").enumValueIndex       = (int)type;
        so.FindProperty("maxHealth").intValue             = hp;
        so.FindProperty("attackPower").intValue           = atk;
        so.FindProperty("defense").intValue               = def;
        so.FindProperty("speed").intValue                 = spd;
        so.FindProperty("weakness").enumValueIndex        = (int)wk;
        so.FindProperty("analysisDescription").stringValue= analysis;
        so.FindProperty("weaknessHint").stringValue       = hint;
        var sp = so.FindProperty("skills");
        sp.arraySize = skills.Length;
        for (int i = 0; i < skills.Length; i++)
        {
            sp.GetArrayElementAtIndex(i).FindPropertyRelative("skillName").stringValue = skills[i].n;
            sp.GetArrayElementAtIndex(i).FindPropertyRelative("damage").intValue       = skills[i].d;
        }
        so.ApplyModifiedProperties();
    }

    static ItemData[] BuildItemSOs()
    {
        var defs = new[]
        {
            ("Vendaje Orgánico",     "Item_Vendaje",     ItemEffect.HealHealth,          30, 3, "Restaura 30 puntos de vida."),
            ("Inyección Adrenal",    "Item_Adrenal",     ItemEffect.ExtraAction,          0, 2, "Otorga una acción extra este turno."),
            ("Carne Procesada",      "Item_Carne",       ItemEffect.RestoreEnergy,        25, 3, "Recupera 25 puntos de energía."),
            ("Feromona Sintética",   "Item_Feromona",    ItemEffect.ReduceEnemyAccuracy,  30, 2, "Reduce la precisión enemiga un 30%."),
            ("Estimulante Mutágeno", "Item_Estimulante", ItemEffect.BoostDamage,          20, 2, "+20 daño durante 2 turnos."),
        };
        var result = new ItemData[defs.Length];
        for (int i = 0; i < defs.Length; i++)
        {
            var item = Load<ItemData>($"{DATA}/{defs[i].Item2}.asset");
            var so   = new SerializedObject(item);
            so.FindProperty("itemName").stringValue    = defs[i].Item1;
            so.FindProperty("effect").enumValueIndex   = (int)defs[i].Item3;
            so.FindProperty("value").intValue          = defs[i].Item4;
            so.FindProperty("quantity").intValue       = defs[i].Item5;
            so.FindProperty("description").stringValue = defs[i].Item6;
            so.ApplyModifiedProperties();
            result[i] = item;
        }
        return result;
    }

    // ── Prefabs ────────────────────────────────────────────────────────────────

    static GameObject BuildItemButtonPrefab()
    {
        string path = $"{PREF}/ItemButton.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var go = new GameObject("ItemButton");
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(480, 52);
        go.AddComponent<Image>().color = new Color(0.13f, 0.18f, 0.23f);
        go.AddComponent<Button>();

        var lbl = Child(go, "Label");
        Stretch(lbl);
        var t = lbl.AddComponent<TextMeshProUGUI>();
        t.fontSize  = 15;
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.margin    = new Vector4(14, 0, 14, 0);
        t.color     = Color.white;

        bool ok;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out ok);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject BuildDamageNumberPrefab()
    {
        string path = $"{PREF}/DamageNumber.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var go  = new GameObject("DamageNumber");
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.fontSize  = 7f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.text      = "-0";
        tmp.color     = Color.red;
        go.AddComponent<DamageNumberFloat>();

        bool ok;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path, out ok);
        Object.DestroyImmediate(go);
        return prefab;
    }

    // ── UI helpers ─────────────────────────────────────────────────────────────

    static GameObject BuildEnemyHUD(GameObject parent, string name, float x,
        string enemyName,
        out Slider      healthBar,
        out TextMeshProUGUI nameLabel,
        out GameObject  weakIndicator,
        out GameObject  selIndicator)
    {
        var hud = Child(parent, name);
        RectAt(hud, x, 0, 215, 74);
        var bgHud = Img(hud, "BG", new Color(0f, 0f, 0f, 0.6f)); Stretch(bgHud);

        var nameLblGO = TMP(hud, "NameLabel", enemyName, 12, Color.white, 0, 22, 200, 22);
        nameLabel = nameLblGO.GetComponent<TextMeshProUGUI>();

        var hpGO = Child(hud, "HealthBar");
        RectAt(hpGO, -5, -6, 190, 18);
        var hpBg = hpGO.AddComponent<RectTransform>(); // already added by Child
        var hpBgImg = Child(hpGO, "BG");
        Stretch(hpBgImg); hpBgImg.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f);
        var hpFill = Child(hpGO, "Fill"); Stretch(hpFill);
        hpFill.AddComponent<Image>().color = new Color(0.88f, 0.2f, 0.2f);
        var sl = hpGO.AddComponent<Slider>();
        sl.fillRect    = hpFill.GetComponent<RectTransform>();
        sl.direction   = Slider.Direction.LeftToRight;
        sl.minValue    = 0; sl.maxValue = 100; sl.value = 100;
        sl.interactable = false;
        healthBar = sl;

        var wk = Img(hud, "WeaknessIndicator", new Color(1f, 0.9f, 0.1f));
        RectAt(wk, 92, 22, 22, 22);
        TMP(wk, "!", "!", 13, Color.black, 0, 0, 22, 22);
        wk.SetActive(false);
        weakIndicator = wk;

        var sel = Img(hud, "SelectionHighlight", new Color(1f, 1f, 0f, 0.25f));
        RectAt(sel, 0, 0, 215, 74);
        sel.SetActive(false);
        selIndicator = sel;

        return hud;
    }

    // Fila HP/EN/HG — devuelve el GO del slider y (opcionalmente) el GO del texto
    static GameObject BuildStatRow(GameObject parent, string label, string sliderName,
        float x, float y, Color fillColor, out GameObject textGO)
    {
        TMP(parent, label + "Lbl", label, 15, Color.white, x - 245, y, 42, 28);
        var sliderGO = Child(parent, sliderName);
        RectAt(sliderGO, x + 20, y, 420, 26);

        var bg = Child(sliderGO, "BG"); Stretch(bg);
        bg.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.18f);
        var fill = Child(sliderGO, "Fill"); Stretch(fill);
        fill.AddComponent<Image>().color = fillColor;

        var sl = sliderGO.AddComponent<Slider>();
        sl.fillRect = fill.GetComponent<RectTransform>();
        sl.direction = Slider.Direction.LeftToRight;
        sl.minValue = 0; sl.maxValue = 100; sl.value = 100;
        sl.interactable = false;

        textGO = TMP(parent, label + "Text", "100 / 100", 13, Color.white, x + 280, y, 110, 26);
        return sliderGO;
    }

    static void WireDisplay(EnemyActionDisplay disp, Image img, string enemyName, Color idle)
    {
        var so = new SerializedObject(disp);
        so.FindProperty("displayImage").objectReferenceValue = img;
        so.FindProperty("trackedEnemyName").stringValue      = enemyName;
        so.FindProperty("idleColor").colorValue   = idle;
        so.FindProperty("attackColor").colorValue = new Color(1f,   0.28f, 0.28f);
        so.FindProperty("dodgeColor").colorValue  = new Color(0.28f, 0.9f, 0.9f);
        so.FindProperty("defendColor").colorValue = new Color(0.28f, 0.5f,  1f);
        so.FindProperty("chargeColor").colorValue = new Color(1f,   0.78f, 0.18f);
        so.ApplyModifiedProperties();
    }

    // ── Generic helpers ────────────────────────────────────────────────────────

    static GameObject Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    static GameObject Img(GameObject parent, string name, Color color)
    {
        var go = Child(parent, name);
        go.AddComponent<Image>().color = color;
        return go;
    }

    static GameObject TMP(GameObject parent, string name, string text,
        int size, Color color, float x, float y, float w, float h)
    {
        var go  = Child(parent, name);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        RectAt(go, x, y, w, h);
        return go;
    }

    static GameObject Btn(GameObject parent, string name, string label,
        Color bg, float x, float y, float w, float h)
    {
        var go  = Child(parent, name);
        RectAt(go, x, y, w, h);
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = bg;
        cb.highlightedColor = bg + new Color(0.18f, 0.18f, 0.18f);
        cb.pressedColor     = bg - new Color(0.18f, 0.18f, 0.18f);
        btn.colors = cb;
        var lgo = Child(go, "Label"); Stretch(lgo);
        var t   = lgo.AddComponent<TextMeshProUGUI>();
        t.text      = label;
        t.fontSize  = 15;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color     = Color.white;
        return go;
    }

    static void RectAt(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void Set(SerializedObject so, string field, Object val)
    {
        so.FindProperty(field).objectReferenceValue = val;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static T Load<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string child  = path.Substring(slash + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, child);
    }
}
