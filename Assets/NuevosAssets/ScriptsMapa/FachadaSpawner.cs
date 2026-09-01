using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Rellena aleatoriamente los tramos de una calle/plaza con prefabs de fachadas.
/// El ancho de cada fachada se MIDE AUTOMATICAMENTE de su bounding box real (Renderer),
/// asi que no hace falta calcularlo ni tipearlo a mano.
///
/// Dos formas de definir el recorrido:
///  - Modo simple: un solo tramo entre "Inicio" y "Final".
///  - Modo ruta: cargá 2 o mas puntos en "Puntos De La Ruta" (en el orden del recorrido)
///    para una calle con varios tramos rectos, opcionalmente cerrada en loop (plaza/manzana).
/// </summary>
public class FachadaSpawner : MonoBehaviour
{
    public enum EjeAvance { X, Z }

    [System.Serializable]
    public class FachadaOption
    {
        public GameObject prefab;

        [Tooltip("Dejalo en 0 para medir el ancho automaticamente del prefab (recomendado). " +
                 "Si el auto-calculo te da algo raro, poné aca el ancho real en metros como respaldo.")]
        public float anchoManual = 0f;

        [Tooltip("Que tan seguido puede salir esta fachada frente a las demas.")]
        public float peso = 1f;
    }

    [Header("Puntos de la calle (modo simple: arrastra los hijos Inicio / Final)")]
    [Tooltip("Se usan solo si 'Puntos De La Ruta' esta vacia o tiene menos de 2 elementos.")]
    public Transform inicio;
    public Transform final;

    [Header("Ruta (opcional, para una zona con varios tramos)")]
    [Tooltip("Si esta lista tiene 2 o mas puntos, reemplaza a Inicio/Final: cada par de puntos " +
             "consecutivos es un tramo recto que se rellena con fachadas. El orden de la lista " +
             "ES el orden del recorrido: arrastra los puntos en el orden en que querés recorrer " +
             "la calle/plaza.")]
    public List<Transform> puntosRuta = new List<Transform>();

    [Tooltip("Si esta activo, el ultimo punto de la ruta se conecta de nuevo con el primero " +
             "(para una manzana o plaza cerrada). Necesita 3 o mas puntos en la ruta.")]
    public bool cerrarLoop = false;

    [Header("Fachadas disponibles")]
    public List<FachadaOption> fachadas = new List<FachadaOption>();

    [Header("Opciones de generacion")]
    [Tooltip("Donde se instancian las fachadas. Si lo dejas vacio, se usan como hijos de este mismo objeto.")]
    public Transform contenedor;

    [Tooltip("Separacion extra entre fachada y fachada (0 = pegadas).")]
    public float espacioEntreFachadas = 0f;

    [Tooltip("Si el ultimo hueco es mas chico que la fachada elegida, la achica para que encaje justo " +
             "en vez de dejar hueco o pasarse del final del tramo.")]
    public bool ajustarUltimaFachada = true;

    [Tooltip("Que eje LOCAL del prefab es el que 'avanza' a lo largo de la calle. Si las fachadas quedan " +
             "mal orientadas o amontonadas, probá cambiar esto de X a Z (o viceversa).")]
    public EjeAvance ejeAvance = EjeAvance.X;

    [Tooltip("Rotacion extra en Y para terminar de alinear la fachada (probá 0 / 90 / 180 / 270 hasta que quede bien).")]
    public float rotacionExtraY = 0f;

    [Tooltip("Evita que la misma fachada salga dos veces seguida.")]
    public bool evitarRepetirVecino = true;

    [Tooltip("Si esta activo, cada fachada se reacomoda en Y para que su base (el punto mas bajo de su " +
             "malla) quede apoyada en el suelo, sin importar la altura del prefab ni donde tenga el pivote.")]
    public bool anclarAlPiso = true;

    [Tooltip("Usar una semilla fija para que la calle se genere siempre igual (util para depurar).")]
    public bool usarSemilla = false;
    public int semilla = 0;
    public bool generarAlIniciar = true;

    // Cache del sistema de pesos, para no recalcularlo en cada fachada colocada.
    float[] _pesosCache;
    float _pesoTotalCache;

    void Start()
    {
        if (Application.isPlaying && generarAlIniciar)
            Generar();
    }

