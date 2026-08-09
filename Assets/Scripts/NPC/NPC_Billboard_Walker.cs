using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// NPC_Billboard_Walker
// ─────────────────────────────────────────────────────────────────────────────
// NPC que camina aleatoriamente con Billboard + sistema de 8 direcciones.
//
// ANIMACIÓN:
//   Animator del Billboard: BlendTree en estado "Walk", parámetro FLOAT "Blend"
//   (thresholds 0 → 0.25 → 0.5 → 0.75 → 1.0):
//     0.00 → walk_front     (cámara al frente)
//     0.25 → Walk_UpRight
//     0.50 → Walk_Right
//     0.75 → Walk_DownRight
//     1.00 → Walk_Back      (cámara detrás)
//   Izquierdas espejadas con flipX.
//
// OBSTACLE AVOIDANCE:
//   PREVENTIVO  — 5 raycasts (0°, ±45°, ±80°) antes de moverse.
//   REACTIVO    — OnControllerColliderHit: refleja la dirección al golpear.
// ─────────────────────────────────────────────────────────────────────────────
[RequireComponent(typeof(CharacterController))]
public class NPC_Billboard_Walker : MonoBehaviour
{
    // ── Paleta de colores ─────────────────────────────────────────────────────
    private static readonly Color[] DefaultPalette =
    {
        new Color(0.306f, 0.514f, 0.325f),
        new Color(0.933f, 0.906f, 0.784f),
        new Color(0.337f, 0.396f, 0.698f),
        new Color(0.753f, 0.733f, 0.824f),
        new Color(0.388f, 0.361f, 0.408f),
        new Color(0.329f, 0.682f, 0.882f),
        new Color(0.855f, 0.533f, 0.588f),
        new Color(0.392f, 0.647f, 0.612f),
        new Color(0.502f, 0.435f, 0.404f),
        new Color(0.875f, 0.784f, 0.737f),
        new Color(0.875f, 0.835f, 0.749f),
        new Color(0.459f, 0.686f, 0.859f),
        new Color(0.690f, 0.800f, 0.447f),
        new Color(0.894f, 0.600f, 0.494f),
    };

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Referencias")]
    public Transform  theCamera;
    [Tooltip("Hijo con Animator y SpriteRenderer (Billboard)")]
    public GameObject billboard;

    [Header("Movimiento")]
    public float moveSpeed          = 0.7f;
    public float changeIntervalMin  = 2f;
    public float changeIntervalMax  = 5f;
    [Tooltip("Radio máximo al que puede alejarse del punto de spawn")]
    public float wanderRadius       = 8f;

    [Header("Billboard")]
    public bool useMirror      = true;
    public bool use4Directions = false;

    [Header("Obstacle Avoidance")]
    [Tooltip("Longitud de los raycasts preventivos")]
    public float    avoidRayLength = 1.5f;
    [Tooltip("Layers de obstáculos (excluir Triggers si hace falta)")]
    public LayerMask obstacleMask  = ~0;

    [Header("Apariencia")]
    public Color[] colorPalette;
    [Tooltip("Si true, color negro (NPC de combate). Lo asigna el spawner.")]
    public bool isCombatNPC = false;

    // ── Componentes ───────────────────────────────────────────────────────────
    private CharacterController _cc;
    private Animator            _animator;
    private SpriteRenderer      _sprite;
    private Transform           _billboardT;
    private Transform           _t;

    // ── Estado de movimiento ──────────────────────────────────────────────────
    private Vector3 _spawnPos;
    private Vector3 _moveDir = Vector3.zero;
    private float   _changeTimer;

    // ── Facing ────────────────────────────────────────────────────────────────
    private enum Facing { Up, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft }
    private Facing _facing;
    private float  _sign = 1f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _t        = transform;
        _spawnPos = _t.position;
        _cc       = GetComponent<CharacterController>();

        if (billboard != null)
        {
            _animator   = billboard.GetComponent<Animator>();
            _sprite     = billboard.GetComponent<SpriteRenderer>();
            _billboardT = billboard.transform;
        }

