using System.Collections;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// SoundManager
// ─────────────────────────────────────────────────────────────────────────────
// Sistema de audio centralizado y escalable.
//
// CÓMO USAR:
//   1. Crea un GO "SoundManager" en la escena y añade este script.
//   2. Crea dos hijos: "MusicSource" y "SFXSource", cada uno con un AudioSource.
//      Puedes usar el menú Kimera/7 para que lo haga automáticamente.
//   3. Asigna los clips de audio en el Inspector de este componente.
//   4. Desde cualquier script: SoundManager.Instance?.PlaySFX(clip);
//
// NOTA: Si activas "Don't Destroy On Load", el SoundManager sobrevive entre
// escenas. Deja la opción desactivada si cada escena tiene el suyo propio.
// ─────────────────────────────────────────────────────────────────────────────
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    // ── Configuración ──────────────────────────────────────────────────────────

    [Header("Fuentes de audio (AudioSource — asignar o crear con menú 7)")]
    [SerializeField] private AudioSource musicSource;   // loop, música de fondo
    [SerializeField] private AudioSource sfxSource;     // one-shot, efectos de sonido

    [Header("Comportamiento")]
    [Tooltip("Si está activo, el SoundManager no se destruye al cambiar de escena")]
    [SerializeField] private bool dontDestroyOnLoad = false;

    // ── Música ─────────────────────────────────────────────────────────────────
    [Header("Música — arrastra tus clips AudioClip aquí")]
    [SerializeField] private AudioClip explorationMusic;
    [SerializeField] private AudioClip combatMusic;
    [SerializeField] private AudioClip victoryJingle;
    [SerializeField] private AudioClip defeatJingle;

    // ── Sonidos UI ─────────────────────────────────────────────────────────────
    [Header("UI")]
    [SerializeField] private AudioClip uiHover;
    [SerializeField] private AudioClip uiClick;

    // ── Sonidos del jugador ────────────────────────────────────────────────────
    [Header("Jugador")]
    [SerializeField] private AudioClip playerAttackSFX;
    [SerializeField] private AudioClip playerDamageSFX;
    [SerializeField] private AudioClip playerInstinctSFX;
    [SerializeField] private AudioClip playerDefendSFX;
    [SerializeField] private AudioClip playerItemSFX;
    [SerializeField] private AudioClip playerFootstepSFX;
    [SerializeField] private AudioClip playerVictorySFX;

    // ── Sonidos de enemigos ────────────────────────────────────────────────────
    [Header("Enemigos")]
    [SerializeField] private AudioClip enemyAttackSFX;
    [SerializeField] private AudioClip enemyDamageSFX;

    // ── Volúmenes ──────────────────────────────────────────────────────────────
    [Header("Volúmenes globales")]
    [Range(0f, 1f)] [SerializeField] private float musicVolume  = 0.55f;
    [Range(0f, 1f)] [SerializeField] private float sfxVolume    = 1.00f;
    [SerializeField] private float musicFadeDuration = 1.20f;

    private Coroutine _musicFadeRoutine;
    private bool      _subscribed;

    // ══════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ══════════════════════════════════════════════════════════════════════════

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

        EnsureSources();
    }

    private IEnumerator Start()
    {
        // Esperar a que CombatManager esté listo para suscribirse a sus eventos
        while (CombatManager.Instance == null) yield return null;
        SubscribeToCombat();
    }

    private void OnDestroy()
    {
        UnsubscribeFromCombat();
        if (Instance == this) Instance = null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // API pública — Música
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Reproduce la música de exploración con crossfade.</summary>
    public void PlayExplorationMusic()  => PlayMusic(explorationMusic);

    /// <summary>Reproduce la música de combate con crossfade.</summary>
    public void PlayCombatMusic()       => PlayMusic(combatMusic);

    /// <summary>Interrumpe la música de fondo y reproduce el jingle de victoria.</summary>
    public void PlayVictoryJingle()
    {
        StopMusic();
        PlaySFX(victoryJingle);
    }

    /// <summary>Interrumpe la música de fondo y reproduce el jingle de derrota.</summary>
    public void PlayDefeatJingle()
    {
        StopMusic();
        PlaySFX(defeatJingle);
    }

    /// <summary>Para la música con fade-out.</summary>
    public void StopMusic(bool immediate = false)
    {
        if (musicSource == null || !musicSource.isPlaying) return;
        if (immediate)
        {
            musicSource.Stop();
            return;
        }
        if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
        _musicFadeRoutine = StartCoroutine(FadeOutMusic(musicFadeDuration * 0.5f));
    }

    // ══════════════════════════════════════════════════════════════════════════
    // API pública — SFX de juego
    // ══════════════════════════════════════════════════════════════════════════

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    // ── UI ─────────────────────────────────────────────────────────────────────
    public void PlayUIHover()   => PlaySFX(uiHover);
    public void PlayUIClick()   => PlaySFX(uiClick);

    // ── Jugador ────────────────────────────────────────────────────────────────
    public void PlayPlayerAttack()   => PlaySFX(playerAttackSFX);
    public void PlayPlayerDamage()   => PlaySFX(playerDamageSFX);
    public void PlayPlayerInstinct() => PlaySFX(playerInstinctSFX);
    public void PlayPlayerDefend()   => PlaySFX(playerDefendSFX);
    public void PlayPlayerItem()     => PlaySFX(playerItemSFX);
    public void PlayPlayerFootstep() => PlaySFX(playerFootstepSFX);
    public void PlayPlayerVictory()  => PlaySFX(playerVictorySFX);

    // ── Enemigos ───────────────────────────────────────────────────────────────
    public void PlayEnemyAttack() => PlaySFX(enemyAttackSFX);
    public void PlayEnemyDamage() => PlaySFX(enemyDamageSFX);

    // ══════════════════════════════════════════════════════════════════════════
    // Suscripción a CombatManager
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Suscribirse a los eventos de CombatManager para reproducir SFX automáticamente.
    /// Se llama desde Start() y puede re-llamarse si el CombatManager se reinicia.
    /// </summary>
    public void SubscribeToCombat()
    {
        if (_subscribed || CombatManager.Instance == null) return;
        CombatManager.Instance.onActionPerformed   += OnCombatAction;
        CombatManager.Instance.onEnemyTookDamage   += OnEnemyDamaged;
        CombatManager.Instance.onPlayerTookDamage  += OnPlayerDamaged;
        CombatManager.Instance.onShowAnalysisPanel += OnInstinctUsed;
        _subscribed = true;
    }

    private void UnsubscribeFromCombat()
    {
        if (!_subscribed || CombatManager.Instance == null) return;
        CombatManager.Instance.onActionPerformed   -= OnCombatAction;
        CombatManager.Instance.onEnemyTookDamage   -= OnEnemyDamaged;
        CombatManager.Instance.onPlayerTookDamage  -= OnPlayerDamaged;
        CombatManager.Instance.onShowAnalysisPanel -= OnInstinctUsed;
        _subscribed = false;
    }

    // ── Handlers de combate ────────────────────────────────────────────────────

    private void OnCombatAction(string message)
    {
        // Detectar acción del jugador por el prefijo del mensaje
        if (message.StartsWith("Mike ataca"))
            PlayPlayerAttack();
        else if (message.StartsWith("Mike se pone en guardia"))
            PlayPlayerDefend();
        else if (message.StartsWith("Usaste"))
            PlayPlayerItem();
        else if (message.StartsWith("Mike usa Instinto"))
            { /* instinct SFX ya se maneja en OnInstinctUsed */ }
        else if (!message.StartsWith("Mike") && !message.StartsWith("Usaste") && !message.StartsWith("──"))
            PlayEnemyAttack();   // mensaje de acción enemiga
    }

    private void OnEnemyDamaged(EnemyCombatant _) => PlayEnemyDamage();
    private void OnPlayerDamaged(int _)            => PlayPlayerDamage();
    private void OnInstinctUsed(string _)          => PlayPlayerInstinct();

    // ══════════════════════════════════════════════════════════════════════════
    // Música — internos
    // ══════════════════════════════════════════════════════════════════════════

    private void PlayMusic(AudioClip clip)
    {
        if (clip == null || musicSource == null) return;
        // No re-iniciar si ya está sonando el mismo clip
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        if (_musicFadeRoutine != null) StopCoroutine(_musicFadeRoutine);
        _musicFadeRoutine = StartCoroutine(CrossfadeMusic(clip));
    }

    private IEnumerator CrossfadeMusic(AudioClip newClip)
    {
        float halfFade = musicFadeDuration * 0.5f;

        // Fade-out del clip actual
        if (musicSource.isPlaying && halfFade > 0f)
        {
            float startVol = musicSource.volume;
            float t = 0f;
            while (t < halfFade)
            {
                t += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(startVol, 0f, t / halfFade);
                yield return null;
            }
        }

        musicSource.Stop();
        musicSource.clip   = newClip;
        musicSource.loop   = true;
        musicSource.volume = 0f;
        if (newClip != null) musicSource.Play();

        // Fade-in del nuevo clip
        if (halfFade > 0f)
        {
            float t = 0f;
            while (t < halfFade)
            {
                t += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, musicVolume, t / halfFade);
                yield return null;
            }
        }
        musicSource.volume = musicVolume;
        _musicFadeRoutine = null;
    }

    private IEnumerator FadeOutMusic(float duration)
    {
        float startVol = musicSource.volume;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;
        }
        musicSource.Stop();
        musicSource.volume = musicVolume;
        _musicFadeRoutine  = null;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>Crea AudioSources en hijos si aún no existen.</summary>
    private void EnsureSources()
    {
        if (musicSource == null)
        {
            var go = new GameObject("MusicSource");
            go.transform.SetParent(transform);
            musicSource = go.AddComponent<AudioSource>();
            musicSource.loop   = true;
            musicSource.volume = musicVolume;
            musicSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            var go = new GameObject("SFXSource");
            go.transform.SetParent(transform);
            sfxSource = go.AddComponent<AudioSource>();
            sfxSource.loop   = false;
            sfxSource.volume = sfxVolume;
            sfxSource.playOnAwake = false;
        }
    }
}
