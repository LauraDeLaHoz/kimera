using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public static class KimeraFixes
{
    // ── Fix Input System ───────────────────────────────────────────────────────
    [MenuItem("Kimera/1 - Fix Input System (ejecutar si hay error de clicks)")]
    public static void FixInputSystem()
    {
        var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (es == null) { Debug.LogError("No hay EventSystem en la escena abierta."); return; }

        var old = es.GetComponent<StandaloneInputModule>();
        if (old != null) { UnityEngine.Object.DestroyImmediate(old); Debug.Log("Eliminado StandaloneInputModule"); }

        Type newType = null;
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            newType = asm.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            if (newType != null) break;
        }

        if (newType == null)
        {
            Debug.LogError("InputSystemUIInputModule no encontrado. ¿Está instalado el paquete Input System?");
            return;
        }

        if (es.GetComponent(newType) == null)
        {
            es.gameObject.AddComponent(newType);
            Debug.Log("Agregado InputSystemUIInputModule ✓");
        }
        else
        {
            Debug.Log("InputSystemUIInputModule ya estaba presente.");
        }

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("✓ Input System corregido. Escena guardada.");
    }

    // ── Diagnóstico ────────────────────────────────────────────────────────────
    [MenuItem("Kimera/2 - Diagnóstico de escena")]
    public static void Diagnostics()
    {
        Debug.Log("── DIAGNÓSTICO KIMERA ──────────────────────────────");

        var cm = UnityEngine.Object.FindFirstObjectByType<CombatManager>();
        Debug.Log(cm != null ? "✓ CombatManager encontrado" : "✗ CombatManager NO encontrado");

        var ci = UnityEngine.Object.FindFirstObjectByType<CombatSceneInitializer>();
        Debug.Log(ci != null ? "✓ CombatSceneInitializer encontrado" : "✗ CombatSceneInitializer NO encontrado");

        var ui = UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        Debug.Log(ui != null ? "✓ CombatUI encontrado" : "✗ CombatUI NO encontrado");

        var hs = UnityEngine.Object.FindFirstObjectByType<HungerSystem>();
        Debug.Log(hs != null ? "✓ HungerSystem encontrado" : "✗ HungerSystem NO encontrado");

        var fb = UnityEngine.Object.FindFirstObjectByType<BattleFeedbackSystem>();
        Debug.Log(fb != null ? "✓ BattleFeedbackSystem encontrado" : "✗ BattleFeedbackSystem NO encontrado (ejecuta Fix 3)");

        var es = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
        if (es != null)
        {
            bool hasOld = es.GetComponent<StandaloneInputModule>() != null;
            bool hasNew = false;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                if (asm.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule") is Type t &&
                    es.GetComponent(t) != null) { hasNew = true; break; }

            Debug.Log(hasOld ? "✗ StandaloneInputModule presente (MAL — ejecuta Fix 1)" : "✓ Sin StandaloneInputModule");
            Debug.Log(hasNew ? "✓ InputSystemUIInputModule presente" : "✗ InputSystemUIInputModule NO encontrado");
        }
        else
        {
            Debug.LogError("✗ No hay EventSystem en la escena");
        }

        Debug.Log("────────────────────────────────────────────────────");
    }

    // ── Add Feedback UI ────────────────────────────────────────────────────────
    // Ejecutar UNA VEZ sobre la CombatScene existente para agregar:
    // - Indicador de turno
    // - Log de batalla
    // - Panel tutorial
    // - BattleFeedbackSystem (MonoBehaviour)
    // - EnemyClickHandler en los retratos de enemigos
    [MenuItem("Kimera/3 - Add Feedback UI (patch escena existente)")]
    public static void AddFeedbackUI()
    {
        // Buscar Canvas principal
        var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (canvas == null) { Debug.LogError("No hay Canvas en la escena. Abre CombatScene primero."); return; }

        GameObject canvasGO = canvas.gameObject;

        // ── Crear BattleFeedback container ─────────────────────────────────────
        GameObject fbGO = new GameObject("BattleFeedback");
        fbGO.transform.SetParent(canvasGO.transform, false);
        RectTransform fbRect = fbGO.AddComponent<RectTransform>();
        fbRect.anchorMin = Vector2.zero;
        fbRect.anchorMax = Vector2.one;
        fbRect.offsetMin = Vector2.zero;
        fbRect.offsetMax = Vector2.zero;

        BattleFeedbackSystem fbSys = fbGO.AddComponent<BattleFeedbackSystem>();

        // ── Indicador de turno ─────────────────────────────────────────────────
        GameObject turnGO = MakeText(fbGO, "TurnIndicator",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0, -70), new Vector2(0, -20),
            "Iniciando combate…", 22, FontStyles.Bold);
        TextMeshProUGUI turnTMP = turnGO.GetComponent<TextMeshProUGUI>();
        turnTMP.alignment = TextAlignmentOptions.Center;
        turnTMP.color     = new Color(0.3f, 1f, 0.4f);

        // ── Log de batalla ─────────────────────────────────────────────────────
        // Fondo semitransparente
        GameObject logBg = new GameObject("BattleLogBG");
        logBg.transform.SetParent(fbGO.transform, false);
        RectTransform logBgRect = logBg.AddComponent<RectTransform>();
        // Posición arriba-izquierda, debajo del TurnIndicator y a la izquierda de los EnemyHUDs
        logBgRect.anchorMin = new Vector2(0f, 0.82f);
        logBgRect.anchorMax = new Vector2(0.42f, 0.93f);
        logBgRect.offsetMin = new Vector2(10, 8);
        logBgRect.offsetMax = new Vector2(-5, -5);
        Image logBgImg = logBg.AddComponent<Image>();
        logBgImg.color = new Color(0f, 0f, 0f, 0.55f);

        GameObject logGO = MakeText(logBg, "BattleLog",
            Vector2.zero, Vector2.one,
            new Vector2(8, 8), new Vector2(-8, -8),
            "", 13, FontStyles.Normal);
        TextMeshProUGUI logTMP = logGO.GetComponent<TextMeshProUGUI>();
        logTMP.alignment      = TextAlignmentOptions.TopLeft;   // más antiguo arriba, más nuevo abajo
        logTMP.color          = Color.white;
        logTMP.overflowMode   = TextOverflowModes.Truncate;

        // ── Panel tutorial ─────────────────────────────────────────────────────
        GameObject tutGO = new GameObject("TutorialPanel");
        tutGO.transform.SetParent(fbGO.transform, false);
        RectTransform tutRect = tutGO.AddComponent<RectTransform>();
        tutRect.anchorMin = new Vector2(0.2f, 0.25f);
        tutRect.anchorMax = new Vector2(0.8f, 0.75f);
        tutRect.offsetMin = Vector2.zero;
        tutRect.offsetMax = Vector2.zero;
        Image tutImg = tutGO.AddComponent<Image>();
        tutImg.color = new Color(0.05f, 0.05f, 0.1f, 0.95f);

        string tutText =
            "<b>CÓMO JUGAR</b>\n\n" +
            "1. Haz click en el retrato de un <b>enemigo</b> para seleccionarlo como objetivo.\n\n" +
            "<b>ATACAR</b>  →  Golpe directo al enemigo seleccionado.\n" +
            "<b>INSTINTO</b>  →  Gasta energía para analizar y exponer la debilidad del enemigo.\n" +
            "   Una vez expuesta, el siguiente ataque causará más daño.\n" +
            "<b>ÍTEM</b>  →  Abre el inventario. Haz click en un ítem para usarlo.\n" +
            "<b>DEFENDER</b>  →  Reduces el daño recibido este turno y recuperas energía.\n\n" +
            "Espera cuando diga 'Turno de…' — el enemigo actúa solo.\n" +
            "Los botones se activan sólo en TU TURNO.";

        GameObject tutTxtGO = MakeText(tutGO, "TutorialText",
            Vector2.zero, Vector2.one,
            new Vector2(20, 60), new Vector2(-20, -20),
            tutText, 14, FontStyles.Normal);
        TextMeshProUGUI tutTMP = tutTxtGO.GetComponent<TextMeshProUGUI>();
        tutTMP.alignment  = TextAlignmentOptions.TopLeft;
        tutTMP.color      = Color.white;

        // Botón cerrar tutorial
        GameObject closeBtnGO = new GameObject("CloseTutorialBtn");
        closeBtnGO.transform.SetParent(tutGO.transform, false);
        RectTransform closeBtnRect = closeBtnGO.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(0f, 0f);
        closeBtnRect.anchorMax = new Vector2(1f, 0f);
        closeBtnRect.offsetMin = new Vector2(20, 8);
        closeBtnRect.offsetMax = new Vector2(-20, 48);
        Image closeBtnImg = closeBtnGO.AddComponent<Image>();
        closeBtnImg.color = new Color(0.2f, 0.6f, 0.2f);
        Button closeBtn = closeBtnGO.AddComponent<Button>();

        GameObject closeLblGO = MakeText(closeBtnGO, "Label",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            "Entendido — ¡a combatir!", 15, FontStyles.Bold);
        closeLblGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        closeLblGO.GetComponent<TextMeshProUGUI>().color     = Color.white;

        // ── Asignar referencias serializadas en BattleFeedbackSystem ──────────
        SerializedObject fbSO = new SerializedObject(fbSys);
        fbSO.FindProperty("turnIndicatorText").objectReferenceValue = turnTMP;
        fbSO.FindProperty("battleLogText").objectReferenceValue     = logTMP;
        fbSO.FindProperty("tutorialPanel").objectReferenceValue     = tutGO;
        fbSO.FindProperty("closeTutorialBtn").objectReferenceValue  = closeBtn;
        fbSO.ApplyModifiedProperties();

        // ── EnemyClickHandler en retratos de enemigos ──────────────────────────
        // Busca GOs que se llamen "Enemy0", "Enemy1", "Enemy2" o "EnemyPortrait*"
        string[] candidateNames = { "Enemy0", "Enemy1", "Enemy2",
                                    "EnemyPortrait0", "EnemyPortrait1", "EnemyPortrait2",
                                    "EnemyDisplay0",  "EnemyDisplay1",  "EnemyDisplay2" };
        int handlerCount = 0;
        foreach (string name in candidateNames)
        {
            GameObject go = GameObject.Find(name);
            if (go == null) continue;
            if (go.GetComponent<EnemyClickHandler>() != null) continue;

            // Asegurarse de que tiene Button
            Button btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();

            EnemyClickHandler handler = go.AddComponent<EnemyClickHandler>();
            SerializedObject hSO = new SerializedObject(handler);
            hSO.FindProperty("enemyIndex").intValue = handlerCount;
            hSO.ApplyModifiedProperties();
            handlerCount++;
            Debug.Log($"EnemyClickHandler agregado a '{name}' (index {handlerCount - 1})");
        }

        if (handlerCount == 0)
            Debug.LogWarning("No se encontraron GOs de enemigo por nombre estándar. " +
                             "Asigna EnemyClickHandler manualmente a los retratos de enemigos.");

        // ── ItemTooltip ────────────────────────────────────────────────────────
        if (canvasGO.GetComponentInChildren<ItemTooltip>() == null)
        {
            GameObject ttGO = new GameObject("ItemTooltip");
            ttGO.transform.SetParent(canvasGO.transform, false);
            RectTransform ttRT = ttGO.AddComponent<RectTransform>();
            ttRT.anchorMin = Vector2.zero;
            ttRT.anchorMax = Vector2.one;
            ttRT.offsetMin = ttRT.offsetMax = Vector2.zero;
            ttGO.AddComponent<ItemTooltip>();
            Debug.Log("ItemTooltip creado.");
        }

        // ── PostCombatFlow ─────────────────────────────────────────────────────
        if (UnityEngine.Object.FindFirstObjectByType<PostCombatFlow>() == null)
        {
            var managers = GameObject.Find("_Managers") ?? canvasGO;
            managers.AddComponent<PostCombatFlow>();
            Debug.Log("PostCombatFlow añadido.");
        }

        EditorSceneManager.SaveOpenScenes();
        Debug.Log("✓ Feedback UI agregado. Escena guardada. Presiona Play para probar.");
    }

    // ── Rebalance Enemies ──────────────────────────────────────────────────────
    // Reduce el ATK y HP enemigos para que el demo sea ganable.
    // Busca todos los EnemyData ScriptableObjects en el proyecto y los parchea.
    [MenuItem("Kimera/4 - Rebalance Enemies (más fácil de ganar)")]
    public static void RebalanceEnemies()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyData");
        if (guids.Length == 0)
        {
            Debug.LogWarning("No se encontraron assets EnemyData en el proyecto.");
            return;
        }

        // Tabla de valores balanceados por nombre de enemigo  (attackPower, maxHealth, defense)
        //
        // Diseño estratégico:
        //   Conejo  — rápido, golpes frecuentes, enseña a esquivar/defender
        //   Jabalí  — su carga (×1.8) hace 30+ dmg; usar Instinto para interrumpirla es clave
        //   Hiena   — se vuelve más agresiva si Mike tiene <40% HP; requiere gestionar la vida
        var balanced = new System.Collections.Generic.Dictionary<string, (int atk, int hp, int def)>
        {
            { "Conejo Mutado",     (atk: 10, hp: 40, def:  2) },
            { "Jabalí Infectado",  (atk: 14, hp: 65, def:  6) },
            { "Hiena de la Élite", (atk: 15, hp: 60, def:  5) },
        };

        int patched = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null) continue;

            if (balanced.TryGetValue(data.enemyName, out var vals))
            {
                SerializedObject so = new SerializedObject(data);
                so.FindProperty("attackPower").intValue = vals.atk;
                so.FindProperty("maxHealth").intValue   = vals.hp;
                so.FindProperty("defense").intValue     = vals.def;
                so.ApplyModifiedProperties();
                patched++;
                Debug.Log($"✓ Rebalanceado: {data.enemyName}  ATK {vals.atk}  HP {vals.hp}  DEF {vals.def}");
            }
            else
            {
                // Para cualquier otro enemigo desconocido, reduce ATK un 35 %
                SerializedObject so = new SerializedObject(data);
                int newAtk = Mathf.Max(5, Mathf.RoundToInt(data.attackPower * 0.65f));
                so.FindProperty("attackPower").intValue = newAtk;
                so.ApplyModifiedProperties();
                patched++;
                Debug.Log($"✓ Rebalanceado (genérico): {data.enemyName}  ATK {newAtk}");
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"✓ {patched} enemigos rebalanceados. Presiona Play para probar.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 5 — Setup InScene Combat in Juego
    // ══════════════════════════════════════════════════════════════════════════
    // Ejecutar UNA VEZ con la escena "Juego" abierta.
    // Crea y conecta todos los GameObjects necesarios para el combate en escena:
    //   _CombatCamera  · _ScreenFade  · _CombatCanvas (UI completa)
    //   _CombatController (CombatManager, InSceneCombatController, PostCombatFlow)
    //   _CombatTrigger_01 (zona de encuentro con Conejo + Jabalí)
    // También asigna la tag "Player" al jugador encontrado en escena.
    [MenuItem("Kimera/5 - Setup InScene Combat in Juego")]
    public static void SetupInSceneCombat()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.ToLower().Contains("juego"))
            Debug.LogWarning($"Escena activa es '{scene.name}', no 'Juego'. Continúa de todas formas…");

        if (UnityEngine.Object.FindFirstObjectByType<InSceneCombatController>() != null)
        {
            Debug.LogWarning("InSceneCombatController ya existe en la escena. Cancela.");
            return;
        }

        // ── 1: Localizar jugador y cámaras ────────────────────────────────────
        var playerMovement = UnityEngine.Object.FindFirstObjectByType<thirdPersonMovement>();
        if (playerMovement == null)
        {
            Debug.LogError("No se encontró 'thirdPersonMovement' en la escena. " +
                           "Asegúrate de que el Player prefab esté en la escena.");
            return;
        }
        GameObject playerGO = playerMovement.gameObject;

        // Añadir PlayerCombatAnimator al jugador (si no lo tiene ya)
        var playerCombatAnim = playerGO.GetComponent<PlayerCombatAnimator>()
                            ?? playerGO.AddComponent<PlayerCombatAnimator>();
        // Intentar auto-detectar el Animator y Alpha_2D_Character_In_3D_World
        var playerAnimatorComp = playerGO.GetComponentInChildren<Animator>();
        var directionScript    = playerGO.GetComponent<Alpha_2D_Character_In_3D_World>()
                              ?? playerGO.GetComponentInChildren<Alpha_2D_Character_In_3D_World>();
        {
            var pcaSO = new SerializedObject(playerCombatAnim);
            if (playerAnimatorComp != null)
                pcaSO.FindProperty("animator").objectReferenceValue = playerAnimatorComp;
            if (directionScript != null)
                pcaSO.FindProperty("directionScript").objectReferenceValue = directionScript;
            pcaSO.ApplyModifiedProperties();
        }
        Debug.Log("✓ PlayerCombatAnimator configurado en el Player");

        // Tag "Player" en la instancia de escena
        playerGO.tag = "Player";

        // Intentar actualizar el prefab también (para que persista en otras instancias)
        string playerPrefabPath = AssetDatabase.GUIDToAssetPath("47064af0d9074144fa200600ca6e281f");
        if (!string.IsNullOrEmpty(playerPrefabPath))
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(playerPrefabPath);
            if (asset != null && !asset.CompareTag("Player"))
            {
                asset.tag = "Player";
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                Debug.Log("✓ Player prefab tag actualizado a 'Player'");
            }
        }

        var camCtrl         = UnityEngine.Object.FindFirstObjectByType<CameraController>();
        Camera explorationCam = camCtrl != null ? camCtrl.GetComponent<Camera>() : null;
        if (explorationCam == null)
        {
            // Fallback: cámara con tag MainCamera
            var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var c in cams)
                if (c.CompareTag("MainCamera")) { explorationCam = c; break; }
        }
        if (explorationCam == null)
            Debug.LogWarning("No se encontró la cámara de exploración. Asígnala manualmente en InSceneCombatController.");

        // ── 2: Cargar ScriptableObjects ───────────────────────────────────────
        const string DATA = "Assets/ScriptableObjects/Data";
        var mikeSO   = AssetDatabase.LoadAssetAtPath<CharacterStats>($"{DATA}/Mike_Stats.asset");
        var conejoSO = AssetDatabase.LoadAssetAtPath<EnemyData>($"{DATA}/Enemy_Conejo.asset");
        var jabaliSO = AssetDatabase.LoadAssetAtPath<EnemyData>($"{DATA}/Enemy_Jabali.asset");
        var itemBtnPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ItemButton.prefab");
        var dmgNumPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/DamageNumber.prefab");

        if (mikeSO == null)
            Debug.LogWarning("Mike_Stats.asset no encontrado. Ejecuta 'Construir CombatScene' primero para crear los SOs.");

        string[] itemFiles = { "Item_Vendaje","Item_Adrenal","Item_Carne","Item_Feromona","Item_Estimulante" };
        var itemSOs = new List<ItemData>();
        foreach (var f in itemFiles)
        {
            var so = AssetDatabase.LoadAssetAtPath<ItemData>($"{DATA}/{f}.asset");
            if (so != null) itemSOs.Add(so);
        }

        // ── 3: HungerSystem ───────────────────────────────────────────────────
        var hungerSystem = UnityEngine.Object.FindFirstObjectByType<HungerSystem>();
        if (hungerSystem == null)
        {
            hungerSystem = new GameObject("HungerSystem").AddComponent<HungerSystem>();
            Debug.Log("✓ HungerSystem creado");
        }

        // ── 4: Cámara de combate ──────────────────────────────────────────────
        var combatCamGO  = new GameObject("_CombatCamera");
        var combatCamera = combatCamGO.AddComponent<Camera>();
        combatCamera.clearFlags      = CameraClearFlags.SolidColor;
        combatCamera.backgroundColor = new Color(0.07f, 0.07f, 0.11f);
        combatCamera.fieldOfView     = 60f;
        combatCamera.nearClipPlane   = 0.1f;
        combatCamera.farClipPlane    = 100f;
        combatCamera.enabled         = false;
        combatCamGO.transform.SetPositionAndRotation(
            playerGO.transform.position + new Vector3(0f, 4f, -8f),
            Quaternion.Euler(15f, 0f, 0f));
        Debug.Log("✓ _CombatCamera creada");

        // ── 5: Canvas de fade (encima de todo) ───────────────────────────────
        var fadeCanvasGO = new GameObject("_ScreenFade");
        var fadeCanvas   = fadeCanvasGO.AddComponent<Canvas>();
        fadeCanvas.renderMode    = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder  = 200;
        fadeCanvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        fadeCanvasGO.AddComponent<GraphicRaycaster>();

        var fadeImgGO = new GameObject("FadeImage");
        fadeImgGO.transform.SetParent(fadeCanvasGO.transform, false);
        IC_Stretch(fadeImgGO.AddComponent<RectTransform>());
        var fadeImage = fadeImgGO.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImgGO.SetActive(false);   // InSceneCombatController lo activa/desactiva
        Debug.Log("✓ _ScreenFade creada");

        // ── 6: Canvas de combate (empieza invisible) ──────────────────────────
        var combatCanvasGO = new GameObject("_CombatCanvas");
        var combatCanvas   = combatCanvasGO.AddComponent<Canvas>();
        combatCanvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        combatCanvas.sortingOrder = 100;
        var ccScaler = combatCanvasGO.AddComponent<CanvasScaler>();
        ccScaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        ccScaler.referenceResolution  = new Vector2(1920, 1080);
        ccScaler.matchWidthOrHeight   = 0.5f;
        combatCanvasGO.AddComponent<GraphicRaycaster>();
        var combatCG          = combatCanvasGO.AddComponent<CanvasGroup>();
        combatCG.alpha         = 0f;
        combatCG.interactable  = false;
        combatCG.blocksRaycasts = false;

        // Fondo
        IC_Stretch(IC_Img(combatCanvasGO, "Background", new Color(0.07f, 0.07f, 0.11f)));

        // Área de personajes
        var charArea = IC_Child(combatCanvasGO, "CharacterArea");
        IC_RectAt(charArea, 0, 70, 1920, 620);

        var mikeArea = IC_Child(charArea, "MikeArea");
        IC_RectAt(mikeArea, -580, 0, 300, 420);
        var mikePortrait = IC_Img(mikeArea, "MikePortrait", new Color(0.18f, 0.52f, 0.18f));
        IC_RectAt(mikePortrait, 0, 30, 220, 290);
        IC_TMP(mikeArea, "MikeLabel", "MIKE", 16, Color.white, 0, -175, 220, 28);
        IC_TMP(mikeArea, "MikeSubLabel", "[Ternero Mutado]", 11, new Color(0.6f, 0.8f, 0.6f), 0, -198, 220, 22);

        var enemyArea = IC_Child(charArea, "EnemiesArea");
        IC_RectAt(enemyArea, 260, 0, 760, 420);

        // ── EnemyDisplay_0 ── (enemyIndex = 0)
        // Estructura: contenedor → marco → SpriteImage (donde se pone el sprite real)
        var e0GO    = IC_Child(enemyArea, "EnemyDisplay_0");
        IC_RectAt(e0GO, -190, 10, 240, 380);
        var e0Frame = IC_Img(e0GO, "Frame", new Color(0.10f, 0.10f, 0.13f));
        IC_Stretch(e0Frame);
        var e0SprGO = IC_Child(e0GO, "SpriteImage");
        IC_RectAt(e0SprGO, 0, 10, 220, 350);
        var e0Img   = e0SprGO.AddComponent<Image>();
        e0Img.color          = Color.white;
        e0Img.preserveAspect = true;
        var e0Anim  = e0SprGO.AddComponent<EnemyUISpriteAnimator>();   // animación idle
        var disp0   = e0GO.AddComponent<EnemyActionDisplay>();

        // ── EnemyDisplay_1 ── (enemyIndex = 1)
        var e1GO    = IC_Child(enemyArea, "EnemyDisplay_1");
        IC_RectAt(e1GO, 190, 10, 240, 380);
        var e1Frame = IC_Img(e1GO, "Frame", new Color(0.10f, 0.10f, 0.13f));
        IC_Stretch(e1Frame);
        var e1SprGO = IC_Child(e1GO, "SpriteImage");
        IC_RectAt(e1SprGO, 0, 10, 220, 350);
        var e1Img   = e1SprGO.AddComponent<Image>();
        e1Img.color          = Color.white;
        e1Img.preserveAspect = true;
        var e1Anim  = e1SprGO.AddComponent<EnemyUISpriteAnimator>();   // animación idle
        var disp1   = e1GO.AddComponent<EnemyActionDisplay>();

        // HUDs de enemigos
        var hudRoot = IC_Child(combatCanvasGO, "EnemyHUDs");
        IC_RectAt(hudRoot, 260, 400, 760, 80);
        IC_BuildEnemyHUD(hudRoot, "EnemyHUD_0", -215, "Conejo Mutado",
            out var eHP0, out var eName0, out var eWeak0, out var eSel0);
        IC_BuildEnemyHUD(hudRoot, "EnemyHUD_1",  115, "Jabalí Infectado",
            out var eHP1, out var eName1, out var eWeak1, out var eSel1);

        // HUD del jugador
        var playerHUD = IC_Child(combatCanvasGO, "PlayerHUD");
        IC_RectAt(playerHUD, -530, -310, 620, 170);
        IC_Stretch(IC_Img(playerHUD, "PlayerHUDBG", new Color(0f, 0f, 0f, 0.55f)));
        var hpSliderGO = IC_BuildStatRow(playerHUD, "HP", "HealthBar", 0,  50, new Color(0.9f, 0.2f, 0.2f), out var hpTMPGO);
        var enSliderGO = IC_BuildStatRow(playerHUD, "EN", "EnergyBar", 0,  10, new Color(0.3f, 0.6f, 1.0f), out var enTMPGO);
        var hgSliderGO = IC_BuildStatRow(playerHUD, "HG", "HungerBar", 0, -30, new Color(0.9f, 0.6f, 0.1f), out var hgTMPGO);

        // Action panel
        var actionPanel = IC_Child(combatCanvasGO, "ActionPanel");
        IC_RectAt(actionPanel, 200, -450, 880, 86);
        IC_Stretch(IC_Img(actionPanel, "ActionBG", new Color(0.08f, 0.08f, 0.12f, 0.96f)));
        var btnAttack   = IC_Btn(actionPanel, "BtnAttack",   "ATACAR",   new Color(0.65f, 0.13f, 0.13f), -320, 0, 190, 66);
        var btnInstinct = IC_Btn(actionPanel, "BtnInstinct", "INSTINTO", new Color(0.13f, 0.38f, 0.65f), -100, 0, 190, 66);
        var btnItem     = IC_Btn(actionPanel, "BtnItem",     "ÍTEM",     new Color(0.13f, 0.52f, 0.22f),  120, 0, 190, 66);
        var btnDefend   = IC_Btn(actionPanel, "BtnDefend",   "DEFENDER", new Color(0.38f, 0.30f, 0.52f),  340, 0, 190, 66);

        // Item panel (oculto)
        var itemPanel = IC_Child(combatCanvasGO, "ItemPanel");
        IC_RectAt(itemPanel, 200, -240, 520, 420);
        IC_Stretch(IC_Img(itemPanel, "ItemBG", new Color(0.06f, 0.06f, 0.1f, 0.97f)));
        IC_TMP(itemPanel, "ItemTitle", "── ÍTEMS ──", 16, new Color(0.7f, 0.85f, 0.7f), 0, 180, 500, 30);
        var scrollGO   = IC_Child(itemPanel, "ScrollView");   IC_RectAt(scrollGO, 0, -20, 500, 350);
        var scrollRect = scrollGO.AddComponent<ScrollRect>();
        var viewport   = IC_Child(scrollGO, "Viewport");     IC_Stretch(viewport);
        viewport.AddComponent<RectMask2D>();
        var content = IC_Child(viewport, "Content");
        var contentRT   = content.GetComponent<RectTransform>();
        contentRT.anchorMin         = new Vector2(0, 1);
        contentRT.anchorMax         = new Vector2(1, 1);
        contentRT.pivot             = new Vector2(0.5f, 1f);
        contentRT.anchoredPosition  = Vector2.zero;
        contentRT.sizeDelta         = Vector2.zero;
        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 4f;
        vlg.padding = new RectOffset(10, 10, 6, 6);
        content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content    = contentRT;
        scrollRect.viewport   = viewport.GetComponent<RectTransform>();
        scrollRect.horizontal = false;
        scrollRect.vertical   = true;
        itemPanel.SetActive(false);

        // Analysis panel (oculto)
        var analysisPanel = IC_Child(combatCanvasGO, "AnalysisPanel");
        IC_RectAt(analysisPanel, 0, 0, 860, 430);
        IC_Stretch(IC_Img(analysisPanel, "ABG", new Color(0.04f, 0.04f, 0.08f, 0.97f)));
        var aMainGO = IC_TMP(analysisPanel, "AnalysisMainText", "[Descripción corporal]",
            16, new Color(0.9f, 0.85f, 0.72f), 0, 80, 820, 210);
        aMainGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.TopLeft;
        var aHintGO = IC_TMP(analysisPanel, "AnalysisHintText", "[Pista de debilidad]",
            13, new Color(0.55f, 0.72f, 0.9f), 0, -135, 820, 100);
        IC_TMP(analysisPanel, "AnalysisLabel", "[ MIRADA INSTINTIVA ]",
            11, new Color(0.3f, 0.55f, 0.8f), 0, 195, 820, 22);
        analysisPanel.SetActive(false);

        // Message box (oculto)
        var messageBox = IC_Child(combatCanvasGO, "MessageBox");
        IC_RectAt(messageBox, 0, 420, 1100, 62);
        IC_Stretch(IC_Img(messageBox, "MBG", new Color(0f, 0f, 0f, 0.88f)));
        var msgTextGO = IC_TMP(messageBox, "MessageText", "", 18, Color.white, 0, 0, 1060, 62);
        messageBox.SetActive(false);

        // Overlay de daño recibido por el jugador (pantalla roja semitransparente)
        // Debe estar encima de todo el canvas → crearlo antes de V/D screens
        var damageOverlayGO = IC_Child(combatCanvasGO, "PlayerDamageOverlay");
        IC_Stretch(damageOverlayGO);
        var damageOverlayImg = damageOverlayGO.AddComponent<Image>();
        damageOverlayImg.color = new Color(1f, 0f, 0f, 0f);
        damageOverlayGO.SetActive(false);

        // Victory / Defeat screens (ocultos)
        var victoryScreen = IC_Child(combatCanvasGO, "VictoryScreen");
        IC_Stretch(victoryScreen);
        IC_Stretch(IC_Img(victoryScreen, "VBG", new Color(0f, 0.04f, 0f, 0.92f)));
        IC_TMP(victoryScreen, "VText", "¡VICTORIA!", 80, new Color(0.9f, 0.85f, 0.18f), 0, 60, 900, 130);
        victoryScreen.SetActive(false);

        var defeatScreen = IC_Child(combatCanvasGO, "DefeatScreen");
        IC_Stretch(defeatScreen);
        IC_Stretch(IC_Img(defeatScreen, "DBG", new Color(0.06f, 0f, 0f, 0.92f)));
        IC_TMP(defeatScreen, "DText", "HAS CAÍDO...", 80, new Color(0.9f, 0.25f, 0.25f), 0, 60, 900, 130);
        defeatScreen.SetActive(false);

        // ItemTooltip
        var ttGO = new GameObject("ItemTooltip");
        ttGO.transform.SetParent(combatCanvasGO.transform, false);
        IC_Stretch(ttGO.AddComponent<RectTransform>());
        ttGO.AddComponent<ItemTooltip>();

        // BattleFeedback
        var fbGO   = new GameObject("BattleFeedback");
        fbGO.transform.SetParent(combatCanvasGO.transform, false);
        IC_Stretch(fbGO.AddComponent<RectTransform>());
        var fbSys  = fbGO.AddComponent<BattleFeedbackSystem>();

        var turnGO  = MakeText(fbGO, "TurnIndicator",
            new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(0, -70), new Vector2(0, -20),
            "Iniciando combate…", 22, FontStyles.Bold);
        var turnTMP = turnGO.GetComponent<TextMeshProUGUI>();
        turnTMP.alignment = TextAlignmentOptions.Center;
        turnTMP.color     = new Color(0.3f, 1f, 0.4f);

        var logBg = new GameObject("BattleLogBG");
        logBg.transform.SetParent(fbGO.transform, false);
        var logBgRT = logBg.AddComponent<RectTransform>();
        // Arriba-izquierda: debajo del TurnIndicator (y=1010px), a la izquierda de EnemyHUDs (x=840px+)
        logBgRT.anchorMin = new Vector2(0f, 0.82f);
        logBgRT.anchorMax = new Vector2(0.42f, 0.93f);
        logBgRT.offsetMin = new Vector2(10, 8);
        logBgRT.offsetMax = new Vector2(-5, -5);
        logBg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        var logGO  = MakeText(logBg, "BattleLog", Vector2.zero, Vector2.one,
            new Vector2(8, 8), new Vector2(-8, -8), "", 13, FontStyles.Normal);
        var logTMP = logGO.GetComponent<TextMeshProUGUI>();
        logTMP.alignment  = TextAlignmentOptions.TopLeft;   // más antiguo arriba, más nuevo abajo
        logTMP.color      = Color.white;
        logTMP.overflowMode = TextOverflowModes.Truncate;

        var tutGO = new GameObject("TutorialPanel");
        tutGO.transform.SetParent(fbGO.transform, false);
        var tutRT = tutGO.AddComponent<RectTransform>();
        tutRT.anchorMin = new Vector2(0.2f, 0.25f);
        tutRT.anchorMax = new Vector2(0.8f, 0.75f);
        tutRT.offsetMin = tutRT.offsetMax = Vector2.zero;
        tutGO.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.1f, 0.95f);
        var tutTxtGO = MakeText(tutGO, "TutorialText", Vector2.zero, Vector2.one,
            new Vector2(20, 60), new Vector2(-20, -20),
            "<b>CÓMO JUGAR</b>\n\n" +
            "1. Haz clic en el retrato de un <b>enemigo</b> para seleccionarlo.\n\n" +
            "<b>ATACAR</b>  →  Golpe directo.\n" +
            "<b>INSTINTO</b>  →  Analiza y expone la debilidad del enemigo.\n" +
            "<b>ÍTEM</b>  →  Abre el inventario.\n" +
            "<b>DEFENDER</b>  →  Reduce el daño y recupera energía.\n\n" +
            "Los botones se activan sólo en TU TURNO.", 14, FontStyles.Normal);
        tutTxtGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.TopLeft;
        tutTxtGO.GetComponent<TextMeshProUGUI>().color     = Color.white;

        var closeBtnGO = new GameObject("CloseTutorialBtn");
        closeBtnGO.transform.SetParent(tutGO.transform, false);
        var closeBtnRT = closeBtnGO.AddComponent<RectTransform>();
        closeBtnRT.anchorMin = new Vector2(0f, 0f);
        closeBtnRT.anchorMax = new Vector2(1f, 0f);
        closeBtnRT.offsetMin = new Vector2(20, 8);
        closeBtnRT.offsetMax = new Vector2(-20, 48);
        closeBtnGO.AddComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f);
        var closeBtn   = closeBtnGO.AddComponent<Button>();
        var closeLblGO = MakeText(closeBtnGO, "Label", Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero, "Entendido — ¡a combatir!", 15, FontStyles.Bold);
        closeLblGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        closeLblGO.GetComponent<TextMeshProUGUI>().color     = Color.white;

        var fbSO = new SerializedObject(fbSys);
        fbSO.FindProperty("turnIndicatorText").objectReferenceValue = turnTMP;
        fbSO.FindProperty("battleLogText").objectReferenceValue     = logTMP;
        fbSO.FindProperty("tutorialPanel").objectReferenceValue     = tutGO;
        fbSO.FindProperty("closeTutorialBtn").objectReferenceValue  = closeBtn;
        fbSO.ApplyModifiedProperties();

        // ── 7: CombatUI — asignar todos los campos ────────────────────────────
        var combatUI = combatCanvasGO.AddComponent<CombatUI>();
        var uiSO     = new SerializedObject(combatUI);

        IC_Set(uiSO, "playerHealthBar",   hpSliderGO.GetComponent<Slider>());
        IC_Set(uiSO, "playerEnergyBar",   enSliderGO.GetComponent<Slider>());
        IC_Set(uiSO, "playerHealthText",  hpTMPGO.GetComponent<TextMeshProUGUI>());
        IC_Set(uiSO, "playerPortrait",    mikePortrait.GetComponent<Image>());
        IC_Set(uiSO, "actionPanel",       actionPanel);
        IC_Set(uiSO, "btnAttack",         btnAttack.GetComponent<Button>());
        IC_Set(uiSO, "btnInstinct",       btnInstinct.GetComponent<Button>());
        IC_Set(uiSO, "btnItem",           btnItem.GetComponent<Button>());
        IC_Set(uiSO, "btnDefend",         btnDefend.GetComponent<Button>());
        IC_Set(uiSO, "itemPanel",         itemPanel);
        IC_Set(uiSO, "itemListContainer", contentRT);
        if (itemBtnPrefab != null) IC_Set(uiSO, "itemButtonPrefab", itemBtnPrefab);
        if (dmgNumPrefab  != null) IC_Set(uiSO, "damageNumberPrefab", dmgNumPrefab);
        IC_Set(uiSO, "analysisPanel",     analysisPanel);
        IC_Set(uiSO, "analysisMainText",  aMainGO.GetComponent<TextMeshProUGUI>());
        IC_Set(uiSO, "analysisHintText",  aHintGO.GetComponent<TextMeshProUGUI>());
        IC_Set(uiSO, "messageBox",        messageBox);
        IC_Set(uiSO, "messageText",       msgTextGO.GetComponent<TextMeshProUGUI>());
        IC_Set(uiSO, "victoryScreen",     victoryScreen);
        IC_Set(uiSO, "defeatScreen",      defeatScreen);
        IC_Set(uiSO, "mainCanvasGroup",   combatCG);

        var hudsProp = uiSO.FindProperty("enemyHUDs");
        hudsProp.arraySize = 2;
        var h0 = hudsProp.GetArrayElementAtIndex(0);
        h0.FindPropertyRelative("healthBar").objectReferenceValue        = eHP0;
        h0.FindPropertyRelative("nameLabel").objectReferenceValue        = eName0;
        h0.FindPropertyRelative("weaknessIndicator").objectReferenceValue = eWeak0;
        h0.FindPropertyRelative("selectionHighlight").objectReferenceValue = eSel0;
        var h1 = hudsProp.GetArrayElementAtIndex(1);
        h1.FindPropertyRelative("healthBar").objectReferenceValue        = eHP1;
        h1.FindPropertyRelative("nameLabel").objectReferenceValue        = eName1;
        h1.FindPropertyRelative("weaknessIndicator").objectReferenceValue = eWeak1;
        h1.FindPropertyRelative("selectionHighlight").objectReferenceValue = eSel1;
        uiSO.FindProperty("enemyViews").arraySize = 2;
        uiSO.ApplyModifiedProperties();

        // EnemyActionDisplay — índice 0 y 1 (más robusto que nombre, soporta boss fight)
        IC_WireDisplayByIndex(disp0, e0Img, 0, e0Anim);
        IC_WireDisplayByIndex(disp1, e1Img, 1, e1Anim);

        // CombatHitFeedback — feedback visual de daño
        // NOTA: ya no usa enemyDisplayImages[]; localiza EnemyActionDisplay dinámicamente
        // mediante EnemyActionDisplay.TracksEnemy(). Esto elimina el bug de referencias cruzadas.
        var hitFeedback = combatCanvasGO.AddComponent<CombatHitFeedback>();
        var hfSO = new SerializedObject(hitFeedback);
        hfSO.FindProperty("playerPortraitImage").objectReferenceValue = mikePortrait.GetComponent<Image>();
        hfSO.FindProperty("screenDamageOverlay").objectReferenceValue = damageOverlayImg;
        hfSO.FindProperty("combatCamera").objectReferenceValue        = combatCamera;
        hfSO.ApplyModifiedProperties();

        Debug.Log("✓ _CombatCanvas creada, CombatUI y CombatHitFeedback cableados");

        // ── 8: _CombatController ──────────────────────────────────────────────
        var controllerGO = new GameObject("_CombatController");
        controllerGO.AddComponent<CombatManager>();
        var combatCtrl   = controllerGO.AddComponent<InSceneCombatController>();
        var postCombatFlow = controllerGO.AddComponent<PostCombatFlow>();

        // Wirear canvas de overlays en PostCombatFlow
        var pcfSO = new SerializedObject(postCombatFlow);
        pcfSO.FindProperty("postCombatCanvas").objectReferenceValue = combatCanvas;
        pcfSO.ApplyModifiedProperties();

        var ctrlSO = new SerializedObject(combatCtrl);
        if (mikeSO != null) IC_Set(ctrlSO, "playerStats", mikeSO);

        var startItems = ctrlSO.FindProperty("startingItems");
        startItems.arraySize = itemSOs.Count;
        for (int i = 0; i < itemSOs.Count; i++)
            startItems.GetArrayElementAtIndex(i).objectReferenceValue = itemSOs[i];

        IC_Set(ctrlSO, "hungerBarInCombat", hgSliderGO.GetComponent<Slider>());

        // Scripts de movimiento a deshabilitar durante el combate
        var movProp = ctrlSO.FindProperty("playerMovementScripts");
        movProp.arraySize = camCtrl != null ? 2 : 1;
        movProp.GetArrayElementAtIndex(0).objectReferenceValue = playerMovement;
        if (camCtrl != null)
            movProp.GetArrayElementAtIndex(1).objectReferenceValue = camCtrl;

        if (explorationCam != null) IC_Set(ctrlSO, "explorationCamera", explorationCam);
        IC_Set(ctrlSO, "combatCamera",        combatCamera);
        IC_Set(ctrlSO, "combatCanvas",        combatCanvas);
        IC_Set(ctrlSO, "combatUI",            combatUI);
        IC_Set(ctrlSO, "combatCanvasGroup",   combatCG);
        IC_Set(ctrlSO, "screenFadeImage",     fadeImage);
        IC_Set(ctrlSO, "playerCombatAnimator", playerCombatAnim);
        ctrlSO.ApplyModifiedProperties();
        Debug.Log("✓ _CombatController creado y cableado");

        // ── 9: Trigger de combate ─────────────────────────────────────────────
        var triggerGO = new GameObject("_CombatTrigger_01");
        triggerGO.transform.position = playerGO.transform.position + new Vector3(5f, 0f, 0f);

        var box     = triggerGO.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size      = new Vector3(3f, 2f, 3f);
        box.center    = new Vector3(0f, 1f, 0f);

        var trigger = triggerGO.AddComponent<InSceneCombatTrigger>();
        var trigSO  = new SerializedObject(trigger);
        var enemies = trigSO.FindProperty("enemies");

        if (conejoSO != null && jabaliSO != null)
        {
            enemies.arraySize = 2;
            enemies.GetArrayElementAtIndex(0).objectReferenceValue = conejoSO;
            enemies.GetArrayElementAtIndex(1).objectReferenceValue = jabaliSO;
        }
        else if (conejoSO != null)
        {
            enemies.arraySize = 1;
            enemies.GetArrayElementAtIndex(0).objectReferenceValue = conejoSO;
        }
        trigSO.ApplyModifiedProperties();
        Debug.Log($"✓ _CombatTrigger_01 creado en {triggerGO.transform.position}");

        // ── 10: EventSystem (si falta) ────────────────────────────────────────
        if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
            Type inputType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                inputType = asm.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule");
                if (inputType != null) break;
            }
            if (inputType != null) esGO.AddComponent(inputType);
            else esGO.AddComponent<StandaloneInputModule>();
            Debug.Log("✓ EventSystem creado");
        }

        // ── 11: Guardar escena ────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(
            "✅ InScene Combat configurado en la escena.\n\n" +
            "PASOS SIGUIENTES:\n" +
            "1. Mueve '_CombatTrigger_01' donde quieras el encuentro en el mundo.\n" +
            "2. En '_CombatTrigger_01 > InSceneCombatTrigger', asigna los GOs de\n" +
            "   'Enemy Visuals' (sprites/modelos de enemigos visibles en exploración).\n" +
            "3. Opcional: ajusta pos/rot de '_CombatCamera' para el ángulo de combate.\n" +
            "4. Opcional: asigna 'Combat Camera Anchor' en el trigger para fijar el ángulo.\n" +
            "5. Si CombatScene ya existe, ya tienes los ScriptableObjects — ¡todo listo!\n" +
            "6. Presiona Play y camina hacia el trigger."
        );
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 6 — Fix Battle Log Position (patch escena existente)
    // ══════════════════════════════════════════════════════════════════════════
    // Mueve el BattleLogBG al área arriba-izquierda (debajo del TurnIndicator).
    // Ejecutar UNA VEZ si ya tenías el combate configurado con el menú 3 ó 5.
    [MenuItem("Kimera/6 - Fix Battle Log Position (parchar escena existente)")]
    public static void FixBattleLogPosition()
    {
        var logBg = GameObject.Find("BattleLogBG");
        if (logBg == null)
        {
            Debug.LogError("'BattleLogBG' no encontrado. ¿Ejecutaste el menú 3 ó 5 primero? " +
                           "Asegúrate de que la escena correcta esté abierta.");
            return;
        }

        var rt = logBg.GetComponent<RectTransform>();
        if (rt == null) { Debug.LogError("BattleLogBG no tiene RectTransform."); return; }

        rt.anchorMin = new Vector2(0f, 0.82f);
        rt.anchorMax = new Vector2(0.42f, 0.93f);
        rt.offsetMin = new Vector2(10, 8);
        rt.offsetMax = new Vector2(-5, -5);
        Debug.Log("✓ BattleLogBG reposicionado (arriba-izquierda)");

        // Actualizar alineación del texto del log
        var logTextT = logBg.transform.Find("BattleLog");
        if (logTextT != null)
        {
            var tmp = logTextT.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                tmp.alignment = TextAlignmentOptions.TopLeft;
                Debug.Log("✓ BattleLog alignment → TopLeft");
            }
        }
        else
        {
            Debug.LogWarning("No se encontró 'BattleLog' como hijo de BattleLogBG. " +
                             "Ajusta la alineación manualmente.");
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("✓ Battle Log corregido. Escena guardada.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 7 — Create SoundManager in scene
    // ══════════════════════════════════════════════════════════════════════════
    // Crea el SoundManager con sus AudioSources hijos.
    // Ejecutar UNA VEZ con la escena Juego abierta.
    [MenuItem("Kimera/7 - Create SoundManager (escena Juego)")]
    public static void CreateSoundManager()
    {
        if (UnityEngine.Object.FindFirstObjectByType<SoundManager>() != null)
        {
            Debug.LogWarning("SoundManager ya existe en la escena.");
            return;
        }

        var smGO = new GameObject("SoundManager");
        var sm   = smGO.AddComponent<SoundManager>();

        // AudioSource para música (loop)
        var musicGO  = new GameObject("MusicSource");
        musicGO.transform.SetParent(smGO.transform);
        var musicSrc = musicGO.AddComponent<AudioSource>();
        musicSrc.loop        = true;
        musicSrc.playOnAwake = false;
        musicSrc.volume      = 0.55f;

        // AudioSource para SFX (one-shot)
        var sfxGO  = new GameObject("SFXSource");
        sfxGO.transform.SetParent(smGO.transform);
        var sfxSrc = sfxGO.AddComponent<AudioSource>();
        sfxSrc.loop        = false;
        sfxSrc.playOnAwake = false;
        sfxSrc.volume      = 1f;

        // Cablear las referencias
        var smSO = new SerializedObject(sm);
        smSO.FindProperty("musicSource").objectReferenceValue = musicSrc;
        smSO.FindProperty("sfxSource").objectReferenceValue   = sfxSrc;
        smSO.ApplyModifiedProperties();

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(
            "✓ SoundManager creado con MusicSource y SFXSource.\n\n" +
            "PASOS SIGUIENTES:\n" +
            "1. Selecciona el GO 'SoundManager' en la Jerarquía.\n" +
            "2. En el Inspector, arrastra tus clips AudioClip a los campos de cada categoría:\n" +
            "   · Música → explorationMusic, combatMusic, victoryJingle, defeatJingle\n" +
            "   · UI     → uiHover, uiClick\n" +
            "   · Jugador → playerAttackSFX, playerDamageSFX, playerInstinctSFX, …\n" +
            "   · Enemigos → enemyAttackSFX, enemyDamageSFX\n" +
            "3. ¡Todo lo demás se conecta automáticamente! Los eventos de combate\n" +
            "   disparan los SFX correctos sin código extra."
        );
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 8 — Fix Enemy Display Wiring
    // ══════════════════════════════════════════════════════════════════════════
    // Repara el bug donde ambos displays muestran el mismo sprite.
    // Busca EnemyDisplay_0 y EnemyDisplay_1 en la escena, verifica que cada
    // EnemyActionDisplay apunta a su propio SpriteImage, y lo corrige si no.
    // También añade EnemyUISpriteAnimator si falta.
    [MenuItem("Kimera/8 - Fix Enemy Display Wiring (arreglar sprite duplicado)")]
    public static void FixEnemyDisplayWiring()
    {
        // ── Diagnóstico previo ────────────────────────────────────────────────
        var allBefore = UnityEngine.Object.FindObjectsByType<EnemyActionDisplay>(FindObjectsSortMode.None);
        Debug.Log($"── ANTES del fix ({allBefore.Length} EnemyActionDisplay) ──────────────");
        foreach (var d in allBefore)
        {
            var dSO = new SerializedObject(d);
            int idx = dSO.FindProperty("enemyIndex").intValue;
            var img = dSO.FindProperty("displayImage").objectReferenceValue;
            Debug.Log($"  {d.gameObject.name}  enemyIndex={idx}  displayImage={img?.name ?? "NULL ⚠️"}  GO={(img != null ? ((Image)img).gameObject.GetInstanceID().ToString() : "-")}");
        }
        Debug.Log("──────────────────────────────────────────────────────────");

        int fixed_ = 0;

        string[] names  = { "EnemyDisplay_0", "EnemyDisplay_1" };
        int[]    indices = { 0, 1 };

        for (int i = 0; i < names.Length; i++)
        {
            GameObject dispGO = GameObject.Find(names[i]);
            if (dispGO == null)
            {
                Debug.LogWarning($"'{names[i]}' no encontrado en la escena. " +
                                 "¿Ejecutaste el menú 5? Asegúrate de que la escena Juego esté abierta.");
                continue;
            }

            var disp = dispGO.GetComponent<EnemyActionDisplay>();
            if (disp == null)
            {
                Debug.LogWarning($"'{names[i]}' no tiene EnemyActionDisplay. Añadiéndolo...");
                disp = dispGO.AddComponent<EnemyActionDisplay>();
            }

            // Buscar el GO hijo "SpriteImage"
            Transform spriteT = dispGO.transform.Find("SpriteImage");
            if (spriteT == null)
            {
                Debug.LogError($"'{names[i]}' no tiene un hijo llamado 'SpriteImage'. " +
                               "Revisa la jerarquía o recrea con el menú 5.");
                continue;
            }

            Image img = spriteT.GetComponent<Image>();
            if (img == null)
            {
                img = spriteT.gameObject.AddComponent<Image>();
                img.color          = Color.white;
                img.preserveAspect = true;
                Debug.Log($"✓ Image añadida a {names[i]}/SpriteImage");
            }

            // Añadir EnemyUISpriteAnimator si falta
            EnemyUISpriteAnimator anim = spriteT.GetComponent<EnemyUISpriteAnimator>()
                                      ?? spriteT.gameObject.AddComponent<EnemyUISpriteAnimator>();

            // Re-cablear (fuerza los valores correctos)
            var so = new SerializedObject(disp);
            var imgProp  = so.FindProperty("displayImage");
            var idxProp  = so.FindProperty("enemyIndex");
            var animProp = so.FindProperty("spriteAnimator");

            bool wasWrong = imgProp.objectReferenceValue != img ||
                            idxProp.intValue != indices[i];

            imgProp.objectReferenceValue  = img;
            idxProp.intValue              = indices[i];
            so.FindProperty("trackedEnemyName").stringValue = "";
            if (animProp != null) animProp.objectReferenceValue = anim;
            so.ApplyModifiedProperties();

            if (wasWrong)
            {
                Debug.Log($"✓ {names[i]} corregido: displayImage → {names[i]}/SpriteImage, enemyIndex = {indices[i]}");
                fixed_++;
            }
            else
            {
                Debug.Log($"  {names[i]} ya estaba correcto (enemyIndex={indices[i]})");
            }
        }

        // ── Limpiar CombatHitFeedback: ya no usa enemyDisplayImages[] ────────────
        // El campo fue eliminado del script; CombatHitFeedback ahora usa
        // EnemyActionDisplay.TracksEnemy() para localizar el display correcto.
        var hitFeedback = UnityEngine.Object.FindFirstObjectByType<CombatHitFeedback>();
        if (hitFeedback != null)
        {
            var hfSO    = new SerializedObject(hitFeedback);
            var hfProp  = hfSO.FindProperty("enemyDisplayImages");
            if (hfProp != null)
            {
                // El campo todavía existe en la versión antigua del script → limpiar
                hfProp.arraySize = 0;
                hfSO.ApplyModifiedProperties();
                Debug.Log("✓ CombatHitFeedback.enemyDisplayImages limpiado " +
                          "(campo obsoleto — ya no es necesario)");
            }
            else
            {
                Debug.Log("✓ CombatHitFeedback ya tiene la versión nueva (sin enemyDisplayImages). Correcto.");
            }
        }
        else
        {
            Debug.LogWarning("CombatHitFeedback no encontrado en la escena. " +
                             "¿Ejecutaste el menú 5 en esta escena?");
        }

        // ── Diagnóstico final ─────────────────────────────────────────────────
        var allDisplays = UnityEngine.Object.FindObjectsByType<EnemyActionDisplay>(FindObjectsSortMode.None);
        Debug.Log($"\n── DIAGNÓSTICO EnemyActionDisplay ({allDisplays.Length} componentes) ──");
        foreach (var d in allDisplays)
        {
            var dSO = new SerializedObject(d);
            int idx  = dSO.FindProperty("enemyIndex").intValue;
            var img  = dSO.FindProperty("displayImage").objectReferenceValue;
            Debug.Log($"  GO={d.gameObject.name}  enemyIndex={idx}  " +
                      $"displayImage={img?.name ?? "NULL ⚠️"}  " +
                      $"activo={d.gameObject.activeSelf}");
        }
        Debug.Log("────────────────────────────────────────────────────");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        if (fixed_ > 0)
            Debug.Log($"✅ {fixed_} display(s) corregido(s). Presiona Play para probar.\n" +
                      "Si los enemigos no tienen sprite, asígnalo en sus EnemyData ScriptableObjects:\n" +
                      "  Assets/ScriptableObjects/Data/Enemy_Conejo.asset  → campo 'Sprite'\n" +
                      "  Assets/ScriptableObjects/Data/Enemy_Jabali.asset  → campo 'Sprite'\n" +
                      "  Assets/ScriptableObjects/Data/Enemy_Hiena.asset   → campo 'Sprite'\n" +
                      "Para animación idle: arrastra los frames en 'Idle Animation Frames' del mismo SO.");
        else
            Debug.Log("✅ Todo estaba correcto. Si el bug persiste:\n" +
                      "  1. Asigna sprites en los EnemyData ScriptableObjects.\n" +
                      "  2. Para animación idle, arrastra los frames en EnemyData.idleAnimationFrames.\n" +
                      "  3. Ejecuta el menú 9 para resetear posiciones si los displays están apilados.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 9 — Fix Enemy Display Positions
    // ══════════════════════════════════════════════════════════════════════════
    // Resetea las posiciones de EnemyDisplay_0 y EnemyDisplay_1 para que queden
    // separados en pantalla (Display_0 a la izquierda, Display_1 a la derecha).
    // Ejecutar si los displays aparecen apilados o fuera de pantalla.
    // Después puedes moverlos libremente desde el Inspector.
    [MenuItem("Kimera/9 - Fix Enemy Display Positions (reposicionar si están apilados)")]
    public static void FixEnemyDisplayPositions()
    {
        GameObject e0 = GameObject.Find("EnemyDisplay_0");
        GameObject e1 = GameObject.Find("EnemyDisplay_1");

        if (e0 == null && e1 == null)
        {
            Debug.LogError("No se encontraron EnemyDisplay_0 ni EnemyDisplay_1 en la escena. " +
                           "¿Ejecutaste el menú 5 primero?");
            return;
        }

        // Helper local para reposicionar un display y su SpriteImage hijo
        static void FixDisplay(GameObject go, float anchorX, float anchorY,
                               float sizeW, float sizeH,
                               float spriteOffX, float spriteOffY,
                               float spriteSizeW, float spriteSizeH)
        {
            if (go == null) return;

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin         = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition  = new Vector2(anchorX, anchorY);
                rt.sizeDelta         = new Vector2(sizeW, sizeH);
                Debug.Log($"✓ {go.name} → pos({anchorX},{anchorY}) size({sizeW}×{sizeH})");
            }

            var spriteT = go.transform.Find("SpriteImage");
            if (spriteT != null)
            {
                var srt = spriteT.GetComponent<RectTransform>();
                if (srt != null)
                {
                    srt.anchorMin        = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
                    srt.anchoredPosition = new Vector2(spriteOffX, spriteOffY);
                    srt.sizeDelta        = new Vector2(spriteSizeW, spriteSizeH);
                    Debug.Log($"✓ {go.name}/SpriteImage → pos({spriteOffX},{spriteOffY}) size({spriteSizeW}×{spriteSizeH})");
                }
            }
        }

        // Display_0 a la izquierda, Display_1 a la derecha
        // (posiciones relativas al padre EnemiesArea, que está centrado en pantalla)
        FixDisplay(e0,  -190f, 10f,  240f, 380f,   0f, 10f,  220f, 350f);
        FixDisplay(e1,   190f, 10f,  240f, 380f,   0f, 10f,  220f, 350f);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(
            "✅ Posiciones de EnemyDisplay reseteadas.\n" +
            "  Display_0 (izquierda): posición (-190, 10), tamaño 240×380\n" +
            "  Display_1 (derecha):   posición ( 190, 10), tamaño 240×380\n\n" +
            "Puedes moverlos / redimensionarlos libremente desde el Inspector.\n" +
            "El código NO los reposicionará en runtime."
        );
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 10 — Repair CombatUI References
    // ══════════════════════════════════════════════════════════════════════════
    // Repara referencias nulas en CombatUI (playerHealthBar, playerEnergyBar, etc.)
    // buscando los GameObjects por nombre en la jerarquía del _CombatCanvas.
    // Ejecutar si ves "playerHealthBar nulo" o similares en la Consola.
    [MenuItem("Kimera/10 - Repair CombatUI References (arreglar HUD nulo)")]
    public static void RepairCombatUIReferences()
    {
        var combatUI = UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        if (combatUI == null)
        {
            Debug.LogError("CombatUI no encontrado en la escena. ¿Ejecutaste el menú 5?");
            return;
        }

        var uiSO    = new SerializedObject(combatUI);
        int repaired = 0;
        int already  = 0;

        // ── Helpers locales ───────────────────────────────────────────────────
        void WireSlider(string field, string goPath)
        {
            var prop = uiSO.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"  Campo '{field}' no existe en CombatUI."); return; }
            if (prop.objectReferenceValue != null) { already++; return; }

            var go = GameObject.Find(goPath);
            if (go == null)
            {
                Debug.LogError($"  '{field}': GO no encontrado en '{goPath}'. " +
                               "Asigna el Slider manualmente en el Inspector.");
                return;
            }
            var sl = go.GetComponent<Slider>();
            if (sl == null)
            {
                Debug.LogError($"  '{field}': '{goPath}' no tiene Slider.");
                return;
            }
            prop.objectReferenceValue = sl;
            repaired++;
            Debug.Log($"  ✓ {field} → {goPath}");
        }

        void WireTMP(string field, string goPath)
        {
            var prop = uiSO.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"  Campo '{field}' no existe."); return; }
            if (prop.objectReferenceValue != null) { already++; return; }

            var go = GameObject.Find(goPath);
            if (go == null)
            {
                Debug.LogError($"  '{field}': GO no encontrado en '{goPath}'.");
                return;
            }
            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null)
            {
                Debug.LogError($"  '{field}': '{goPath}' no tiene TextMeshProUGUI.");
                return;
            }
            prop.objectReferenceValue = tmp;
            repaired++;
            Debug.Log($"  ✓ {field} → {goPath}");
        }

        void WireGO(string field, string goPath)
        {
            var prop = uiSO.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"  Campo '{field}' no existe."); return; }
            if (prop.objectReferenceValue != null) { already++; return; }

            var go = GameObject.Find(goPath);
            if (go == null)
            {
                Debug.LogError($"  '{field}': GO no encontrado en '{goPath}'.");
                return;
            }
            prop.objectReferenceValue = go;
            repaired++;
            Debug.Log($"  ✓ {field} → {goPath}");
        }

        void WireImage(string field, string goPath)
        {
            var prop = uiSO.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"  Campo '{field}' no existe."); return; }
            if (prop.objectReferenceValue != null) { already++; return; }

            var go = GameObject.Find(goPath);
            if (go == null) { Debug.LogError($"  '{field}': GO no encontrado en '{goPath}'."); return; }
            var img = go.GetComponent<Image>();
            if (img == null) { Debug.LogError($"  '{field}': '{goPath}' no tiene Image."); return; }
            prop.objectReferenceValue = img;
            repaired++;
            Debug.Log($"  ✓ {field} → {goPath}");
        }

        void WireButton(string field, string goPath)
        {
            var prop = uiSO.FindProperty(field);
            if (prop == null) { Debug.LogWarning($"  Campo '{field}' no existe."); return; }
            if (prop.objectReferenceValue != null) { already++; return; }

            var go = GameObject.Find(goPath);
            if (go == null) { Debug.LogError($"  '{field}': GO no encontrado en '{goPath}'."); return; }
            var btn = go.GetComponent<Button>();
            if (btn == null) { Debug.LogError($"  '{field}': '{goPath}' no tiene Button."); return; }
            prop.objectReferenceValue = btn;
            repaired++;
            Debug.Log($"  ✓ {field} → {goPath}");
        }

        // ── Reparar todas las referencias ─────────────────────────────────────
        Debug.Log("── Reparando referencias de CombatUI ────────────────────────────");

        // PlayerHUD
        WireSlider("playerHealthBar",  "_CombatCanvas/PlayerHUD/HealthBar");
        WireSlider("playerEnergyBar",  "_CombatCanvas/PlayerHUD/EnergyBar");
        WireTMP("playerHealthText",    "_CombatCanvas/PlayerHUD/HPText");
        WireImage("playerPortrait",    "_CombatCanvas/CharacterArea/MikeArea/MikePortrait");

        // Action buttons
        WireButton("btnAttack",   "_CombatCanvas/ActionPanel/BtnAttack");
        WireButton("btnInstinct", "_CombatCanvas/ActionPanel/BtnInstinct");
        WireButton("btnItem",     "_CombatCanvas/ActionPanel/BtnItem");
        WireButton("btnDefend",   "_CombatCanvas/ActionPanel/BtnDefend");
        WireGO("actionPanel", "_CombatCanvas/ActionPanel");

        // Item panel
        WireGO("itemPanel", "_CombatCanvas/ItemPanel");

        // Analysis panel
        WireGO("analysisPanel", "_CombatCanvas/AnalysisPanel");
        WireTMP("analysisMainText", "_CombatCanvas/AnalysisPanel/AnalysisMainText");
        WireTMP("analysisHintText", "_CombatCanvas/AnalysisPanel/AnalysisHintText");

        // Message box
        WireGO("messageBox", "_CombatCanvas/MessageBox");
        WireTMP("messageText", "_CombatCanvas/MessageBox/MessageText");

        // End screens
        WireGO("victoryScreen", "_CombatCanvas/VictoryScreen");
        WireGO("defeatScreen",  "_CombatCanvas/DefeatScreen");

        // HungerBar (wired en InSceneCombatController, pero por si acaso)
        // La HungerBar va al CombatController, no a CombatUI

        uiSO.ApplyModifiedProperties();

        // ── También reparar InSceneCombatController ───────────────────────────
        var combatCtrl = UnityEngine.Object.FindFirstObjectByType<InSceneCombatController>();
        if (combatCtrl != null)
        {
            var ctrlSO = new SerializedObject(combatCtrl);
            var hbProp = ctrlSO.FindProperty("hungerBarInCombat");
            if (hbProp != null && hbProp.objectReferenceValue == null)
            {
                var hbGO = GameObject.Find("_CombatCanvas/PlayerHUD/HungerBar");
                if (hbGO != null)
                {
                    var sl = hbGO.GetComponent<Slider>();
                    if (sl != null)
                    {
                        hbProp.objectReferenceValue = sl;
                        ctrlSO.ApplyModifiedProperties();
                        repaired++;
                        Debug.Log("  ✓ hungerBarInCombat → _CombatCanvas/PlayerHUD/HungerBar");
                    }
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"────────────────────────────────────────────────────────\n" +
                  $"✅ Reparación completada: {repaired} referencia(s) reparada(s), " +
                  $"{already} ya estaban correctas.");

        if (repaired == 0 && already == 0)
            Debug.LogWarning("No se encontró ninguna referencia. " +
                             "Verifica que '_CombatCanvas' y sus hijos existen en la escena.");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 11 — Setup EnemyDisplayManager (displays persistentes, posicionables)
    // ══════════════════════════════════════════════════════════════════════════
    // Ejecutar UNA VEZ con la escena Juego abierta, DESPUÉS del menú 5.
    //
    // Este menú:
    //   1. Localiza o crea EnemyDisplay_0 y EnemyDisplay_1 como GOs permanentes
    //      en la jerarquía del Canvas (hijo de EnemiesArea).
    //   2. Añade EnemyDisplayManager al _CombatController y cablea los displays.
    //
    // POSICIONAMIENTO:
    //   Después de ejecutar el menú, mueve EnemyDisplay_0 y EnemyDisplay_1
    //   libremente desde la Scene View o el Inspector.
    //   El código NUNCA modifica sus posiciones en runtime.
    //
    // COMBATES:
    //   · Combate normal (conejo + jabalí): ambos displays activos.
    //   · Boss fight (hiena): solo EnemyDisplay_0 activo; Display_1 se oculta.
    //   Cada display tiene su propio RectTransform, Image y EnemyUISpriteAnimator.
    [MenuItem("Kimera/11 - Setup EnemyDisplayManager (displays permanentes)")]
    public static void SetupEnemyDisplayManager()
    {
        // ── Validar escena ────────────────────────────────────────────────────
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.name.ToLower().Contains("juego"))
            Debug.LogWarning($"La escena activa es '{scene.name}', no 'Juego'. Continúa de todas formas…");

        // ── 1: Localizar EnemiesArea ──────────────────────────────────────────
        GameObject enemiesAreaGO = GameObject.Find("EnemiesArea")
                                ?? GameObject.Find("_CombatCanvas/CharacterArea/EnemiesArea");

        if (enemiesAreaGO == null)
        {
            Debug.LogError("'EnemiesArea' no encontrado. ¿Ejecutaste el menú 5 primero?");
            return;
        }

        // ── 2: Crear o reparar EnemyDisplay_0 y EnemyDisplay_1 ───────────────
        // Helper: crea o localiza un display persistente como hijo de EnemiesArea.
        static EnemyActionDisplay EnsureDisplay(GameObject parent, string name,
            int index, float posX, float posY, float sizeW, float sizeH)
        {
            // Buscar GO existente (puede venir del menú 5 o de una ejecución anterior)
            Transform existing = parent.transform.Find(name);
            GameObject go = existing != null ? existing.gameObject : null;

            if (go == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent.transform, false);
                Debug.Log($"  ✓ '{name}' creado como hijo de '{parent.name}'");
            }
            else
            {
                Debug.Log($"  ✓ '{name}' ya existe — se reutiliza (posición actual conservada)");
            }

            // Asegurar RectTransform
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();

            // Solo poner posición/tamaño si el GO es NUEVO (sin componentes previos).
            // Si ya existía, respetamos la posición que el usuario haya configurado.
            bool isNew = go.GetComponent<EnemyActionDisplay>() == null;
            if (isNew)
            {
                rt.anchorMin        = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(posX, posY);
                rt.sizeDelta        = new Vector2(sizeW, sizeH);
                Debug.Log($"    → posición inicial ({posX}, {posY}), tamaño {sizeW}×{sizeH}. " +
                          "Puedes moverlo libremente desde la Scene View.");
            }

            // Asegurar Frame (fondo oscuro)
            Transform frameT = go.transform.Find("Frame");
            if (frameT == null)
            {
                var frameGO = new GameObject("Frame");
                frameGO.transform.SetParent(go.transform, false);
                IC_Stretch(frameGO.AddComponent<RectTransform>());
                var fi = frameGO.AddComponent<Image>();
                fi.color         = new Color(0.10f, 0.10f, 0.13f);
                fi.raycastTarget = false;  // el Frame no debe bloquear clics al sprite
                Debug.Log($"    → Frame creado");
            }
            else
            {
                // Asegurar que frames existentes tampoco bloqueen clics
                var fi = frameT.GetComponent<Image>();
                if (fi != null) fi.raycastTarget = false;
            }

            // Asegurar SpriteImage (sprite del enemigo + animador)
            Transform spriteT = go.transform.Find("SpriteImage");
            GameObject spriteGO;
            if (spriteT == null)
            {
                spriteGO = new GameObject("SpriteImage");
                spriteGO.transform.SetParent(go.transform, false);
                var srt = spriteGO.AddComponent<RectTransform>();
                srt.anchorMin        = srt.anchorMax = srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = new Vector2(0f, 10f);
                srt.sizeDelta        = new Vector2(sizeW - 20f, sizeH - 30f);
                Debug.Log($"    → SpriteImage creado");
            }
            else
            {
                spriteGO = spriteT.gameObject;
            }

            var spriteImg  = spriteGO.GetComponent<Image>()
                          ?? spriteGO.AddComponent<Image>();
            spriteImg.color          = Color.white;
            spriteImg.preserveAspect = true;
            spriteImg.raycastTarget  = true;  // debe recibir clics para propagar al padre

            var spriteAnim = spriteGO.GetComponent<EnemyUISpriteAnimator>()
                          ?? spriteGO.AddComponent<EnemyUISpriteAnimator>();

            // ── SelectionHighlight (overlay de selección) ──────────────────────
            // Imagen semi-transparente sobre el sprite que indica que este enemigo
            // está seleccionado como objetivo. Empieza desactivada.
            Transform selT = go.transform.Find("SelectionHighlight");
            Image selImg;
            if (selT == null)
            {
                var selGO = new GameObject("SelectionHighlight");
                selGO.transform.SetParent(go.transform, false);
                IC_Stretch(selGO.AddComponent<RectTransform>());
                selImg = selGO.AddComponent<Image>();
                selImg.color         = new Color(1f, 0.85f, 0.1f, 0.22f); // amarillo-oro suave
                selImg.raycastTarget = false;   // no debe interceptar clics
                selGO.SetActive(false);
                Debug.Log($"    → SelectionHighlight creado");
            }
            else
            {
                selImg = selT.GetComponent<Image>();
                if (selImg != null) selImg.raycastTarget = false;
                Debug.Log($"    → SelectionHighlight ya existe — se reutiliza");
            }

            // ── Asegurar y cablear EnemyActionDisplay ──────────────────────────
            var disp = go.GetComponent<EnemyActionDisplay>()
                    ?? go.AddComponent<EnemyActionDisplay>();

            // Usar SerializedObject para TODOS los campos serializados.
            // La asignación directa (disp.field = value) no siempre persiste en la escena
            // porque ApplyModifiedProperties() puede revertirla si el snapshot
            // fue tomado antes de la asignación directa.
            var dispSO = new SerializedObject(disp);
            dispSO.FindProperty("displayImage").objectReferenceValue        = spriteImg;
            dispSO.FindProperty("spriteAnimator").objectReferenceValue      = spriteAnim;
            dispSO.FindProperty("enemyIndex").intValue                      = index;
            dispSO.FindProperty("trackedEnemyName").stringValue             = "";
            dispSO.FindProperty("selectionHighlight").objectReferenceValue  = selImg;
            dispSO.ApplyModifiedProperties();

            go.SetActive(true);
            return disp;
        }

        // Posiciones iniciales (solo se aplican si el display no existía antes):
        //   Display_0 → izquierda   Display_1 → derecha
        var disp0 = EnsureDisplay(enemiesAreaGO, "EnemyDisplay_0",
            index: 0, posX: -190f, posY: 10f, sizeW: 240f, sizeH: 380f);
        var disp1 = EnsureDisplay(enemiesAreaGO, "EnemyDisplay_1",
            index: 1, posX:  190f, posY: 10f, sizeW: 240f, sizeH: 380f);

        // ── 3: Añadir/obtener EnemyDisplayManager en _CombatController ─────────
        GameObject controllerGO = GameObject.Find("_CombatController");
        if (controllerGO == null)
        {
            Debug.LogError("'_CombatController' no encontrado. Ejecuta el menú 5 primero.");
            return;
        }

        var manager = controllerGO.GetComponent<EnemyDisplayManager>()
                   ?? controllerGO.AddComponent<EnemyDisplayManager>();

        // Cablear displays[] con los dos displays permanentes
        var mgrSO = new SerializedObject(manager);
        var displaysProp = mgrSO.FindProperty("displays");
        displaysProp.arraySize = 2;
        displaysProp.GetArrayElementAtIndex(0).objectReferenceValue = disp0;
        displaysProp.GetArrayElementAtIndex(1).objectReferenceValue = disp1;
        mgrSO.ApplyModifiedProperties();

        Debug.Log($"✓ EnemyDisplayManager cableado en '{controllerGO.name}' " +
                  $"con {2} display(s) permanente(s).");

        // ── 4: Guardar escena ─────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(
            "✅ EnemyDisplayManager configurado con displays permanentes.\n\n" +
            "POSICIONAMIENTO:\n" +
            "  · Selecciona 'EnemyDisplay_0' en la Jerarquía → muévelo en la Scene View.\n" +
            "  · Selecciona 'EnemyDisplay_1' → muévelo donde quieras.\n" +
            "  · El código NO modifica posición, escala ni tamaño en runtime.\n\n" +
            "PASOS SIGUIENTES:\n" +
            "1. Posiciona los displays manualmente (arriba).\n" +
            "2. Asigna sprites en los EnemyData ScriptableObjects:\n" +
            "   Assets/ScriptableObjects/Data/Enemy_Conejo.asset → Sprite\n" +
            "   Assets/ScriptableObjects/Data/Enemy_Jabali.asset → Sprite\n" +
            "   Assets/ScriptableObjects/Data/Enemy_Hiena.asset  → Sprite\n" +
            "3. Presiona Play y entra en combate.\n" +
            "4. Boss fight: solo aparece EnemyDisplay_0 (la hiena). Display_1 se oculta."
        );
    }

    // ── Menú 12 ───────────────────────────────────────────────────────────────

    [MenuItem("Kimera/12 - Setup CombatHitFeedback y PlayerCombatAnimator")]
    public static void SetupFeedbackAndPlayerAnimator()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // ── 1: InSceneCombatController ────────────────────────────────────────
        var controller = UnityEngine.Object.FindFirstObjectByType<InSceneCombatController>();
        if (controller == null)
        {
            Debug.LogError("[Kimera/12] InSceneCombatController no encontrado. Ejecuta el menú 5 primero.");
            return;
        }
        var controllerSO = new SerializedObject(controller);

        // ── 2: Canvas de combate ──────────────────────────────────────────────
        Canvas combatCanvas = controllerSO.FindProperty("combatCanvas")?.objectReferenceValue as Canvas;
        if (combatCanvas == null)
            combatCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (combatCanvas == null)
        {
            Debug.LogError("[Kimera/12] Canvas de combate no encontrado.");
            return;
        }
        Debug.Log($"[Kimera/12] Canvas: '{combatCanvas.gameObject.name}'");

        // ── 3: Asegurar GraphicRaycaster en el Canvas (necesario para clics en UI) ──
        if (combatCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
        {
            combatCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Debug.Log($"  ✓ GraphicRaycaster añadido a '{combatCanvas.gameObject.name}'");
        }

        // ── 4: CombatHitFeedback en el Canvas ────────────────────────────────
        var hitFeedback = combatCanvas.GetComponent<CombatHitFeedback>()
                       ?? combatCanvas.gameObject.AddComponent<CombatHitFeedback>();
        Debug.Log($"  ✓ CombatHitFeedback en '{combatCanvas.gameObject.name}'");
        var feedbackSO = new SerializedObject(hitFeedback);

        // ── 5: DamageOverlay ─────────────────────────────────────────────────
        Transform overlayT = combatCanvas.transform.Find("DamageOverlay");
        GameObject overlayGO;
        if (overlayT == null)
        {
            overlayGO = new GameObject("DamageOverlay");
            overlayGO.transform.SetParent(combatCanvas.transform, false);
            IC_Stretch(overlayGO.AddComponent<RectTransform>());
            var oi = overlayGO.AddComponent<Image>();
            oi.color         = new Color(1f, 0f, 0f, 0f);
            oi.raycastTarget = false;
            overlayGO.SetActive(false);
            Debug.Log("  ✓ DamageOverlay creado");
        }
        else
        {
            overlayGO = overlayT.gameObject;
            Debug.Log("  ✓ DamageOverlay ya existe — se reutiliza");
        }
        feedbackSO.FindProperty("screenDamageOverlay").objectReferenceValue =
            overlayGO.GetComponent<Image>();

        // ── 6: Cablear cámara de combate en CombatHitFeedback ────────────────
        Camera combatCam = controllerSO.FindProperty("combatCamera")?.objectReferenceValue as Camera;
        if (combatCam != null)
        {
            feedbackSO.FindProperty("combatCamera").objectReferenceValue = combatCam;
            Debug.Log($"  ✓ combatCamera: '{combatCam.gameObject.name}'");
        }
        else
        {
            Debug.LogWarning("  ⚠️ combatCamera no encontrada. Asígnala en CombatHitFeedback.");
        }
        feedbackSO.ApplyModifiedProperties();

        // ── 7: PlayerCombatAnimator ───────────────────────────────────────────
        // Busca el jugador por tag "Player"; si no, por thirdPersonMovement.
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO == null)
        {
            var mv = UnityEngine.Object.FindFirstObjectByType<thirdPersonMovement>();
            if (mv != null) playerGO = mv.gameObject;
        }

        if (playerGO == null)
        {
            Debug.LogWarning("[Kimera/12] ⚠️ Jugador no encontrado (tag 'Player' ni thirdPersonMovement). " +
                             "Añade PlayerCombatAnimator manualmente y cabléalo.");
        }
        else
        {
            var pca   = playerGO.GetComponent<PlayerCombatAnimator>()
                     ?? playerGO.AddComponent<PlayerCombatAnimator>();
            var pcaSO = new SerializedObject(pca);

            // ── Buscar sprite de exploración (Billboard) ──────────────────────
            // Busca un hijo cuyo nombre contenga "billboard" (sin importar mayúsculas).
            GameObject explorationGO = FindChildContaining(playerGO, "billboard");
            if (explorationGO != null)
            {
                pcaSO.FindProperty("explorationSpriteGO").objectReferenceValue = explorationGO;
                Debug.Log($"  ✓ explorationSpriteGO → '{explorationGO.name}'");
            }
            else
            {
                Debug.LogWarning("  ⚠️ No se encontró un hijo con 'Billboard' en el nombre. " +
                                 "Arrastra el GO de exploración al campo 'Exploration Sprite GO' " +
                                 "del componente PlayerCombatAnimator en el Inspector.");
            }

            // ── Buscar sprite de combate ──────────────────────────────────────
            // Busca un hijo cuyo nombre contenga "combate" o "combat" (sin mayúsculas).
            GameObject combatGO = FindChildContaining(playerGO, "combate")
                               ?? FindChildContaining(playerGO, "combat sprite")
                               ?? FindChildContaining(playerGO, "spritecombate");
            if (combatGO != null)
            {
                pcaSO.FindProperty("combatSpriteGO").objectReferenceValue = combatGO;

                // Garantizar que el sprite de combate empiece INACTIVO en la escena.
                // (Debe estar inactivo en exploración; PlayerCombatAnimator lo activa al entrar en combate.)
                if (combatGO.activeSelf)
                {
                    combatGO.SetActive(false);
                    Debug.Log($"  ✓ combatSpriteGO → '{combatGO.name}'  (desactivado para estado inicial correcto)");
                }
                else
                {
                    Debug.Log($"  ✓ combatSpriteGO → '{combatGO.name}'  (ya estaba inactivo)");
                }
            }
            else
            {
                Debug.LogWarning("  ⚠️ No se encontró un hijo con 'combate' en el nombre. " +
                                 "Arrastra el GO de combate al campo 'Combat Sprite GO' " +
                                 "del componente PlayerCombatAnimator en el Inspector, " +
                                 "y asegúrate de que está INACTIVO en la escena.");
            }

            // ── Alpha_2D_Character_In_3D_World ────────────────────────────────
            var dir = playerGO.GetComponent<Alpha_2D_Character_In_3D_World>()
                   ?? playerGO.GetComponentInChildren<Alpha_2D_Character_In_3D_World>(true);
            if (dir != null)
            {
                pcaSO.FindProperty("directionScript").objectReferenceValue = dir;
                Debug.Log($"  ✓ directionScript → '{dir.gameObject.name}'");
            }

            pcaSO.ApplyModifiedProperties();

            // ── Cablear en InSceneCombatController ────────────────────────────
            controllerSO.FindProperty("playerCombatAnimator").objectReferenceValue = pca;
            controllerSO.ApplyModifiedProperties();
            Debug.Log($"  ✓ PlayerCombatAnimator cableado en InSceneCombatController");
        }

        // ── 8: Guardar ────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(
            "✅ Kimera/12 completado.\n\n" +
            "VERIFICA EN EL INSPECTOR (Player → PlayerCombatAnimator):\n" +
            "  · Combat Sprite GO      → el hijo 'sprite de combate'  (debe estar INACTIVO)\n" +
            "  · Exploration Sprite GO → el hijo 'Billboard'          (debe estar ACTIVO)\n\n" +
            "Si algún campo sigue vacío, arrástralo tú mismo desde la Jerarquía.\n" +
            "Luego presiona Play y entra en combate para probar el swap."
        );
    }

    // ── Helper: busca en todos los hijos (activos e inactivos) el primero cuyo
    //    nombre contenga 'keyword' (comparación sin mayúsculas/minúsculas). ─────
    private static GameObject FindChildContaining(GameObject root, string keyword)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.gameObject == root) continue;   // excluir la raíz
            if (t.name.ToLowerInvariant().Contains(keyword.ToLowerInvariant()))
                return t.gameObject;
        }
        return null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 13 — Setup PostCombatArtwork (imágenes en la Jerarquía del Canvas)
    // ══════════════════════════════════════════════════════════════════════════
    // Crea el GO "PostCombatArtwork" como hijo de _CombatCanvas con 6 Image hijos:
    //   · LevelUpArtwork       — pantalla de nivel subido
    //   · MealArtwork          — pantalla de comida pre-boss
    //   · DefeatArtwork        — pantalla de derrota normal
    //   · BossWinArtwork       — pantalla de victoria del boss
    //   · BossLoseArtwork      — pantalla de derrota del boss
    //   · BossCharacterSprite  — sprite del enemigo Hiena en el boss fight
    //
    // CÓMO USAR:
    //   1. Ejecuta este menú UNA VEZ con la escena Juego abierta.
    //   2. En la Jerarquía: expande _CombatCanvas → PostCombatArtwork.
    //   3. Selecciona un GO (ej. LevelUpArtwork) y en su Inspector
    //      arrastra tu sprite al campo "Source Image" del componente Image.
    //   4. PostCombatFlow leerá ese sprite automáticamente en runtime.
    [MenuItem("Kimera/13 - Setup PostCombatArtwork (sprites de post-combate en Canvas)")]
    public static void SetupPostCombatArtwork()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // ── 1: Localizar el canvas de combate ─────────────────────────────────
        Canvas combatCanvas = null;
        var controller = UnityEngine.Object.FindFirstObjectByType<InSceneCombatController>();
        if (controller != null)
        {
            var ctrlSO = new SerializedObject(controller);
            combatCanvas = ctrlSO.FindProperty("combatCanvas")?.objectReferenceValue as Canvas;
        }
        if (combatCanvas == null)
            combatCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (combatCanvas == null)
        {
            Debug.LogError("[Kimera/13] Canvas de combate no encontrado. Ejecuta el menú 5 primero.");
            return;
        }
        Debug.Log($"[Kimera/13] Canvas: '{combatCanvas.gameObject.name}'");

        // ── 2: Crear o reutilizar el GO contenedor ────────────────────────────
        // IMPORTANTE: no usar el operador ?? con Unity objects (el "fake null" de Unity
        // hace que ?? no funcione correctamente). Se usan comprobaciones explícitas con if.
        Transform existingRoot = combatCanvas.transform.Find("PostCombatArtwork");
        GameObject artRoot;
        RectTransform artRootRT;

        if (existingRoot != null)
        {
            artRoot = existingRoot.gameObject;
            artRootRT = artRoot.GetComponent<RectTransform>();
            if (artRootRT == null)
                artRootRT = artRoot.AddComponent<RectTransform>();
        }
        else
        {
            artRoot   = new GameObject("PostCombatArtwork");
            artRoot.transform.SetParent(combatCanvas.transform, false);
            artRootRT = artRoot.AddComponent<RectTransform>();  // directo, sin ??
        }
        IC_Stretch(artRootRT);

        // Contenedor invisible — no se muestra en runtime; es solo un "cajón" para los sprites.
        CanvasGroup cg = artRoot.GetComponent<CanvasGroup>();
        if (cg == null) cg = artRoot.AddComponent<CanvasGroup>();
        cg.alpha          = 0f;   // invisible
        cg.interactable   = false;
        cg.blocksRaycasts = false;

        Debug.Log($"  ✓ PostCombatArtwork preparado (alpha=0, no bloquea clics)");

        // ── 3: Helper para crear/reutilizar cada Image hija ────────────────────
        static Image EnsureArtImage(GameObject parent, string childName, string tooltip)
        {
            Transform t = parent.transform.Find(childName);
            GameObject go = t != null ? t.gameObject : null;

            if (go == null)
            {
                go = new GameObject(childName);
                go.transform.SetParent(parent.transform, false);
                IC_Stretch(go.AddComponent<RectTransform>());
                var img = go.AddComponent<Image>();
                img.color          = Color.white;
                img.preserveAspect = true;
                img.raycastTarget  = false;
                Debug.Log($"  ✓ '{childName}' creado — arrastra tu sprite a su campo Source Image");
                return img;
            }
            else
            {
                // No usar ?? con Unity objects — usar if explícito
                Image img = go.GetComponent<Image>();
                if (img == null) img = go.AddComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget  = false;
                Debug.Log($"  ✓ '{childName}' ya existía — reutilizado");
                return img;
            }
        }

        Image imgLevelUp  = EnsureArtImage(artRoot, "LevelUpArtwork",      "Pantalla: Nivel Subido");
        Image imgMeal     = EnsureArtImage(artRoot, "MealArtwork",          "Pantalla: Comida pre-boss");
        Image imgDefeat   = EnsureArtImage(artRoot, "DefeatArtwork",        "Pantalla: Derrota normal");
        Image imgBossWin  = EnsureArtImage(artRoot, "BossWinArtwork",       "Pantalla: Victoria boss");
        Image imgBossLose = EnsureArtImage(artRoot, "BossLoseArtwork",      "Pantalla: Derrota boss");
        Image imgBossChar = EnsureArtImage(artRoot, "BossCharacterSprite",  "Sprite de la Hiena (EnemyData)");

        // ── 4: Cablear Image referencias en PostCombatFlow ────────────────────
        var pcf = UnityEngine.Object.FindFirstObjectByType<PostCombatFlow>();
        if (pcf == null)
        {
            Debug.LogWarning("[Kimera/13] PostCombatFlow no encontrado en la escena. " +
                             "Las referencias de artwork quedan en los GOs — cabléalas manualmente " +
                             "arrastrando cada Image al Inspector de PostCombatFlow cuando lo crees.");
        }
        else
        {
            var pcfSO = new SerializedObject(pcf);
            pcfSO.FindProperty("levelUpArtworkImage").objectReferenceValue  = imgLevelUp;
            pcfSO.FindProperty("mealArtworkImage").objectReferenceValue     = imgMeal;
            pcfSO.FindProperty("defeatArtworkImage").objectReferenceValue   = imgDefeat;
            pcfSO.FindProperty("bossWinArtworkImage").objectReferenceValue  = imgBossWin;
            pcfSO.FindProperty("bossLoseArtworkImage").objectReferenceValue = imgBossLose;
            pcfSO.FindProperty("bossCharacterImage").objectReferenceValue   = imgBossChar;
            pcfSO.ApplyModifiedProperties();
            Debug.Log("  ✓ PostCombatFlow cableado con las 6 Image referencias");
        }

        // ── 5: Guardar ────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(
            "✅ Kimera/13 completado.\n\n" +
            "CÓMO ASIGNAR TUS SPRITES:\n" +
            "  1. Expande en la Jerarquía:  _CombatCanvas → PostCombatArtwork\n" +
            "  2. Haz clic en el GO que quieres cambiar, por ejemplo 'LevelUpArtwork'.\n" +
            "  3. En el Inspector, arrastra tu sprite al campo 'Source Image'.\n" +
            "  4. Repite para cada pantalla.\n\n" +
            "GOs disponibles:\n" +
            "  · LevelUpArtwork     → aparece en la pantalla ¡Nivel Subido!\n" +
            "  · MealArtwork        → aparece en la pantalla de comida pre-boss\n" +
            "  · DefeatArtwork      → aparece en la pantalla de derrota normal\n" +
            "  · BossWinArtwork     → aparece cuando ganas al boss\n" +
            "  · BossLoseArtwork    → aparece cuando pierdes contra el boss\n" +
            "  · BossCharacterSprite → sprite de la Hiena en el boss fight\n\n" +
            "El contenedor PostCombatArtwork tiene alpha=0 → invisible en pantalla.\n" +
            "Solo sus sprites se usan en las pantallas de post-combate."
        );
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 14 — Setup Shop Trigger + Shop UI + Enemy World Particles
    // ══════════════════════════════════════════════════════════════════════════
    // Ejecutar UNA VEZ con la escena Juego abierta.
    //
    // Este menú:
    //   1. Busca el GO "tienda" en la escena.
    //   2. Crea "_ShopCamera" — cámara NUEVA independiente frente a la tienda.
    //   3. Crea "_ShopCanvas → _ShopPanel" con 3 botones (Button + ShopItem).
    //   4. Crea "_TiendaTrigger" con BoxCollider isTrigger + ShopInteraction,
    //      cableando playerMovement, cameraController, cámaras y panel UI.
    //   5. Añade EnemyWorldParticles a todos los Enemy Visuals de la escena.
    [MenuItem("Kimera/14 - Setup Tienda Trigger y Partículas Enemigos")]
    public static void SetupShopAndParticles()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // ── 1: Buscar la tienda ───────────────────────────────────────────────
        GameObject tiendaGO = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            tiendaGO = FindGOByName(root, "tienda");
            if (tiendaGO != null) break;
        }

        if (tiendaGO == null)
            Debug.LogWarning("[Kimera/14] No se encontró un GO llamado 'tienda'. " +
                             "Crea el trigger manualmente o renombra el GO de la tienda.");
        else
        {
            Debug.Log($"[Kimera/14] Tienda: '{GetHierarchyPath(tiendaGO)}'");
            SetupShopTrigger(tiendaGO);
        }

        // ── 2: Partículas rojas en Enemy Visuals ──────────────────────────────
        int particleCount = 0;
        var combatTriggers = UnityEngine.Object.FindObjectsByType<InSceneCombatTrigger>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var ct in combatTriggers)
        {
            var so      = new SerializedObject(ct);
            var visProp = so.FindProperty("enemyVisuals");
            if (visProp == null || !visProp.isArray) continue;

            for (int i = 0; i < visProp.arraySize; i++)
            {
                var goRef = visProp.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (goRef == null) continue;

                if (goRef.GetComponent<EnemyWorldParticles>() == null)
                {
                    goRef.AddComponent<EnemyWorldParticles>();
                    particleCount++;
                    Debug.Log($"  ✓ EnemyWorldParticles → '{goRef.name}'");
                }
            }
        }

        if (combatTriggers.Length == 0)
            Debug.LogWarning("[Kimera/14] Sin InSceneCombatTrigger en la escena.");
        else
            Debug.Log(particleCount > 0
                ? $"[Kimera/14] ✓ EnemyWorldParticles añadido a {particleCount} enemigo(s)."
                : "[Kimera/14] Los Enemy Visuals ya tenían EnemyWorldParticles.");

        // ── 3: Guardar ────────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(
            "✅ Kimera/14 completado.\n\n" +
            "TIENDA — PASOS SIGUIENTES:\n" +
            "  1. Selecciona '_ShopCamera' en la Jerarquía.\n" +
            "     Muévela y rótala para encuadrar la fachada de la tienda.\n" +
            "  2. Selecciona '_TiendaTrigger' y ajusta su BoxCollider\n" +
            "     para cubrir el área de entrada.\n" +
            "  3. Expande en la Jerarquía:  _ShopCanvas → _ShopPanel\n" +
            "     Verás 3 botones: ShopItem_0, ShopItem_1, ShopItem_2.\n" +
            "     En el Inspector de cada uno edita el campo 'Item Name'.\n" +
            "  4. (Opcional) Asigna una Image negra full-screen al campo\n" +
            "     'Fade Image' de ShopInteraction para el corte suave.\n\n" +
            "FLUJO EN RUNTIME:\n" +
            "  Jugador entra al trigger → movimiento bloqueado → cámara swap\n" +
            "  → _ShopPanel aparece con 3 botones → jugador elige uno\n" +
            "  → GameProgress.CompleteShop(índice) → enemigos se activan\n" +
            "  → trigger desactivado PERMANENTEMENTE"
        );
    }

    // ── Helper: crea _ShopCamera, _ShopCanvas/_ShopPanel y _TiendaTrigger ────

    private static void SetupShopTrigger(GameObject tiendaGO)
    {
        // ── A: Cámara de tienda (_ShopCamera) ─────────────────────────────────
        GameObject shopCamGO = GameObject.Find("_ShopCamera");
        Camera shopCam;
        if (shopCamGO == null)
        {
            shopCamGO = new GameObject("_ShopCamera");
            Vector3 tiendaPos = tiendaGO.transform.position;
            shopCamGO.transform.position = tiendaPos + new Vector3(0f, 3f, 8f);
            shopCamGO.transform.rotation = Quaternion.Euler(15f, 180f, 0f);

            shopCam = shopCamGO.AddComponent<Camera>();
            shopCam.clearFlags      = CameraClearFlags.SolidColor;
            shopCam.backgroundColor = new Color(0.05f, 0.05f, 0.08f);
            shopCam.fieldOfView     = 55f;
            shopCam.nearClipPlane   = 0.1f;
            shopCam.farClipPlane    = 200f;
            shopCam.enabled         = false;   // empieza DESACTIVADA

            Debug.Log("  ✓ '_ShopCamera' creada (desactivada).");
            Debug.Log("  → MUÉVELA frente a la tienda desde la Scene View antes de probar.");
        }
        else
        {
            shopCam = shopCamGO.GetComponent<Camera>();
            if (shopCam == null) shopCam = shopCamGO.AddComponent<Camera>();
            shopCam.enabled = false;
            Debug.Log("  · '_ShopCamera' ya existe — se reutiliza (enabled=false).");
        }

        // ── B: Canvas y Panel de la tienda (_ShopCanvas → _ShopPanel) ─────────
        // El canvas va en la raíz de la escena (Screen Space Overlay).
        // El panel empieza INACTIVO; ShopInteraction lo activa al entrar.
        GameObject shopCanvasGO = GameObject.Find("_ShopCanvas");
        if (shopCanvasGO == null)
        {
            shopCanvasGO = new GameObject("_ShopCanvas");
            var canv = shopCanvasGO.AddComponent<Canvas>();
            canv.renderMode   = RenderMode.ScreenSpaceOverlay;
            canv.sortingOrder = 50;
            var scaler = shopCanvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution  = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight   = 0.5f;
            shopCanvasGO.AddComponent<GraphicRaycaster>();
            Debug.Log("  ✓ '_ShopCanvas' creado.");
        }
        else
        {
            Debug.Log("  · '_ShopCanvas' ya existe — se reutiliza.");
        }

        // Panel principal (fondo oscuro, empieza inactivo)
        Transform panelT = shopCanvasGO.transform.Find("_ShopPanel");
        GameObject shopPanelGO;
        if (panelT == null)
        {
            shopPanelGO = new GameObject("_ShopPanel");
            shopPanelGO.transform.SetParent(shopCanvasGO.transform, false);
            IC_Stretch(shopPanelGO.AddComponent<RectTransform>());
            shopPanelGO.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);
            shopPanelGO.SetActive(false);
            Debug.Log("  ✓ '_ShopPanel' creado (inactivo).");
        }
        else
        {
            shopPanelGO = panelT.gameObject;
            shopPanelGO.SetActive(false);
            Debug.Log("  · '_ShopPanel' ya existe — reutilizado (SetActive false).");
        }

        // Título (solo si no existe)
        if (shopPanelGO.transform.Find("Title") == null)
        {
            var titleGO = IC_TMP(shopPanelGO, "Title", "¿Qué te llevas?",
                36, Color.white, 0f, 220f, 900f, 60f);
            titleGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;
        }

        // Contenedor horizontal de los 3 botones
        Transform containerT = shopPanelGO.transform.Find("ItemsContainer");
        GameObject itemsContainer;
        if (containerT == null)
        {
            itemsContainer = new GameObject("ItemsContainer");
            itemsContainer.transform.SetParent(shopPanelGO.transform, false);
            var rt  = itemsContainer.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -20f);
            rt.sizeDelta        = new Vector2(900f, 220f);
            var hlg = itemsContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment       = TextAnchor.MiddleCenter;
            hlg.spacing              = 40f;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(20, 20, 0, 0);
            Debug.Log("  ✓ 'ItemsContainer' creado.");
        }
        else
        {
            itemsContainer = containerT.gameObject;
        }

        // Crear/reutilizar los 3 botones ShopItem
        var shopItems = new ShopItem[3];
        string[] defaultNames = { "Objeto 1", "Objeto 2", "Objeto 3" };

        for (int i = 0; i < 3; i++)
        {
            string  btnName = $"ShopItem_{i}";
            Transform btnT  = itemsContainer.transform.Find(btnName);
            GameObject btnGO;

            if (btnT == null)
            {
                btnGO = new GameObject(btnName);
                btnGO.transform.SetParent(itemsContainer.transform, false);
                var rt2 = btnGO.AddComponent<RectTransform>();
                rt2.sizeDelta = new Vector2(240f, 200f);

                // Fondo del botón
                btnGO.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.2f);

                // Button con colores de hover/press
                var btn = btnGO.AddComponent<Button>();
                var cb  = btn.colors;
                cb.normalColor      = new Color(0.15f, 0.15f, 0.2f);
                cb.highlightedColor = new Color(0.28f, 0.28f, 0.38f);
                cb.pressedColor     = new Color(0.08f, 0.08f, 0.12f);
                cb.selectedColor    = new Color(0.28f, 0.28f, 0.38f);
                btn.colors = cb;

                // Etiqueta de texto
                var labelGO = IC_TMP(btnGO, "Label", defaultNames[i],
                    20, Color.white, 0f, 0f, 220f, 60f);
                labelGO.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Center;

                // ShopItem
                var si   = btnGO.AddComponent<ShopItem>();
                var siSO = new SerializedObject(si);
                siSO.FindProperty("itemName").stringValue = defaultNames[i];
                siSO.FindProperty("itemIndex").intValue   = i;
                siSO.ApplyModifiedProperties();

                Debug.Log($"  ✓ '{btnName}' creado.");
            }
            else
            {
                btnGO = btnT.gameObject;
                Debug.Log($"  · '{btnName}' ya existe — se reutiliza.");
            }

            shopItems[i] = btnGO.GetComponent<ShopItem>();
        }

        // ── C: Trigger (_TiendaTrigger) con ShopInteraction ──────────────────
        Transform existingTrigger = tiendaGO.transform.Find("_TiendaTrigger");

        // Si hay una versión vieja con ShopCameraZone, eliminarla
        if (existingTrigger != null)
        {
            var oldZone = existingTrigger.GetComponent<ShopCameraZone>();
            if (oldZone != null)
            {
                UnityEngine.Object.DestroyImmediate(oldZone);
                Debug.Log("  ✓ ShopCameraZone (versión anterior) eliminado de '_TiendaTrigger'.");
            }

            // Si ya tiene ShopInteraction, solo actualizar referencias
            var existInteraction = existingTrigger.GetComponent<ShopInteraction>();
            if (existInteraction != null)
            {
                Debug.Log("  · '_TiendaTrigger' con ShopInteraction ya existe — actualizando referencias.");
                WireShopInteraction(existInteraction, shopPanelGO, shopItems, shopCam);
                return;
            }
        }

        GameObject triggerGO;
        if (existingTrigger != null)
        {
            triggerGO = existingTrigger.gameObject;
        }
        else
        {
            triggerGO = new GameObject("_TiendaTrigger");
            triggerGO.transform.SetParent(tiendaGO.transform);
            triggerGO.transform.localPosition = Vector3.zero;

            var box       = triggerGO.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center    = new Vector3(0f, 1.5f, 2f);
            box.size      = new Vector3(5f,  3f,  4f);
            Debug.Log("  ✓ '_TiendaTrigger' creado con BoxCollider isTrigger.");
        }

        var interaction = triggerGO.AddComponent<ShopInteraction>();
        WireShopInteraction(interaction, shopPanelGO, shopItems, shopCam);
        Debug.Log("  ✓ ShopInteraction añadido y cableado en '_TiendaTrigger'.");
    }

    // ── Helper: cablea todos los campos de ShopInteraction ────────────────────

    private static void WireShopInteraction(ShopInteraction interaction,
        GameObject shopPanel, ShopItem[] items, Camera shopCam)
    {
        var so = new SerializedObject(interaction);

        // shopPanel
        so.FindProperty("shopPanel").objectReferenceValue = shopPanel;

        // shopItems[]
        var itemsProp = so.FindProperty("shopItems");
        itemsProp.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++)
            itemsProp.GetArrayElementAtIndex(i).objectReferenceValue = items[i];

        // shopCamera
        so.FindProperty("shopCamera").objectReferenceValue = shopCam;

        // playerMovement
        var pm = UnityEngine.Object.FindFirstObjectByType<thirdPersonMovement>();
        if (pm != null)
        {
            so.FindProperty("playerMovement").objectReferenceValue = pm;
            Debug.Log($"  ✓ playerMovement → '{pm.gameObject.name}'");
        }
        else
            Debug.LogWarning("  ⚠️ thirdPersonMovement no encontrado. Asígnalo manualmente " +
                             "en '_TiendaTrigger → ShopInteraction → Player Movement'.");

        // cameraController
        var cc = UnityEngine.Object.FindFirstObjectByType<CameraController>();
        if (cc != null)
        {
            so.FindProperty("cameraController").objectReferenceValue = cc;
            Debug.Log($"  ✓ cameraController → '{cc.gameObject.name}'");
        }
        else
            Debug.LogWarning("  ⚠️ CameraController no encontrado. Asígnalo manualmente " +
                             "en '_TiendaTrigger → ShopInteraction → Camera Controller'.");

        // explorationCamera
        Camera explorationCam = Camera.main;
        if (explorationCam == null && cc != null)
            explorationCam = cc.GetComponent<Camera>();
        if (explorationCam != null)
        {
            so.FindProperty("explorationCamera").objectReferenceValue = explorationCam;
            Debug.Log($"  ✓ explorationCamera → '{explorationCam.gameObject.name}'");
        }
        else
            Debug.LogWarning("  ⚠️ explorationCamera no encontrada. Asígnala manualmente " +
                             "en '_TiendaTrigger → ShopInteraction → Exploration Camera'.");

        so.ApplyModifiedProperties();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 15 — Setup Post-Combat Screens (pantallas editables en la Jerarquía)
    // ══════════════════════════════════════════════════════════════════════════
    // Crea _PostCombatScreens como hijo de _CombatCanvas con 5 pantallas permanentes:
    //   · LevelUpScreen   — pantalla "¡Nivel subido!"
    //   · MealScreen      — pantalla de elección de comida pre-boss
    //   · DefeatScreen    — pantalla de derrota en combate normal
    //   · BossWinScreen   — pantalla de victoria del boss
    //   · BossLoseScreen  — pantalla de derrota del boss
    //
    // Cada pantalla tiene:
    //   · Image de fondo (cambiar sprite en Inspector)
    //   · Botones con Image + Button (cambiar sprite en Inspector)
    //   · Labels TMP (el texto se asigna en runtime)
    //   · ArtworkSlot (Image vacía — arrastra tu sprite aquí)
    //
    // Cablea automáticamente PostCombatFlow con las referencias de cada pantalla.
    // Una vez creadas, expande _CombatCanvas → _PostCombatScreens en la Jerarquía.
    [MenuItem("Kimera/15 - Setup Post-Combat Screens (editables en Jerarquía)")]
    public static void SetupPostCombatScreens()
    {
        var scene = EditorSceneManager.GetActiveScene();

        // ── 1: Localizar el canvas de combate ─────────────────────────────────
        Canvas combatCanvas = null;
        var controller = UnityEngine.Object.FindFirstObjectByType<InSceneCombatController>();
        if (controller != null)
        {
            var ctrlSO = new SerializedObject(controller);
            combatCanvas = ctrlSO.FindProperty("combatCanvas")?.objectReferenceValue as Canvas;
        }
        if (combatCanvas == null)
            combatCanvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
        if (combatCanvas == null)
        {
            Debug.LogError("[Kimera/15] Canvas de combate no encontrado. Ejecuta el menú 5 primero.");
            return;
        }
        Debug.Log($"[Kimera/15] Canvas: '{combatCanvas.gameObject.name}'");

        // ── 2: Contenedor raíz _PostCombatScreens ─────────────────────────────
        Transform existingRoot = combatCanvas.transform.Find("_PostCombatScreens");
        GameObject screensRoot;
        if (existingRoot != null)
        {
            screensRoot = existingRoot.gameObject;
            Debug.Log("  · '_PostCombatScreens' ya existe — se reutiliza.");
        }
        else
        {
            screensRoot = new GameObject("_PostCombatScreens");
            screensRoot.transform.SetParent(combatCanvas.transform, false);
            IC_Stretch(screensRoot.AddComponent<RectTransform>());
            Debug.Log("  ✓ '_PostCombatScreens' creado.");
        }

        // ── 3: Helpers locales ────────────────────────────────────────────────

        // Crea un GO con RectTransform con anchorMin/Max. Devuelve el GO.
        static GameObject PCF_Rect(GameObject parent, string name,
            float x0, float y0, float x1, float y1)
        {
            Transform t = parent.transform.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, y0);
            rt.anchorMax = new Vector2(x1, y1);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        // Raíz de pantalla: Image de fondo + CanvasGroup para fade, empieza inactiva.
        static GameObject PCF_ScreenRoot(GameObject parent, string name, Color bgColor)
        {
            Transform t = parent.transform.Find(name);
            GameObject go = t != null ? t.gameObject : new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            IC_Stretch(go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>());

            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = bgColor;

            var cg = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            cg.alpha          = 1f;
            cg.interactable   = true;
            cg.blocksRaycasts = true;

            go.SetActive(false);
            return go;
        }

        // Image (slot de artwork o decoración). Empieza sin sprite.
        static Image PCF_ImageSlot(GameObject parent, string name,
            float x0, float y0, float x1, float y1)
        {
            var go  = PCF_Rect(parent, name, x0, y0, x1, y1);
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color          = Color.white;
            img.preserveAspect = true;
            img.raycastTarget  = false;
            return img;
        }

        // Botón con fondo de color y etiqueta TMP. Devuelve el Button.
        static Button PCF_Button(GameObject parent, string name,
            float x0, float y0, float x1, float y1,
            Color bgColor, string labelText, int fontSize)
        {
            var go  = PCF_Rect(parent, name, x0, y0, x1, y1);
            var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();

            // Etiqueta (texto se puede cambiar desde el Inspector → componente TMP)
            string lblName = name + "Label";
            Transform lblT = go.transform.Find(lblName);
            GameObject lblGO = lblT != null ? lblT.gameObject : new GameObject(lblName);
            lblGO.transform.SetParent(go.transform, false);
            IC_Stretch(lblGO.GetComponent<RectTransform>() ?? lblGO.AddComponent<RectTransform>());
            var tmp = lblGO.GetComponent<TextMeshProUGUI>() ?? lblGO.AddComponent<TextMeshProUGUI>();
            tmp.text              = labelText;
            tmp.fontSize          = fontSize;
            tmp.fontStyle         = FontStyles.Bold;
            tmp.alignment         = TextAlignmentOptions.Center;
            tmp.color             = Color.white;
            tmp.enableWordWrapping = false;

            return btn;
        }

        // Label TMP (solo texto, sin fondo).
        static TextMeshProUGUI PCF_Label(GameObject parent, string name,
            float x0, float y0, float x1, float y1,
            string defaultText, int fontSize, Color color)
        {
            var go  = PCF_Rect(parent, name, x0, y0, x1, y1);
            var tmp = go.GetComponent<TextMeshProUGUI>() ?? go.AddComponent<TextMeshProUGUI>();
            tmp.text              = defaultText;
            tmp.fontSize          = fontSize;
            tmp.alignment         = TextAlignmentOptions.Center;
            tmp.color             = color;
            tmp.enableWordWrapping = true;
            return tmp;
        }

        // ── 4: LevelUpScreen ─────────────────────────────────────────────────
        var luRoot      = PCF_ScreenRoot(screensRoot, "LevelUpScreen", new Color(0f, 0f, 0.04f, 0.96f));
        var luArtwork   = PCF_ImageSlot (luRoot, "ArtworkSlot",      0.25f, 0.55f, 0.75f, 0.92f);
        var luTitle     = PCF_Label     (luRoot, "TitleLabel",
            0f, 0.50f, 1f, 0.62f,
            "¡ NIVEL SUBIDO !", 34, new Color(1f, 0.9f, 0.1f));
        luTitle.fontStyle = FontStyles.Bold;
        var luStats     = PCF_Label     (luRoot, "StatsLabel",
            0f, 0.41f, 1f, 0.51f,
            "+HP máx   +ATK   +Energía máx", 18, Color.white);
        var luFlavor    = PCF_Label     (luRoot, "FlavorLabel",
            0.05f, 0.29f, 0.95f, 0.42f,
            "Mike siente el poder del Kime fluir en sus venas.", 14, new Color(0.85f, 0.85f, 0.85f));
        var luContinue  = PCF_Button    (luRoot, "ContinueButton",
            0.25f, 0.05f, 0.75f, 0.18f,
            new Color(0.15f, 0.45f, 0.15f), "Continuar  →", 17);
        Debug.Log("  ✓ LevelUpScreen creada.");

        // ── 5: MealScreen ─────────────────────────────────────────────────────
        var msRoot    = PCF_ScreenRoot(screensRoot, "MealScreen", new Color(0.04f, 0.02f, 0f, 0.97f));
        var msArtwork = PCF_ImageSlot (msRoot, "ArtworkSlot", 0.58f, 0.22f, 1.00f, 0.98f);
        var msHeader  = PCF_Label     (msRoot, "HeaderLabel",
            0f, 0.84f, 0.56f, 0.92f,
            "— La Guía —", 16, new Color(0.85f, 0.75f, 0.55f));
        msHeader.fontStyle = FontStyles.Italic;
        var msDialog  = PCF_Label     (msRoot, "DialogLabel",
            0.06f, 0.66f, 0.56f, 0.85f,
            "\"Toma, come algo. Necesitarás fuerzas.\"", 15, new Color(0.95f, 0.90f, 0.80f));
        var msSection = PCF_Label     (msRoot, "SectionLabel",
            0f, 0.60f, 0.56f, 0.67f,
            "── Elige qué comer ──", 13, new Color(0.65f, 0.65f, 0.55f));
        var msComment = PCF_Label     (msRoot, "CommentLabel",
            0.04f, 0.10f, 0.54f, 0.22f,
            "", 13, new Color(0.8f, 0.75f, 0.6f));
        msComment.fontStyle = FontStyles.Italic;

        // 3 botones de comida
        string[] mealNames    = { "Carne Asada", "Fruta Kime", "Ración de Campo" };
        float[]  mealBtnYS    = { 0.50f, 0.34f, 0.18f };
        float[]  mealBtnYE    = { 0.62f, 0.48f, 0.32f };
        Color    mealBtnColor = new Color(0.18f, 0.12f, 0.06f, 0.95f);

        var msBtns      = new Button[3];
        var msNameTMPs  = new TextMeshProUGUI[3];
        var msDescTMPs  = new TextMeshProUGUI[3];

        for (int i = 0; i < 3; i++)
        {
            var mbGO = PCF_Rect(msRoot, $"MealButton_{i}", 0.04f, mealBtnYS[i], 0.54f, mealBtnYE[i]);
            var mbImg = mbGO.GetComponent<Image>() ?? mbGO.AddComponent<Image>();
            mbImg.color = mealBtnColor;
            msBtns[i] = mbGO.GetComponent<Button>() ?? mbGO.AddComponent<Button>();

            // Nombre de la comida (fila superior)
            var nameGO  = PCF_Rect(mbGO, "MealNameLabel", 0f, 0.50f, 1f, 1f);
            var nameTMP = nameGO.GetComponent<TextMeshProUGUI>() ?? nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text              = $"<b>{mealNames[i]}</b>";
            nameTMP.fontSize          = 16;
            nameTMP.alignment         = TextAlignmentOptions.Center;
            nameTMP.color             = new Color(1f, 0.88f, 0.6f);
            nameTMP.enableWordWrapping = false;
            msNameTMPs[i] = nameTMP;

            // Descripción breve (fila inferior)
            var descGO  = PCF_Rect(mbGO, "MealDescLabel", 0f, 0f, 1f, 0.52f);
            var descTMP = descGO.GetComponent<TextMeshProUGUI>() ?? descGO.AddComponent<TextMeshProUGUI>();
            descTMP.text              = "(descripción)";
            descTMP.fontSize          = 12;
            descTMP.alignment         = TextAlignmentOptions.Center;
            descTMP.color             = new Color(0.7f, 0.9f, 0.7f);
            descTMP.enableWordWrapping = false;
            msDescTMPs[i] = descTMP;
        }
        Debug.Log("  ✓ MealScreen creada.");

        // ── 6: DefeatScreen ───────────────────────────────────────────────────
        var dsRoot    = PCF_ScreenRoot(screensRoot, "DefeatScreen", new Color(0.07f, 0f, 0f, 0.95f));
        var dsTitle   = PCF_Label     (dsRoot, "TitleLabel",
            0f, 0.68f, 1f, 0.82f,
            "Caíste en combate.", 32, new Color(1f, 0.3f, 0.2f));
        dsTitle.fontStyle = FontStyles.Bold;
        var dsArtwork = PCF_ImageSlot (dsRoot, "ArtworkSlot", 0.12f, 0.37f, 0.88f, 0.68f);
        var dsRetry   = PCF_Button    (dsRoot, "RetryButton",
            0.08f, 0.22f, 0.92f, 0.37f,
            new Color(0.15f, 0.35f, 0.55f), "Reintentar combate", 17);
        var dsHome    = PCF_Button    (dsRoot, "HomeButton",
            0.25f, 0.05f, 0.75f, 0.18f,
            new Color(0.22f, 0.22f, 0.22f), "Ir al inicio", 17);
        Debug.Log("  ✓ DefeatScreen creada.");

        // ── 7: BossWinScreen ──────────────────────────────────────────────────
        var bwRoot      = PCF_ScreenRoot(screensRoot, "BossWinScreen",  new Color(0.01f, 0.04f, 0.01f, 0.97f));
        var bwTitle     = PCF_Label     (bwRoot, "TitleLabel",
            0f, 0.72f, 1f, 0.88f,
            "Ganaste.", 46, new Color(0.85f, 1f, 0.55f));
        bwTitle.fontStyle = FontStyles.Bold;
        var bwArtwork   = PCF_ImageSlot (bwRoot, "ArtworkSlot", 0.05f, 0.28f, 0.95f, 0.72f);
        var bwStory     = PCF_Label     (bwRoot, "StoryLabel",
            0.06f, 0.30f, 0.94f, 0.72f,
            "Fue de suerte, y lo sabes.\nEl verdadero camino apenas comienza.",
            15, new Color(0.88f, 0.85f, 0.82f));
        var bwContinued = PCF_Label     (bwRoot, "ContinuedLabel",
            0f, 0.19f, 1f, 0.30f,
            "— C O N T I N U A R Á —", 22, new Color(1f, 0.88f, 0.4f, 0f));
        bwContinued.fontStyle = FontStyles.Bold;
        var bwHome      = PCF_Button    (bwRoot, "HomeButton",
            0.08f, 0.04f, 0.46f, 0.16f,
            new Color(0.15f, 0.30f, 0.15f), "Volver al inicio", 15);
        var bwQuit      = PCF_Button    (bwRoot, "QuitButton",
            0.54f, 0.04f, 0.92f, 0.16f,
            new Color(0.30f, 0.10f, 0.10f), "Salir", 15);
        Debug.Log("  ✓ BossWinScreen creada.");

        // ── 8: BossLoseScreen ─────────────────────────────────────────────────
        var blRoot      = PCF_ScreenRoot(screensRoot, "BossLoseScreen", new Color(0.06f, 0f, 0f, 0.97f));
        var blTitle     = PCF_Label     (blRoot, "TitleLabel",
            0f, 0.72f, 1f, 0.88f,
            "Perdiste.", 46, new Color(1f, 0.35f, 0.25f));
        blTitle.fontStyle = FontStyles.Bold;
        var blArtwork   = PCF_ImageSlot (blRoot, "ArtworkSlot", 0.05f, 0.28f, 0.95f, 0.72f);
        var blStory     = PCF_Label     (blRoot, "StoryLabel",
            0.06f, 0.30f, 0.94f, 0.72f,
            "Es normal que pierdas.\nEntrenarás. Aprenderás. Y cuando vuelvas, será diferente.",
            15, new Color(0.88f, 0.85f, 0.82f));
        var blContinued = PCF_Label     (blRoot, "ContinuedLabel",
            0f, 0.19f, 1f, 0.30f,
            "— C O N T I N U A R Á —", 22, new Color(1f, 0.88f, 0.4f, 0f));
        blContinued.fontStyle = FontStyles.Bold;
        var blHome      = PCF_Button    (blRoot, "HomeButton",
            0.08f, 0.04f, 0.46f, 0.16f,
            new Color(0.15f, 0.30f, 0.15f), "Volver al inicio", 15);
        var blQuit      = PCF_Button    (blRoot, "QuitButton",
            0.54f, 0.04f, 0.92f, 0.16f,
            new Color(0.30f, 0.10f, 0.10f), "Salir", 15);
        Debug.Log("  ✓ BossLoseScreen creada.");

        // ── 9: Cablear PostCombatFlow ─────────────────────────────────────────
        var pcf = UnityEngine.Object.FindFirstObjectByType<PostCombatFlow>();
        if (pcf == null)
        {
            Debug.LogWarning("[Kimera/15] PostCombatFlow no encontrado en la escena. " +
                             "Las pantallas están creadas — cabléalas manualmente en PostCombatFlow.");
        }
        else
        {
            var pcfSO = new SerializedObject(pcf);

            // Helper: cablea una pantalla (PCF_LevelUpScreen / PCF_DefeatScreen / etc.)
            void WireScreen(string fieldName, GameObject root)
            {
                var prop = pcfSO.FindProperty(fieldName);
                if (prop == null) { Debug.LogWarning($"  Campo '{fieldName}' no encontrado en PostCombatFlow."); return; }
                prop.FindPropertyRelative("root").objectReferenceValue = root;
            }

            void WireRef(string screenField, string subField, UnityEngine.Object value)
            {
                var screen = pcfSO.FindProperty(screenField);
                if (screen == null) return;
                var sub = screen.FindPropertyRelative(subField);
                if (sub != null) sub.objectReferenceValue = value;
            }

            void WireArray(string screenField, string subField, int index, UnityEngine.Object value)
            {
                var screen = pcfSO.FindProperty(screenField);
                if (screen == null) return;
                var arr = screen.FindPropertyRelative(subField);
                if (arr == null) return;
                if (arr.arraySize <= index) arr.arraySize = index + 1;
                arr.GetArrayElementAtIndex(index).objectReferenceValue = value;
            }

            // LevelUpScreen
            WireScreen("levelUpScreenRefs", luRoot);
            WireRef("levelUpScreenRefs", "artworkSlot",   luArtwork);
            WireRef("levelUpScreenRefs", "titleLabel",    luTitle);
            WireRef("levelUpScreenRefs", "statsLabel",    luStats);
            WireRef("levelUpScreenRefs", "flavorLabel",   luFlavor);
            WireRef("levelUpScreenRefs", "continueButton", luContinue);

            // MealScreen
            WireScreen("mealScreenRefs", msRoot);
            WireRef("mealScreenRefs", "artworkSlot",  msArtwork);
            WireRef("mealScreenRefs", "headerLabel",  msHeader);
            WireRef("mealScreenRefs", "dialogLabel",  msDialog);
            WireRef("mealScreenRefs", "sectionLabel", msSection);
            WireRef("mealScreenRefs", "commentLabel", msComment);
            for (int i = 0; i < 3; i++)
            {
                WireArray("mealScreenRefs", "mealButtons",    i, msBtns[i]);
                WireArray("mealScreenRefs", "mealNameLabels", i, msNameTMPs[i]);
                WireArray("mealScreenRefs", "mealDescLabels", i, msDescTMPs[i]);
            }

            // DefeatScreen
            WireScreen("defeatScreenRefs", dsRoot);
            WireRef("defeatScreenRefs", "artworkSlot",  dsArtwork);
            WireRef("defeatScreenRefs", "retryButton",  dsRetry);
            WireRef("defeatScreenRefs", "homeButton",   dsHome);

            // BossWinScreen
            WireScreen("bossWinScreenRefs", bwRoot);
            WireRef("bossWinScreenRefs", "artworkSlot",   bwArtwork);
            WireRef("bossWinScreenRefs", "titleLabel",    bwTitle);
            WireRef("bossWinScreenRefs", "storyLabel",    bwStory);
            WireRef("bossWinScreenRefs", "continuedLabel", bwContinued);
            WireRef("bossWinScreenRefs", "homeButton",    bwHome);
            WireRef("bossWinScreenRefs", "quitButton",    bwQuit);

            // BossLoseScreen
            WireScreen("bossLoseScreenRefs", blRoot);
            WireRef("bossLoseScreenRefs", "artworkSlot",   blArtwork);
            WireRef("bossLoseScreenRefs", "titleLabel",    blTitle);
            WireRef("bossLoseScreenRefs", "storyLabel",    blStory);
            WireRef("bossLoseScreenRefs", "continuedLabel", blContinued);
            WireRef("bossLoseScreenRefs", "homeButton",    blHome);
            WireRef("bossLoseScreenRefs", "quitButton",    blQuit);

            pcfSO.ApplyModifiedProperties();
            Debug.Log("  ✓ PostCombatFlow cableado con las 5 pantallas.");
        }

        // ── 10: Guardar ───────────────────────────────────────────────────────
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();

        Debug.Log(
            "✅ Kimera/15 completado.\n\n" +
            "CÓMO CAMBIAR SPRITES EN LA JERARQUÍA:\n" +
            "  1. Expande en la Jerarquía:  _CombatCanvas → _PostCombatScreens\n" +
            "  2. Expande la pantalla que quieres editar (ej. DefeatScreen).\n" +
            "  3. Selecciona el GO cuyo sprite quieres cambiar:\n" +
            "       · Fondo        → selecciona el GO raíz de la pantalla.\n" +
            "       · Botón        → selecciona 'RetryButton' o 'HomeButton', etc.\n" +
            "       · Artwork slot → selecciona 'ArtworkSlot'.\n" +
            "  4. En el Inspector cambia el campo 'Source Image' del componente Image.\n\n" +
            "PANTALLAS DISPONIBLES:\n" +
            "  · LevelUpScreen  — pantalla ¡Nivel subido!\n" +
            "  · MealScreen     — pantalla de comida pre-boss (3 botones MealButton_0/1/2)\n" +
            "  · DefeatScreen   — pantalla de derrota en combate normal\n" +
            "  · BossWinScreen  — pantalla de victoria del boss\n" +
            "  · BossLoseScreen — pantalla de derrota del boss\n\n" +
            "NOTA: Las pantallas empiezan INACTIVAS (SetActive false).\n" +
            "En PlayMode se activan automáticamente cuando corresponde."
        );
    }

    // ── Helper: busca un GO por nombre (sin importar mayúsculas) ─────────────

    private static GameObject FindGOByName(GameObject root, string name)
    {
        if (string.Compare(root.name, name, System.StringComparison.OrdinalIgnoreCase) == 0)
            return root;
        foreach (Transform child in root.transform)
        {
            var found = FindGOByName(child.gameObject, name);
            if (found != null) return found;
        }
        return null;
    }

    // ── Helper: devuelve el path jerárquico de un GO ───────────────────────────

    private static string GetHierarchyPath(GameObject go)
    {
        string path = go.name;
        var t = go.transform.parent;
        while (t != null) { path = t.name + "/" + path; t = t.parent; }
        return path;
    }

    // ── Helpers exclusivos de SetupInSceneCombat ──────────────────────────────

    private static GameObject IC_Child(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static GameObject IC_Img(GameObject parent, string name, Color color)
    {
        var go = IC_Child(parent, name);
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static GameObject IC_TMP(GameObject parent, string name, string text,
        int size, Color color, float x, float y, float w, float h)
    {
        var go  = IC_Child(parent, name);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = color;
        tmp.alignment = TextAlignmentOptions.Center;
        IC_RectAt(go, x, y, w, h);
        return go;
    }

    private static GameObject IC_Btn(GameObject parent, string name, string label,
        Color bg, float x, float y, float w, float h)
    {
        var go  = IC_Child(parent, name);
        IC_RectAt(go, x, y, w, h);
        go.AddComponent<Image>().color = bg;
        var btn = go.AddComponent<Button>();
        var cb  = btn.colors;
        cb.normalColor      = bg;
        cb.highlightedColor = bg + new Color(0.18f, 0.18f, 0.18f);
        cb.pressedColor     = bg - new Color(0.18f, 0.18f, 0.18f);
        btn.colors = cb;
        var lgo = IC_Child(go, "Label"); IC_Stretch(lgo);
        var t   = lgo.AddComponent<TextMeshProUGUI>();
        t.text      = label;
        t.fontSize  = 15;
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.color     = Color.white;
        return go;
    }

    private static void IC_RectAt(GameObject go, float x, float y, float w, float h)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
    }

    private static void IC_Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void IC_Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        IC_Stretch(rt);
    }

    private static void IC_Set(SerializedObject so, string field, UnityEngine.Object val)
    {
        var prop = so.FindProperty(field);
        if (prop == null) { Debug.LogWarning($"Campo serializado no encontrado: {field}"); return; }
        prop.objectReferenceValue = val;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Versión legacy — mantener por compatibilidad con el menú Fix 3
    private static void IC_WireDisplay(EnemyActionDisplay disp, Image img,
        string enemyName, Color idle)
    {
        var so = new SerializedObject(disp);
        so.FindProperty("displayImage").objectReferenceValue  = img;
        so.FindProperty("trackedEnemyName").stringValue       = enemyName;
        so.FindProperty("enemyIndex").intValue                = -1;   // usar nombre
        so.FindProperty("idleColor").colorValue   = idle;
        so.FindProperty("attackColor").colorValue = new Color(1f,    0.28f, 0.28f);
        so.FindProperty("dodgeColor").colorValue  = new Color(0.28f, 0.9f,  0.9f);
        so.FindProperty("defendColor").colorValue = new Color(0.28f, 0.5f,  1f);
        so.FindProperty("chargeColor").colorValue = new Color(1f,    0.78f, 0.18f);
        so.ApplyModifiedProperties();
    }

    // Versión por índice — recomendada para combate in-scene (soporta boss)
    private static void IC_WireDisplayByIndex(EnemyActionDisplay disp, Image img, int index,
                                              EnemyUISpriteAnimator anim = null)
    {
        var so = new SerializedObject(disp);
        so.FindProperty("displayImage").objectReferenceValue    = img;
        so.FindProperty("enemyIndex").intValue                  = index;
        so.FindProperty("trackedEnemyName").stringValue         = "";    // ignorado
        if (anim != null)
            so.FindProperty("spriteAnimator").objectReferenceValue = anim;
        so.FindProperty("idleColor").colorValue   = Color.white;
        so.FindProperty("attackColor").colorValue = new Color(1f,    0.28f, 0.28f);
        so.FindProperty("dodgeColor").colorValue  = new Color(0.28f, 0.9f,  0.9f);
        so.FindProperty("defendColor").colorValue = new Color(0.28f, 0.5f,  1f);
        so.FindProperty("chargeColor").colorValue = new Color(1f,    0.78f, 0.18f);
        so.ApplyModifiedProperties();
    }

    private static void IC_BuildEnemyHUD(GameObject parent, string name, float x,
        string enemyName, out Slider healthBar, out TextMeshProUGUI nameLabel,
        out GameObject weakIndicator, out GameObject selIndicator)
    {
        var hud = IC_Child(parent, name);
        IC_RectAt(hud, x, 0, 215, 74);
        IC_Stretch(IC_Img(hud, "BG", new Color(0f, 0f, 0f, 0.6f)));

        var lblGO = IC_TMP(hud, "NameLabel", enemyName, 12, Color.white, 0, 22, 200, 22);
        nameLabel = lblGO.GetComponent<TextMeshProUGUI>();

        var hpGO = IC_Child(hud, "HealthBar");
        IC_RectAt(hpGO, -5, -6, 190, 18);
        IC_Stretch(IC_Img(hpGO, "BG",   new Color(0.18f, 0.18f, 0.18f)));
        var fillGO = IC_Img(hpGO, "Fill", new Color(0.88f, 0.2f, 0.2f));
        IC_Stretch(fillGO);
        var sl = hpGO.AddComponent<Slider>();
        sl.fillRect   = fillGO.GetComponent<RectTransform>();
        sl.direction  = Slider.Direction.LeftToRight;
        sl.minValue   = 0; sl.maxValue = 100; sl.value = 100;
        sl.interactable = false;
        healthBar = sl;

        var wkGO = IC_Img(hud, "WeaknessIndicator", new Color(1f, 0.9f, 0.1f));
        IC_RectAt(wkGO, 92, 22, 22, 22);
        IC_TMP(wkGO, "!", "!", 13, Color.black, 0, 0, 22, 22);
        wkGO.SetActive(false);
        weakIndicator = wkGO;

        var selGO = IC_Img(hud, "SelectionHighlight", new Color(1f, 1f, 0f, 0.25f));
        IC_RectAt(selGO, 0, 0, 215, 74);
        selGO.SetActive(false);
        selIndicator = selGO;
    }

    private static GameObject IC_BuildStatRow(GameObject parent, string label,
        string sliderName, float x, float y, Color fillColor, out GameObject textGO)
    {
        IC_TMP(parent, label + "Lbl", label, 15, Color.white, x - 245, y, 42, 28);
        var sliderGO = IC_Child(parent, sliderName);
        IC_RectAt(sliderGO, x + 20, y, 420, 26);
        IC_Stretch(IC_Img(sliderGO, "BG",   new Color(0.18f, 0.18f, 0.18f)));
        var fillGO = IC_Img(sliderGO, "Fill", fillColor);
        IC_Stretch(fillGO);
        var sl = sliderGO.AddComponent<Slider>();
        sl.fillRect   = fillGO.GetComponent<RectTransform>();
        sl.direction  = Slider.Direction.LeftToRight;
        sl.minValue   = 0; sl.maxValue = 100; sl.value = 100;
        sl.interactable = false;
        textGO = IC_TMP(parent, label + "Text", "100 / 100", 13, Color.white, x + 280, y, 110, 26);
        return sliderGO;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static GameObject MakeText(
        GameObject parent, string name,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax,
        string text, int fontSize, FontStyles style)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.fontStyle = style;
        return go;
    }
}