        PickNewDirection();
    }

    private void Start()
    {
        // Color en Start para que el spawner pueda modificar isCombatNPC
        // entre Instantiate (→ Awake) y el primer Start.
        ApplyColor();
    }

    // ── Ciclo ─────────────────────────────────────────────────────────────────

    private void Update()
    {
        if (theCamera == null || billboard == null) return;

        HandleMovement();
        HandleBillboard();
        HandleFacingAnimation();
    }

    private void LateUpdate()
    {
        CalculateFacing();
    }

    // ── Color ─────────────────────────────────────────────────────────────────

    private void ApplyColor()
    {
        if (_sprite == null) return;

        if (isCombatNPC) { _sprite.color = Color.black; return; }

        Color[] palette = (colorPalette != null && colorPalette.Length > 0)
            ? colorPalette : DefaultPalette;
        _sprite.color = palette[Random.Range(0, palette.Length)];
    }

    // ── Movimiento ────────────────────────────────────────────────────────────

    private void HandleMovement()
    {
        _changeTimer -= Time.deltaTime;
        if (_changeTimer <= 0f) PickNewDirection();

        // Volver al spawn si sale del radio
        Vector3 flatPos   = new Vector3(_t.position.x, 0f, _t.position.z);
        Vector3 flatSpawn = new Vector3(_spawnPos.x,   0f, _spawnPos.z);
        if (Vector3.Distance(flatPos, flatSpawn) > wanderRadius)
            _moveDir = (flatSpawn - flatPos).normalized;

        // Evitación preventiva con raycasts
        Vector3 steered = SteerAroundObstacles(_moveDir);

        // ── Actualizar forward del transform ─────────────────────────────────
        // IMPRESCINDIBLE: CalculateFacing usa _t.forward para saber qué
        // animación reproducir. Sin esto la animación siempre es la del spawn.
        if (steered.sqrMagnitude > 0.01f)
            _t.forward = Vector3.Slerp(_t.forward, steered, Time.deltaTime * 8f);

        Vector3 motion = steered * moveSpeed;
        motion.y = -9.8f;
        _cc.Move(motion * Time.deltaTime);
    }

    // ── Evitación REACTIVA ────────────────────────────────────────────────────
    // Se llama automáticamente cuando el CharacterController toca un colisionador.
    // Refleja la dirección de movimiento sobre la normal de la pared.
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Ignorar suelo y techo
        if (hit.normal.y > 0.6f) return;

        Vector3 reflected = Vector3.Reflect(_moveDir, hit.normal);
        reflected.y = 0f;
        if (reflected.sqrMagnitude > 0.01f)
        {
            _moveDir     = reflected.normalized;
            _changeTimer = Random.Range(changeIntervalMin, changeIntervalMax);
        }
    }

    // ── Evitación PREVENTIVA (raycasts) ──────────────────────────────────────
    // Prueba ángulos progresivamente más amplios a ambos lados.
    private Vector3 SteerAroundObstacles(Vector3 desired)
    {
        if (desired == Vector3.zero) return desired;

        Vector3 origin = _t.position + Vector3.up * 0.6f;

        // Sin obstáculo directo → avanzar
        if (!Physics.Raycast(origin, desired, avoidRayLength, obstacleMask,
                             QueryTriggerInteraction.Ignore))
            return desired;

        // Probar ángulos crecientes a derecha e izquierda
        float[] angles = { 45f, 80f, 120f };
        foreach (float a in angles)
        {
            Vector3 rightDir = Quaternion.Euler(0f,  a, 0f) * desired;
            Vector3 leftDir  = Quaternion.Euler(0f, -a, 0f) * desired;

            bool hitR = Physics.Raycast(origin, rightDir, avoidRayLength, obstacleMask,
                                        QueryTriggerInteraction.Ignore);
            bool hitL = Physics.Raycast(origin, leftDir,  avoidRayLength, obstacleMask,
                                        QueryTriggerInteraction.Ignore);

            if (!hitR) { _moveDir = rightDir; return rightDir; }
            if (!hitL) { _moveDir = leftDir;  return leftDir;  }
        }

        // Todo bloqueado → dirección completamente aleatoria
        PickNewDirection();
        return _moveDir;
    }

    private void PickNewDirection()
    {
        float a  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        _moveDir     = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        _changeTimer = Random.Range(changeIntervalMin, changeIntervalMax);
    }

    // ── Billboard (siempre mira a la cámara) ──────────────────────────────────

    private void HandleBillboard()
    {
        Vector3 dir = theCamera.position - _t.position;
        dir.y = 0f;
        if (dir != Vector3.zero)
            _billboardT.rotation = Quaternion.LookRotation(-dir, _t.up);
    }

    // ── Cálculo de facing (LateUpdate) ───────────────────────────────────────
    // Calcula el ángulo cámara↔forward y de qué lado está la cámara.
    private void CalculateFacing()
    {
        // _t.forward ya apunta en la dirección de movimiento (actualizado en Update).
        Vector3 camDir = theCamera.position - _t.position;
        camDir.y = 0f;

        Vector3 dir2 = _t.InverseTransformPoint(theCamera.position);
        _sign = (dir2.x >= 0) ? -1f : 1f;

        float angle = Vector3.Angle(camDir, _t.forward);

        if (use4Directions)
        {
            _facing = angle < 45f  ? Facing.Up
                    : angle < 135f ? (_sign < 0 ? Facing.Right : Facing.Left)
                    : Facing.Down;
        }
        else
        {
            _facing = angle < 22.5f  ? Facing.Up
                    : angle < 67.5f  ? (_sign < 0 ? Facing.UpRight   : Facing.UpLeft)
                    : angle < 112.5f ? (_sign < 0 ? Facing.Right      : Facing.Left)
                    : angle < 157.5f ? (_sign < 0 ? Facing.DownRight  : Facing.DownLeft)
                    : Facing.Down;
        }
    }

    // ── Animación ─────────────────────────────────────────────────────────────

    private void HandleFacingAnimation()
    {
        if (_animator == null) return;

        if (useMirror) ApplyMirror();
        else if (_sprite != null) _sprite.flipX = false;

        // Blend: Facing 0-4 (tras mirror) → 0.0-1.0
        _animator.SetFloat("Blend",   (int)_facing * 0.25f);
        _animator.SetBool ("walking", true);
    }

    private void ApplyMirror()
    {
        if (_sprite == null) return;

        if (use4Directions)
        {
            if (_facing == Facing.Left) { _facing = Facing.Right; _sprite.flipX = true; }
            else                          _sprite.flipX = false;
        }
        else
        {
            switch (_facing)
            {
                case Facing.DownLeft: _facing = Facing.DownRight; _sprite.flipX = true;  break;
                case Facing.Left:     _facing = Facing.Right;     _sprite.flipX = true;  break;
                case Facing.UpLeft:   _facing = Facing.UpRight;   _sprite.flipX = true;  break;
                default:                                           _sprite.flipX = false; break;
            }
        }
    }
}
