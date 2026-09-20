using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

/// <summary>
/// Orquesta el arranque de la partida:
///   (opcional) cutscene de Timeline de inicio -> devuelve cámara y control
///   al jugador EXACTAMENTE donde estaba antes de reproducirla (jugador Y
///   TODA su jerarquía visual, sin importar qué objeto puntual haya tocado
///   el Timeline) -> bienvenida -> arranca la quest "Explora la ciudad".
///
/// "Explora la ciudad" YA NO se completa sola por tiempo: se completa cuando
/// el jugador termina el ÚLTIMO punto del recorrido (ver su OneShotTrigger,
/// campo 'Completes Quest'). "Encuentra a tu guía" arranca sola gracias a
/// exploreCityQuest.autoStartNextQuest.
///
/// Todo esto pasa UNA sola vez (StoryFlags).
/// Poné este componente una sola vez en la escena (ej. junto a GameManager).
/// </summary>
public class IntroSequence : MonoBehaviour
{
    [Header("Cutscene de inicio (opcional)")]
    [Tooltip("Si la asignás, la secuencia espera a que termine ANTES de mostrar " +
             "la bienvenida y arrancar 'Explora la ciudad'. Dejala vacía si no " +
             "querés cutscene de inicio.")]
    public PlayableDirector introCutscene;

    [Header("Devolver control tras la cutscene")]
    [Tooltip("La cámara normal de juego (ej. Main Camera). Se reactiva apenas termina la cutscene.")]
    public Camera gameplayCamera;

    [Tooltip("La cámara que usa la cutscene (ej. _CinematicCamera). Se desactiva apenas termina.")]
    public Camera cinematicCamera;

    [Tooltip("El script de movimiento del player (ej. Third Person Movement).")]
    public MonoBehaviour playerMovement;

    [Tooltip("El CharacterController del player.")]
    public CharacterController playerController;

    [Tooltip("El Transform RAÍZ del player (el que tiene el CharacterController, " +
             "ej. 'Player'). Al arrancar el juego se guarda la pose LOCAL de " +
             "este objeto Y DE TODOS SUS HIJOS (PicoChan, pico_chan_chr_pico_00, " +
             "huesos, etc.) — sea cual sea el objeto exacto que el Timeline " +
             "toque durante la cutscene, TODOS vuelven a su pose original al " +
             "terminar. Ya no hace falta saber cuál en particular se desalinea.")]
    public Transform playerRoot;

    [Tooltip("El Animator de la malla del player (ej. dentro de 'PicoChan', " +
             "Controller 'Player3D'). Se apaga por un instante SOLO durante el " +
             "reset de pose (no durante la cutscene) para que no vuelva a " +
             "pisar la posición que acabamos de restaurar en el mismo frame.")]
    public Animator playerVisualAnimator;

    // Snapshot de TODA la jerarquía de Player, tomado en Awake — antes de que
    // absolutamente nada (Timeline incluido) haya tenido oportunidad de tocarla.
    private Transform[] _allTransforms;
    private Vector3[] _localPositions;
    private Quaternion[] _localRotations;

    [Header("Quest")]
    [Tooltip("Quest tipo Explore, ej. 'Explora la ciudad'. Se completa sola cuando " +
             "el jugador termina el último punto del recorrido (ver su OneShotTrigger, " +
             "campo 'Completes Quest'). Configurá 'Auto Start Next Quest' en este asset " +
             "apuntando a 'Encuentra a tu guía' para que se encadene sola.")]
    public QuestData exploreCityQuest;

    [Header("Tiempos")]
    public float delayBeforeExplorePrompt = 5f;

    [Header("Bienvenida (opcional)")]
    [TextArea] public string welcomeText = "Bienvenido a Kimera City!";

    private bool cutsceneFinished;

    private void Awake()
    {
        // Snapshot ANTES de todo — Awake corre antes que cualquier Start(),
        // incluido el de esta misma clase, así que es el punto más temprano
        // posible para capturar la pose "real" de reposo del player.
        if (playerRoot != null)
        {
            _allTransforms = playerRoot.GetComponentsInChildren<Transform>(true); // incluye playerRoot
            _localPositions = new Vector3[_allTransforms.Length];
            _localRotations = new Quaternion[_allTransforms.Length];

            for (int i = 0; i < _allTransforms.Length; i++)
            {
                _localPositions[i] = _allTransforms[i].localPosition;
                _localRotations[i] = _allTransforms[i].localRotation;
            }
        }
    }

    private void Start()
    {
        if (!StoryFlags.Instance.TryConsume("intro_sequence"))
            return; // ya se corrió esta secuencia antes (ej. volviste a cargar la escena)

        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (introCutscene != null)
        {
            yield return StartCoroutine(PlayIntroCutscene());
        }

        if (!string.IsNullOrEmpty(welcomeText))
            NotificationManager.Instance.ShowAchievement(welcomeText);

        yield return new WaitForSeconds(delayBeforeExplorePrompt);

        QuestManager.Instance.StartQuest(exploreCityQuest);

        // A partir de acá el jugador queda libre para hacer el recorrido. El
        // OneShotTrigger del último punto completa la quest solo, y si
        // configuraste autoStartNextQuest, "Encuentra a tu guía" arranca sola.
    }

    private IEnumerator PlayIntroCutscene()
    {
        if (playerMovement != null)
            playerMovement.enabled = false;

        if (playerController != null)
            playerController.enabled = false;

        if (gameplayCamera != null)
            gameplayCamera.gameObject.SetActive(false);

        if (cinematicCamera != null)
            cinematicCamera.gameObject.SetActive(true);

        // Forzamos Wrap Mode = None: por defecto muchos Timelines quedan en
        // 'Hold', que sigue empujando la ÚLTIMA pose indefinidamente incluso
        // después de "terminar". Con 'None', el Director suelta el control
        // por completo al llegar al final.
        introCutscene.extrapolationMode = DirectorWrapMode.None;

        cutsceneFinished = false;
        introCutscene.stopped += OnCutsceneStopped;
        introCutscene.Play();

        yield return new WaitUntil(() => cutsceneFinished);

        // Apagar el Director EN SÍ — un PlayableDirector desactivado no
        // evalúa su PlayableGraph para nada, así que garantiza que ya no
        // puede seguir escribiendo sobre ningún Transform.
        introCutscene.enabled = false;

        if (cinematicCamera != null)
            cinematicCamera.gameObject.SetActive(false);

        if (gameplayCamera != null)
            gameplayCamera.gameObject.SetActive(true);

        // Apagar el Animator JUSTO AHORA (no antes, la cutscene lo necesitaba)
        // para que no vuelva a pisar la pose que estamos por restaurar.
        if (playerVisualAnimator != null)
            playerVisualAnimator.enabled = false;

        // Restaurar TODA la jerarquía del player a su pose local original,
        // capturada en Awake — objeto por objeto, sin excepción.
        if (_allTransforms != null)
        {
            for (int i = 0; i < _allTransforms.Length; i++)
            {
                if (_allTransforms[i] == null) continue; // por si algo se destruyó
                _allTransforms[i].localPosition = _localPositions[i];
                _allTransforms[i].localRotation = _localRotations[i];
            }
        }

        yield return null;

        if (playerVisualAnimator != null)
            playerVisualAnimator.enabled = true;

        yield return null;

        if (playerController != null)
            playerController.enabled = true;

        yield return null;

        if (playerMovement != null)
            playerMovement.enabled = true;
    }

    private void OnCutsceneStopped(PlayableDirector director)
    {
        introCutscene.stopped -= OnCutsceneStopped;
        cutsceneFinished = true;
    }
}