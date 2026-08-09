using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// NPC_Spawner
// ─────────────────────────────────────────────────────────────────────────────
// Instancia NPCs en el área definida.
//
// NPCS NORMALES:
//   · Se les asigna un color aleatorio de la paleta (definida en NPC_Billboard_Walker).
//   · Usan la animación de 8 direcciones con espejo.
//
// NPC DE COMBATE (negro, partículas rojas):
//   · Es siempre el PRIMER NPC instanciado (NPC_0).
//   · Color negro, partículas rojas que lo identifican.
//   · Al tocar al jugador inicia el combate definido en combatEnemies[].
//   · Tiene un SphereCollider isTrigger como zona de detección.
// ─────────────────────────────────────────────────────────────────────────────
public class NPC_Spawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Prefab del NPC (raíz: CharacterController + NPC_Billboard_Walker; hijo: Billboard con Animator + SpriteRenderer)")]
    public GameObject npcPrefab;
    [Tooltip("Main Camera — se asigna a cada NPC instanciado")]
    public Transform theCamera;

    [Header("Cantidad")]
    [Min(1)] public int count = 6;

    [Header("Área de spawn")]
    public Vector3 areaCenter;
    public Vector2 areaSize = new Vector2(20f, 20f);

    [Header("Suelo")]
    [Tooltip("Si true, hace raycast hacia abajo para posicionar el NPC en el suelo")]
    public bool spawnOnGround = true;
    public LayerMask groundMask;
    [Tooltip("Altura desde la que parte el raycast")]
    public float raycastHeight = 10f;

    [Header("NPC de Combate (NPC negro)")]
    [Tooltip("EnemyData[] que se cargarán al tocar al NPC negro.\n" +
             "Si está vacío no se creará el NPC de combate.")]
    public EnemyData[] combatEnemies;
    [Tooltip("Radio del SphereCollider isTrigger del NPC de combate")]
    public float combatTriggerRadius = 1.5f;
    [Tooltip("Tasa de emisión de partículas rojas (partículas/segundo)")]
    public float particleRate = 20f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (areaCenter == Vector3.zero)
            areaCenter = transform.position;

        SpawnAll();
    }

    // ── Spawn principal ───────────────────────────────────────────────────────

    public void SpawnAll()
    {
        if (npcPrefab == null)
        {
            Debug.LogWarning("[NPC_Spawner] Falta asignar npcPrefab.");
            return;
        }

        bool hasCombatNPC = combatEnemies != null && combatEnemies.Length > 0;

        for (int i = 0; i < count; i++)
        {
            Vector3    spawnPos = GetRandomPosition();
            GameObject npc      = Instantiate(npcPrefab, spawnPos, Quaternion.identity);
            npc.name = $"NPC_{i}";

            // Parentar al spawner → desactivar el spawner ocultará todos los NPCs
            npc.transform.SetParent(transform, worldPositionStays: true);

            // ── Asignar cámara ────────────────────────────────────────────────
            NPC_Billboard_Walker walker = npc.GetComponent<NPC_Billboard_Walker>();
            if (walker != null && theCamera != null)
                walker.theCamera = theCamera;

            // ── NPC 0 = NPC de combate (si hay enemigos configurados) ─────────
            if (i == 0 && hasCombatNPC)
            {
                SetupCombatNPC(npc, walker);
            }
            // ── El resto = NPCs normales con color aleatorio ───────────────────
            // (el color se aplica en NPC_Billboard_Walker.Start() automáticamente)
        }
    }

    // ── Setup del NPC negro de combate ────────────────────────────────────────
    // Desactiva NPC_Billboard_Walker y usa EnemyPatrolChase en su lugar.
    // EnemyPatrolChase maneja: patrol → chase → combat, activado tras la tienda.

    private void SetupCombatNPC(GameObject npc, NPC_Billboard_Walker walker)
    {
        // 1. Deshabilitar el walker normal (EnemyPatrolChase mueve el CharacterController)
        if (walker != null) walker.enabled = false;

        // 2. Pintar de negro el sprite manualmente (walker.Start() no correrá)
        var sprite = npc.GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) sprite.color = Color.black;

        // 3. Añadir EnemyPatrolChase — patrol + chase + combat tras OnShopCompleted
        EnemyPatrolChase chase = npc.GetComponent<EnemyPatrolChase>();
        if (chase == null) chase = npc.AddComponent<EnemyPatrolChase>();

        chase.combatEnemies = combatEnemies;

        if (theCamera != null)
            chase.theCamera = theCamera;

        // Conectar el hijo Billboard al componente de chase
        if (walker != null && walker.billboard != null)
        {
            chase.billboard = walker.billboard;
        }
        else
        {
            // Buscar hijo llamado "Billboard" por nombre
            Transform billboardT = npc.transform.Find("Billboard");
            if (billboardT != null) chase.billboard = billboardT.gameObject;
        }

        // 4. Sistema de partículas rojas como hijo del NPC
        AddRedParticles(npc);

        Debug.Log($"[NPC_Spawner] NPC de combate '{npc.name}' creado con EnemyPatrolChase " +
                  $"({combatEnemies.Length} enemigo(s)). Perseguirá al jugador tras completar la tienda.");
    }

    // ── Partículas rojas ──────────────────────────────────────────────────────

    private void AddRedParticles(GameObject parent)
    {
        GameObject psGO = new GameObject("CombatParticles");
        psGO.transform.SetParent(parent.transform);
        psGO.transform.localPosition = new Vector3(0f, 1.4f, 0f);

        ParticleSystem ps = psGO.AddComponent<ParticleSystem>();

        // ── Main ──────────────────────────────────────────────────────────────
        var main = ps.main;
        main.loop             = true;
        main.playOnAwake      = true;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;
        main.startLifetime    = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed       = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startSize        = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startColor       = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0f, 0f, 1f),
            new Color(1f, 0.3f, 0.1f, 0.8f));
        main.gravityModifier  = -0.3f;   // flotan hacia arriba ligeramente

        // ── Emission ──────────────────────────────────────────────────────────
        var emission = ps.emission;
        emission.rateOverTime = particleRate;

        // ── Shape: esfera alrededor del NPC ──────────────────────────────────
        var shape = ps.shape;
        shape.enabled    = true;
        shape.shapeType  = ParticleSystemShapeType.Sphere;
        shape.radius     = 0.4f;

        // ── Color sobre vida: fade out ────────────────────────────────────────
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]  { new GradientColorKey(Color.red, 0f),  new GradientColorKey(Color.red, 1f) },
            new GradientAlphaKey[]  { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ── Renderer: usa sprite circular (built-in) ──────────────────────────
        var renderer = psGO.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode   = ParticleSystemRenderMode.Billboard;
        renderer.sortingOrder = 1;

        ps.Play();
    }

    // ── Posición aleatoria con raycast de suelo ───────────────────────────────

    private Vector3 GetRandomPosition()
    {
        float x = areaCenter.x + Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f);
        float z = areaCenter.z + Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f);
        float y = areaCenter.y;

        if (spawnOnGround)
        {
            Vector3 origin = new Vector3(x, areaCenter.y + raycastHeight, z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight * 2f, groundMask))
                y = hit.point.y;
        }

        return new Vector3(x, y, z);
    }

    // ── Gizmo del área de spawn ───────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Vector3 center = areaCenter == Vector3.zero ? transform.position : areaCenter;

        // Área general (verde)
        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 0.2f);
        Gizmos.DrawCube(center, new Vector3(areaSize.x, 0.1f, areaSize.y));
        Gizmos.color = new Color(0.2f, 0.8f, 0.4f, 1f);
        Gizmos.DrawWireCube(center, new Vector3(areaSize.x, 0.1f, areaSize.y));

        // Radio del trigger del NPC de combate (rojo)
        if (combatEnemies != null && combatEnemies.Length > 0)
        {
            Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.4f);
            Gizmos.DrawWireSphere(center, combatTriggerRadius);
        }
    }
}