    [ContextMenu("Generar Fachadas")]
    public void Generar()
    {
        List<Vector3> puntos = ConstruirPuntos();
        if (puntos == null || puntos.Count < 2)
        {
            Debug.LogWarning($"[{name}] Hacen falta al menos 2 puntos: asigná Inicio/Final, o " +
                              "cargá 2 o mas Transforms en 'Puntos De La Ruta'.");
            return;
        }
        if (fachadas == null || fachadas.Count == 0)
        {
            Debug.LogWarning($"[{name}] No hay fachadas cargadas en la lista.");
            return;
        }

        Limpiar();

        System.Random rng = usarSemilla ? new System.Random(semilla) : new System.Random();
        Transform padre = contenedor != null ? contenedor : transform;

        RecalcularPesos();

        int n = puntos.Count;
        bool loop = cerrarLoop && n >= 3;
        int totalTramos = loop ? n : n - 1;

        FachadaOption anterior = null;
        int colocadas = 0;

        for (int i = 0; i < totalTramos; i++)
        {
            Vector3 desde = puntos[i];
            Vector3 hasta = puntos[(i + 1) % n];

            Vector3 dir = (hasta - desde).normalized;

            // El eje que "avanza" por el tramo apunta en dir. Segun ejeAvance, ese eje local
            // del prefab es X (derecha) o Z (adelante).
            Vector3 forwardParaRotar = ejeAvance == EjeAvance.X
                ? Quaternion.Euler(0f, -90f, 0f) * dir
                : dir;
            Quaternion rot = Quaternion.LookRotation(forwardParaRotar, Vector3.up) * Quaternion.Euler(0f, rotacionExtraY, 0f);

            colocadas += GenerarSegmento(rng, desde, hasta, dir, rot, padre, ref anterior);
        }

        Debug.Log($"[{name}] {colocadas} fachadas generadas en {totalTramos} tramo(s).");
    }

    // Arma la lista ordenada de puntos del recorrido. Si 'Puntos De La Ruta' tiene 2 o mas
    // elementos se usa esa (modo multi-tramo); si no, se cae al modo simple Inicio/Final.
    List<Vector3> ConstruirPuntos()
    {
        if (puntosRuta != null && puntosRuta.Count >= 2)
        {
            List<Vector3> resultado = new List<Vector3>(puntosRuta.Count);
            foreach (Transform t in puntosRuta)
            {
                if (t == null)
                {
                    Debug.LogWarning($"[{name}] Hay un elemento vacio en 'Puntos De La Ruta'.");
                    return null;
                }
                resultado.Add(t.position);
            }
            return resultado;
        }

        if (inicio != null && final != null)
            return new List<Vector3> { inicio.position, final.position };

        return null;
    }

    // Antes: Mathf.Max(f.peso, 0.0001f) se recalculaba para TODAS las fachadas cada vez que
    // se elegia una. Ahora se calcula una sola vez por llamada a Generar() y se reutiliza.
    void RecalcularPesos()
    {
        if (_pesosCache == null || _pesosCache.Length != fachadas.Count)
            _pesosCache = new float[fachadas.Count];

        _pesoTotalCache = 0f;
        for (int i = 0; i < fachadas.Count; i++)
        {
            _pesosCache[i] = Mathf.Max(fachadas[i].peso, 0.0001f);
            _pesoTotalCache += _pesosCache[i];
        }
    }

    FachadaOption ElegirAleatoria(System.Random rng, FachadaOption anterior)
    {
        if (fachadas.Count == 1) return fachadas[0];

        FachadaOption resultado = null;
        for (int intento = 0; intento < 10; intento++)
        {
            resultado = ElegirPorPeso(rng);
            if (!evitarRepetirVecino || resultado != anterior) break;
        }
        return resultado;
    }

    FachadaOption ElegirPorPeso(System.Random rng)
    {
        float valor = (float)(rng.NextDouble() * _pesoTotalCache);
        float acumulado = 0f;
        for (int i = 0; i < fachadas.Count; i++)
        {
            acumulado += _pesosCache[i];
            if (valor <= acumulado) return fachadas[i];
        }
        return fachadas[fachadas.Count - 1];
    }

    // Rellena con fachadas el tramo recto entre 'desde' y 'hasta'. 'anterior' se pasa por
    // referencia para que 'evitarRepetirVecino' tambien considere la ultima fachada del
    // tramo previo (asi no se repite justo en el cambio de tramo).
    int GenerarSegmento(System.Random rng, Vector3 desde, Vector3 hasta, Vector3 dir, Quaternion rot, Transform padre, ref FachadaOption anterior)
    {
        float totalDist = Vector3.Distance(desde, hasta);
        float recorrido = 0f;
        int seguridad = 0;
        int colocadas = 0;

        while (recorrido < totalDist - 0.001f && seguridad < 500)
        {
            seguridad++;
            FachadaOption elegida = ElegirAleatoria(rng, anterior);
            if (elegida == null || elegida.prefab == null) break;

            Vector3 posTentativa = desde + dir * recorrido;
            GameObject go = InstanciarPrefab(elegida.prefab, posTentativa, rot, padre);

            Bounds b = ObtenerBoundsCombinados(go);
            if (b.size.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning($"[{name}] {elegida.prefab.name} no tiene Renderer/Collider detectable, se omite.");
                DestruirInmediato(go);
                continue;
            }

            float minT, maxT;
            MedirExtensionEnDireccion(b, desde, dir, out minT, out maxT);
            float anchoReal = elegida.anchoManual > 0f ? elegida.anchoManual : (maxT - minT);

            float espacioRestante = totalDist - recorrido;
            float anchoUsado = anchoReal;
            bool ajustar = false;

            if (anchoReal + espacioEntreFachadas > espacioRestante + 0.001f)
            {
                if (!ajustarUltimaFachada)
                {
                    DestruirInmediato(go);
                    break;
                }
                anchoUsado = Mathf.Max(espacioRestante, 0.01f);
                ajustar = true;
            }

            if (ajustar && anchoReal > 0.0001f)
            {
                float factor = anchoUsado / anchoReal;
                Vector3 s = go.transform.localScale;
                go.transform.localScale = ejeAvance == EjeAvance.X
                    ? new Vector3(s.x * factor, s.y, s.z)
                    : new Vector3(s.x, s.y, s.z * factor);

                // El reescalado cambia el tamaño real del bounding box: aca si hace falta
                // volver a medirlo contra el Renderer, no se puede deducir matematicamente.
                b = ObtenerBoundsCombinados(go);
                MedirExtensionEnDireccion(b, desde, dir, out minT, out maxT);
            }

            // Alinear el borde trasero de la fachada (minT) exactamente en 'recorrido',
            // sin importar donde este el pivote del prefab.
            float offset = recorrido - minT;
            go.transform.position += dir * offset;

            // Anclar al piso: el paso anterior fue solo una TRASLACION (no cambia escala
            // ni rotacion), asi que el bounding box que ya tenemos en 'b' se puede trasladar
            // matematicamente sumandole el mismo offset, en vez de volver a consultar los
            // Renderer del objeto (GetComponentsInChildren es la parte mas cara del bucle).
            if (anclarAlPiso)
            {
                float minYFinal = b.min.y + offset * dir.y;
                float offsetY = desde.y - minYFinal;
                go.transform.position += Vector3.up * offsetY;
            }

            go.name = elegida.prefab.name;

            recorrido += anchoUsado + espacioEntreFachadas;
            anterior = elegida;
            colocadas++;
        }

        return colocadas;
    }

