using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// EnemyPatrolChase
// ─────────────────────────────────────────────────────────────────────────────
// IA de enemigo que patrulla, persigue al jugador y desencadena el combate.
//
// ESTADOS:
//   SLEEPING  → desactivado hasta que GameProgress.OnShopCompleted dispare.
//   PATROL    → deambula en un radio; evita obstáculos con raycasts.
//   CHASE     → cuando el jugador entra en detectionRadius, lo persigue.
//   COMBAT    → cuando está a attackRadius del jugador, inicia el combate.
//
// COLISIÓN:
//   Usa CharacterController (cápsula de colisión integrada).
//   Agrega raycasts de 3 vías para evitar obstáculos activamente.
//
// ANIMACIÓN:
//   Mismo sistema BlendTree del NPC_Billboard_Walker:
//   parámetro FLOAT "Blend" (0.0–1.0) + flipX para las izquierdas.
//
// SETUP:
//   · Prefab: raíz con CharacterController + EnemyPatrolChase
//             hijo "Billboard" con Animator + SpriteRenderer
//   · Asignar en Inspector: billboard, theCamera, combatEnemies[]
//   · El spawner (NPC_Spawner ampliado) o un GO manual puede instanciarlo.
// ─────────────────────────────────────────────────────────────────────────────
[RequireComponent(typeof(CharacterController))]
public class EnemyPatrolChase : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Referencias")]
    public Transform  theCamera;
    public GameObject billboard;

    [Header("Combate")]
    [Tooltip("EnemyData que se cargará al iniciar el combate")]
    public EnemyData[] combatEnemies;

    [Header("Velocidades")]
    public float patrolSpeed = 1.0f;
    public float chaseSpeed  = 3.5f;

    [Header("Radios de detección")]
    [Tooltip("El jugador es visible dentro de este radio")]
    public float detectionRadius = 7f;
    [Tooltip("A esta distancia se inicia el combate")]
    public float attackRadius    = 1.4f;
    [Tooltip("Radio máximo de patrulla desde el spawn")]
    public float patrolRadius    = 8f;

    [Header("Patrulla")]
    public float changeIntervalMin = 2f;
    public float changeIntervalMax = 5f;

    [Header("Obstacle Avoidance")]
    [Tooltip("Longitud de los raycasts de evitación")]
    public float avoidRayLength  = 1.5f;
    [Tooltip("Layers de obstáculos (suelo NO, paredes SÍ)")]
    public LayerMask obstacleMask = ~0;   // todo por defecto; ajusta en Inspector

    [Header("Billboard")]
    public bool useMirror     = true;
    public bool use4Directions = false;

    // ── Componentes ───────────────────────────────────────────────────────────
    private CharacterController _cc;
    private Animator            _animator;
    private SpriteRenderer      _sprite;
    private Transform           _billboardT;
    private Transform           _t;
    private Transform           _playerT;

    // ── Estado interno ────────────────────────────────────────────────────────
    private enum State { Sleeping, Patrol, Chase }
    private State _state = State.Sleeping;

    private Vector3 _spawnPos;
    private Vector3 _moveDir;
    private float   _changeTimer;
    private bool    _combatStarted = false;

    // ── Facing (mismos valores que NPC_Billboard_Walker) ─────────────────────
    private enum Facing { Up, UpRight, Right, DownRight, Down, DownLeft, Left, UpLeft }
    private Facing _facing;
    private float  _angle;
    private float  _sign;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _t        = transform;
        _spawnPos = _t.position;
        _cc       = GetComponent<CharacterController>();

        // billboard puede asignarse DESPUÉS de Awake (desde el spawner),
        // así que la inicialización real se hace en InitBillboard().
        InitBillboard();

        // Buscar el jugador por tag
        var playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) _playerT = playerGO.transform;

        PickNewPatrolDir();
    }

    // Inicializa las refs internas del billboard.
    // Se llama en Awake y, por seguridad, de forma lazy en Update cuando
    // billboard se asignó después de Awake (caso del spawner dinámico).
    private void InitBillboard()
    {
        if (billboard == null) return;
        _animator   = billboard.GetComponent<Animator>();
        _sprite     = billboard.GetComponent<SpriteRenderer>();
        _billboardT = billboard.transform;
    }

    private void Start()
    {
        if (GameProgress.ShopCompleted)
            Activate();

        // Suscripción permanente: se reactiva en cada compra de la tienda
        GameProgress.OnShopCompleted += OnShopDone;
    }

    private void OnDestroy()
    {
        GameProgress.OnShopCompleted -= OnShopDone;
    }

    // ── Activación ────────────────────────────────────────────────────────────

    private void OnShopDone(int _)
    {
        GameProgress.OnShopCompleted -= OnShopDone;
        Activate();
    }

    private void Activate()
    {
        _combatStarted = false;   // ← línea nueva
        _state = State.Patrol;
        Debug.Log($"[EnemyPatrolChase] '{name}' activado.");
    }

    // ── Ciclo principal ───────────────────────────────────────────────────────

    private void Update()
    {
        if (_state == State.Sleeping) return;
        if (_combatStarted) return;

        EvaluateState();

        switch (_state)
        {
            case State.Patrol: DoPatrol(); break;
            case State.Chase:  DoChase();  break;
        }

        if (theCamera != null && billboard != null)
        {
            // Lazy init: billboard asignado después de Awake (spawner dinámico)
            if (_billboardT == null) InitBillboard();
            if (_billboardT != null) UpdateBillboard();
        }
    }

    private void LateUpdate()
    {
        if (_state == State.Sleeping || _combatStarted) return;
        if (theCamera != null && _billboardT != null) CalculateFacing();
    }

    // ── Lógica de estados ─────────────────────────────────────────────────────

    private void EvaluateState()
    {
        if (_playerT == null) return;

        float dist = Vector3.Distance(
            new Vector3(_t.position.x, 0f, _t.position.z),
            new Vector3(_playerT.position.x, 0f, _playerT.position.z));

        // Iniciar combate
        if (dist <= attackRadius)
        {
            TriggerCombat();
            return;
        }

        // Chase ↔ Patrol
        _state = dist <= detectionRadius ? State.Chase : State.Patrol;
    }

    // ── Patrulla ──────────────────────────────────────────────────────────────

    private void DoPatrol()
    {
        _changeTimer -= Time.deltaTime;
        if (_changeTimer <= 0f) PickNewPatrolDir();

        // Regresar al spawn si sale del radio
        Vector3 flatPos   = new Vector3(_t.position.x, 0f, _t.position.z);
        Vector3 flatSpawn = new Vector3(_spawnPos.x,   0f, _spawnPos.z);
        if (Vector3.Distance(flatPos, flatSpawn) > patrolRadius)
            _moveDir = (flatSpawn - flatPos).normalized;

        Vector3 steered = SteerAroundObstacles(_moveDir);
        MoveInDirection(steered, patrolSpeed);
    }

    private void PickNewPatrolDir()
    {
        float a = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        _moveDir     = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
        _changeTimer = Random.Range(changeIntervalMin, changeIntervalMax);
    }

    // ── Persecución ───────────────────────────────────────────────────────────

    private void DoChase()
    {
        if (_playerT == null) return;

        Vector3 toPlayer = (_playerT.position - _t.position);
        toPlayer.y = 0f;
        Vector3 dir = toPlayer.normalized;

        Vector3 steered = SteerAroundObstacles(dir);
        MoveInDirection(steered, chaseSpeed);
    }

    // ── Evitación REACTIVA ────────────────────────────────────────────────────
    // Refleja la dirección al golpear físicamente con un colisionador.
    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.6f) return;   // ignorar suelo

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
    private Vector3 SteerAroundObstacles(Vector3 desiredDir)
    {
        if (desiredDir == Vector3.zero) return desiredDir;

        Vector3 origin = _t.position + Vector3.up * 0.6f;

        if (!Physics.Raycast(origin, desiredDir, avoidRayLength, obstacleMask,
                             QueryTriggerInteraction.Ignore))
            return desiredDir;

        float[] angles = { 45f, 80f, 120f };
        foreach (float a in angles)
        {
            Vector3 rightDir = Quaternion.Euler(0f,  a, 0f) * desiredDir;
            Vector3 leftDir  = Quaternion.Euler(0f, -a, 0f) * desiredDir;

            bool hitR = Physics.Raycast(origin, rightDir, avoidRayLength, obstacleMask,
                                        QueryTriggerInteraction.Ignore);
            bool hitL = Physics.Raycast(origin, leftDir,  avoidRayLength, obstacleMask,
                                        QueryTriggerInteraction.Ignore);

            if (!hitR) return rightDir;
            if (!hitL) return leftDir;
        }

        // Todo bloqueado
        PickNewPatrolDir();
        return _moveDir;
    }

    // ── Movimiento con CharacterController ───────────────────────────────────

    private void MoveInDirection(Vector3 dir, float speed)
    {
        if (dir != Vector3.zero)
            _t.forward = Vector3.Slerp(_t.forward, dir, Time.deltaTime * 8f);

        Vector3 motion = dir * speed;
        motion.y = -9.8f;
        _cc.Move(motion * Time.deltaTime);

        // Animación walking
        if (_animator != null) _animator.SetBool("walking", dir.sqrMagnitude > 0.01f);
    }

    // ── Combate ───────────────────────────────────────────────────────────────

    private void TriggerCombat()
    {
        if (_combatStarted) return;
        _combatStarted = true;

        if (InSceneCombatController.Instance == null)
        {
            Debug.LogWarning($"[EnemyPatrolChase] InSceneCombatController no encontrado.", this);
            return;
        }
        if (combatEnemies == null || combatEnemies.Length == 0)
        {
            Debug.LogWarning($"[EnemyPatrolChase] '{name}' no tiene combatEnemies asignados.", this);
            return;
        }

        Debug.Log($"[EnemyPatrolChase] '{name}' inicia combate con {combatEnemies.Length} enemigo(s).");
        InSceneCombatController.Instance.StartCombat(combatEnemies, null);
    }

    // ── Billboard ─────────────────────────────────────────────────────────────

    private void UpdateBillboard()
    {
        Vector3 dir = theCamera.position - _t.position;
        dir.y = 0f;
        if (dir != Vector3.zero)
            _billboardT.rotation = Quaternion.LookRotation(-dir, _t.up);
    }

    private void CalculateFacing()
    {
        Vector3 forward = _t.forward; forward.y = 0f;

        Vector3 dir2 = _t.InverseTransformPoint(theCamera.position);
        _sign = dir2.x >= 0 ? -1f : 1f;

        Vector3 camDir = theCamera.position - _t.position; camDir.y = 0f;
        _angle = Vector3.Angle(camDir, forward);

        if (use4Directions)
        {
            _facing = _angle < 45f ? Facing.Up
                    : _angle < 135f ? (_sign < 0 ? Facing.Right : Facing.Left)
                    : Facing.Down;
        }
        else
        {
            _facing = _angle < 22.5f  ? Facing.Up
                    : _angle < 67.5f  ? (_sign < 0 ? Facing.UpRight   : Facing.UpLeft)
                    : _angle < 112.5f ? (_sign < 0 ? Facing.Right      : Facing.Left)
                    : _angle < 157.5f ? (_sign < 0 ? Facing.DownRight  : Facing.DownLeft)
                    : Facing.Down;
        }

        ApplyAnimation();
    }

    private void ApplyAnimation()
    {
        if (_animator == null) return;
        if (_sprite != null && useMirror) ApplyMirror();
        else if (_sprite != null) _sprite.flipX = false;

        _animator.SetFloat("Blend", (int)_facing * 0.25f);
    }

    private void ApplyMirror()
    {
        switch (_facing)
        {
            case Facing.DownLeft: _facing = Facing.DownRight; _sprite.flipX = true;  break;
            case Facing.Left:     _facing = Facing.Right;     _sprite.flipX = true;  break;
            case Facing.UpLeft:   _facing = Facing.UpRight;   _sprite.flipX = true;  break;
            default:                                           _sprite.flipX = false; break;
        }
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        // Radio de detección
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Radio de ataque
        Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, attackRadius);

        // Radio de patrulla
        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.15f);
        Gizmos.DrawWireSphere(Application.isPlaying ? _spawnPos : transform.position, patrolRadius);
    }
}
