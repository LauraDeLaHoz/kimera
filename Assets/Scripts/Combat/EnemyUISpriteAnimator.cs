using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
// EnemyUISpriteAnimator
// ─────────────────────────────────────────────────────────────────────────────
// Da a los displays de enemigo una animación idle de frames.
//
// CÓMO USARLO:
//   Los frames se cargan automáticamente desde EnemyData.idleAnimationFrames
//   cuando EnemyActionDisplay.Refresh() detecta qué enemigo mostrar.
//   También puedes asignar frames manualmente en el Inspector como fallback.
//
//   Para asignar animación a un enemigo:
//   1. Abre el EnemyData ScriptableObject (Assets/ScriptableObjects/Data/)
//   2. En "Animación idle en la UI de combate" arrastra los sprites en orden
//   3. Ajusta "Animation Fps" (6-8 fps para look retro)
//
// Si el enemigo no tiene frames en su EnemyData, se usa el sprite estático.
// EnemyActionDisplay pausa la animación durante las acciones.
// ─────────────────────────────────────────────────────────────────────────────
[RequireComponent(typeof(Image))]
public class EnemyUISpriteAnimator : MonoBehaviour
{
    [Header("Frames fallback (sobreescritos por EnemyData en runtime)")]
    [Tooltip("Frames de animación idle asignados manualmente. " +
             "EnemyActionDisplay los reemplaza con los de EnemyData al cargar el combate.")]
    [SerializeField] private Sprite[] idleFrames;

    [Header("Velocidad")]
    [Tooltip("Frames de animación por segundo (sobreescrito por EnemyData.animationFps)")]
    [Range(1f, 24f)]
    [SerializeField] private float fps = 7f;

    // ── Runtime ───────────────────────────────────────────────────────────────
    private Image _image;
    private int   _currentFrame;
    private float _timer;
    private bool  _paused;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _image = GetComponent<Image>();
    }

    private void Update()
    {
        if (_paused || idleFrames == null || idleFrames.Length < 2 || _image == null)
            return;

        _timer += Time.deltaTime;
        float interval = 1f / fps;

        if (_timer >= interval)
        {
            _timer -= interval;
            _currentFrame = (_currentFrame + 1) % idleFrames.Length;
            Sprite next = idleFrames[_currentFrame];
            if (next != null) _image.sprite = next;
        }
    }

    // ── API pública ────────────────────────────────────────────────────────────

    /// <summary>
    /// Pausa o reanuda la animación.
    /// EnemyActionDisplay llama a Pause(true) antes de mostrar un sprite de acción
    /// y a Pause(false) cuando vuelve al idle.
    /// </summary>
    public void Pause(bool paused) => _paused = paused;

    /// <summary>
    /// Reinicia la animación al frame 0.
    /// </summary>
    public void ResetAnimation()
    {
        _currentFrame = 0;
        _timer        = 0f;
        _paused       = false;

        if (_image != null && idleFrames != null &&
            idleFrames.Length > 0 && idleFrames[0] != null)
        {
            _image.sprite = idleFrames[0];
        }
    }

    /// <summary>
    /// Carga un nuevo conjunto de frames (llamado desde EnemyActionDisplay.Refresh
    /// cuando el enemigo que muestra este display cambia).
    /// Si frames es null o tiene menos de 2 elementos, la animación se detiene
    /// y el sprite estático queda a cargo de EnemyActionDisplay.
    /// </summary>
    public void SetFrames(Sprite[] frames, float newFps = -1f)
    {
        idleFrames = frames;
        if (newFps > 0f) fps = newFps;

        if (idleFrames != null && idleFrames.Length >= 2)
        {
            // Frames válidos → reiniciar animación con los nuevos sprites
            ResetAnimation();
        }
        else
        {
            // Sin frames suficientes → pausar; el sprite estático lo pone EnemyActionDisplay
            _paused       = true;
            _currentFrame = 0;
            _timer        = 0f;
        }
    }

    /// <summary>Devuelve true si tiene al menos 2 frames asignados (animación activa).</summary>
    public bool HasAnimation => idleFrames != null && idleFrames.Length >= 2;

    /// <summary>El primer frame de la animación (o null si no hay frames).</summary>
    public Sprite FirstFrame => (idleFrames != null && idleFrames.Length > 0) ? idleFrames[0] : null;
}