    Bounds ObtenerBoundsCombinados(GameObject go)
    {
        Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        Collider[] colliders = go.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds b = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++) b.Encapsulate(colliders[i].bounds);
            return b;
        }

        return new Bounds(go.transform.position, Vector3.zero);
    }

    void MedirExtensionEnDireccion(Bounds b, Vector3 origen, Vector3 dir, out float minT, out float maxT)
    {
        Vector3 c = b.center;
        Vector3 e = b.extents;
        minT = float.MaxValue;
        maxT = float.MinValue;

        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);

            float t = Vector3.Dot(corner - origen, dir);
            if (t < minT) minT = t;
            if (t > maxT) maxT = t;
        }
    }

    GameObject InstanciarPrefab(GameObject prefab, Vector3 pos, Quaternion rot, Transform padre)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject instancia = (GameObject)PrefabUtility.InstantiatePrefab(prefab, padre);
            instancia.transform.position = pos;
            instancia.transform.rotation = rot;
            return instancia;
        }
#endif
        return Instantiate(prefab, pos, rot, padre);
    }

    void DestruirInmediato(GameObject go)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) { DestroyImmediate(go); return; }
#endif
        Destroy(go);
    }

    [ContextMenu("Limpiar Fachadas")]
    public void Limpiar()
    {
        Transform padre = contenedor != null ? contenedor : transform;
        for (int i = padre.childCount - 1; i >= 0; i--)
        {
            Transform hijo = padre.GetChild(i);
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(hijo.gameObject);
                continue;
            }
#endif
            Destroy(hijo.gameObject);
        }
    }

    void OnDrawGizmos()
    {
        List<Vector3> puntos = ConstruirPuntos();
        if (puntos == null || puntos.Count < 2) return;

        int n = puntos.Count;
        bool loop = cerrarLoop && n >= 3;
        int totalTramos = loop ? n : n - 1;

        // Linea + flecha de direccion en cada tramo, asi se ve de un vistazo hacia
        // donde "avanza" cada calle sin tener que generar nada todavia.
        for (int i = 0; i < totalTramos; i++)
        {
            Vector3 desde = puntos[i];
            Vector3 hasta = puntos[(i + 1) % n];

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(desde, hasta);
            DibujarFlecha((desde + hasta) * 0.5f, (hasta - desde).normalized, Color.yellow);
        }

        // Esfera en cada punto (celeste = punto intermedio, amarillo = extremo) + numero
        // de orden, para confirmar antes de generar que arrastraste los puntos en el
        // orden correcto.
        for (int i = 0; i < n; i++)
        {
            bool esIntermedio = loop || (i > 0 && i < n - 1);
            Gizmos.color = esIntermedio ? Color.cyan : Color.yellow;
            Gizmos.DrawSphere(puntos[i], 0.3f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = esIntermedio ? Color.cyan : Color.white;
            UnityEditor.Handles.Label(puntos[i] + Vector3.up * 0.6f, i.ToString());
#endif
        }
    }

    void DibujarFlecha(Vector3 posicion, Vector3 dir, Color color)
    {
        if (dir.sqrMagnitude < 0.0001f) return;

        Gizmos.color = color;
        Vector3 lado = Vector3.Cross(dir, Vector3.up).normalized * 0.25f;
        Vector3 punta = posicion + dir * 0.3f;
        Gizmos.DrawLine(posicion - dir * 0.3f, punta);
        Gizmos.DrawLine(punta, punta - dir * 0.35f + lado);
        Gizmos.DrawLine(punta, punta - dir * 0.35f - lado);
    }
}