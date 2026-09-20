using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Rellena aleatoriamente los tramos de una calle/plaza con prefabs de fachadas, y coloca
/// automaticamente piezas de ESQUINA en los vertices de la ruta donde la direccion cambia.
///
/// El ancho de cada fachada (y cuanto "come" cada esquina de los tramos vecinos) se MIDE
/// AUTOMATICAMENTE de su bounding box real (Renderer), asi que no hace falta calcularlo a mano.
///
/// Dos formas de definir el recorrido:
///  - Modo simple: un solo tramo entre "Inicio" y "Final" (sin esquinas, son solo 2 puntos).
///  - Modo ruta: cargá 2 o mas puntos en "Puntos De La Ruta" (en el orden del recorrido) para
///    una calle con varios tramos rectos, opcionalmente cerrada en loop (plaza/manzana). Cada
///    vertice INTERIOR (o todos si "Cerrar Loop" esta activo) donde la direccion cambia mas de
///    "Angulo Minimo Esquina" grados recibe automaticamente una pieza de "Fachadas Esquina".
///
/// FIX: la medicion del ancho de cada fachada ahora se hace con el prefab en rotacion
/// identidad (antes de orientarlo hacia la calle). Renderer.bounds SIEMPRE devuelve un AABB
/// alineado a los ejes del MUNDO, nunca a los ejes locales del objeto: si se medía DESPUES de
/// rotar (como antes), en cualquier calle/esquina que no cayera justo en un multiplo de 90
/// grados ese bounding box quedaba mas grande que la malla real, y eso generaba huecos entre
/// fachadas y un recorte de esquinas incorrecto en calles diagonales. Midiendo primero (sin
/// rotar) y rotando recien despues, el resultado es exacto sin importar el angulo de la calle.
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

        [Tooltip("Correccion de rotacion en Y SOLO para ESTE prefab, ademas de 'Rotacion Extra " +
                 "Y' (o 'Rotacion Extra Y Esquina' si esta en la lista de esquinas). Usalo cuando " +
                 "este prefab en particular quedo con una rotacion propia distinta a las demas " +
                 "(por ejemplo porque en la escena original cada casa se roto a mano de forma " +
                 "distinta antes de convertirla en prefab, y 'Limpiar Transform Raiz' preservo " +
                 "esa rotacion tal cual). Probá de a 90 grados hasta que ESTE prefab quede " +
                 "mirando para el mismo lado que los demas.")]
        public float rotacionYIndividual = 0f;

        [Tooltip("SOLO para fachadas de esquina. Dejalo en 0 para medir automaticamente cuanto " +
                 "tapa esta esquina del tramo que LLEGA (dirIn), con el bounding box de la pieza. " +
                 "Como una pieza diagonal/en L no es una caja perfecta, el bounding box a veces " +
                 "se queda corto o se pasa; si el auto-calculo no encaja bien, poné aca los " +
                 "metros reales que tapa hacia atras como respaldo.")]
        public float consumoManualAtras = 0f;

        [Tooltip("Igual que 'Consumo Manual Atras' pero para el tramo que SALE de la esquina " +
                 "(dirOut). Dejalo en 0 para medir automaticamente.")]
        public float consumoManualAdelante = 0f;
    }

    // Pesos precalculados para el sorteo ponderado de una lista de fachadas (recta o esquina).
    class PesosCache
    {
        public float[] pesos;
        public float total;
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

    [Header("Fachadas disponibles (tramos rectos)")]
    [Tooltip("Fachadas rectas normales. Las piezas de esquina/diagonales van en la lista de " +
             "abajo, no aca -- se colocan solas en los vertices donde la ruta gira.")]
    public List<FachadaOption> fachadas = new List<FachadaOption>();

    [Header("Fachadas de esquina (se colocan solas donde la ruta gira)")]
    [Tooltip("Piezas diagonales/de esquina (casa6Diagonal, casa8Diagonal, etc). Se colocan " +
             "automaticamente en cada vertice interior de la ruta donde la direccion cambia " +
             "mas de 'Angulo Minimo Esquina' grados (o en TODOS los vertices si 'Cerrar Loop' " +
             "esta activo). Si esta lista queda vacia, no se coloca ninguna esquina.")]
    public List<FachadaOption> fachadasEsquina = new List<FachadaOption>();

    [Tooltip("Rotacion extra en Y SOLO para las piezas de esquina, ademas de la rotacion que ya " +
             "las orienta hacia la bisectriz del giro. Anda probando 0 / 45 / 90 / 135 / 180... " +
             "hasta que la pieza quede bien encajada en el angulo real de tus calles -- esto " +
             "depende de como esta modelada cada malla, no se puede adivinar solo.")]
    public float rotacionExtraYEsquina = 0f;

    [Tooltip("Angulo minimo (grados) para considerar que un vertice es una esquina real. Si el " +
             "cambio de direccion es menor a esto, se trata como una recta y no se coloca " +
             "ninguna pieza de esquina ahi.")]
    public float anguloMinimoEsquina = 5f;

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

    [Tooltip("Rotacion extra en Y para terminar de alinear la fachada recta (probá 0 / 90 / 180 / 270 hasta que quede bien).")]
    public float rotacionExtraY = 0f;

    [Tooltip("Evita que la misma fachada salga dos veces seguida.")]
    public bool evitarRepetirVecino = true;

    [Tooltip("Si esta activo, cada fachada (y cada esquina) se reacomoda en Y para que su base " +
             "(el punto mas bajo de su malla) quede apoyada en el suelo, sin importar la altura " +
             "del prefab ni donde tenga el pivote.")]
    public bool anclarAlPiso = true;

    [Tooltip("Usar una semilla fija para que la calle se genere siempre igual (util para depurar).")]
    public bool usarSemilla = false;
    public int semilla = 0;
    public bool generarAlIniciar = true;

    PesosCache _pesosFachadas;
    PesosCache _pesosEsquinas;

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

        ValidarPrefabs();
        Limpiar();

        System.Random rng = usarSemilla ? new System.Random(semilla) : new System.Random();
        Transform padre = contenedor != null ? contenedor : transform;

        _pesosFachadas = CalcularPesos(fachadas);
        bool hayEsquinas = fachadasEsquina != null && fachadasEsquina.Count > 0;
        if (hayEsquinas) _pesosEsquinas = CalcularPesos(fachadasEsquina);

        int n = puntos.Count;
        bool loop = cerrarLoop && n >= 3;
        int totalTramos = loop ? n : n - 1;

        // 1) Colocar esquinas primero. Cada esquina "come" un pedazo del tramo que llega y del
        // que sale desde ese vertice; guardamos cuanto para recortar los tramos rectos despues.
        var trimSalida = new Dictionary<int, float>();  // vertice -> cuanto se consume al INICIO del tramo que SALE de ahi
        var trimLlegada = new Dictionary<int, float>(); // vertice -> cuanto se consume al FINAL del tramo que LLEGA ahi
        int esquinasColocadas = 0;

        if (hayEsquinas)
        {
            for (int v = 0; v < n; v++)
            {
                bool tieneVecinoAtras = loop || v > 0;
                bool tieneVecinoAdelante = loop || v < n - 1;
                if (!tieneVecinoAtras || !tieneVecinoAdelante) continue; // extremo de una ruta abierta: no hay esquina

                int vAnterior = (v - 1 + n) % n;
                int vSiguiente = (v + 1) % n;

                Vector3 dirIn = (puntos[v] - puntos[vAnterior]).normalized;
                Vector3 dirOut = (puntos[vSiguiente] - puntos[v]).normalized;

                float anguloGrados = Vector3.Angle(dirIn, dirOut);
                if (anguloGrados < anguloMinimoEsquina) continue; // practicamente recto, no hace falta pieza

                if (ColocarEsquina(rng, puntos[v], dirIn, dirOut, padre, out float consumidoAtras, out float consumidoAdelante))
                {
                    trimLlegada[v] = consumidoAtras;
                    trimSalida[v] = consumidoAdelante;
                    esquinasColocadas++;
                }
            }
        }

        // 2) Rellenar cada tramo recto, recortando en las puntas el espacio que ya ocupan
        // las esquinas de sus dos extremos.
        FachadaOption anterior = null;
        int colocadas = 0;

        for (int i = 0; i < totalTramos; i++)
        {
            int vDesde = i;
            int vHasta = (i + 1) % n;

            Vector3 desde = puntos[vDesde];
            Vector3 hasta = puntos[vHasta];
            Vector3 dir = (hasta - desde).normalized;

            float recorte1 = trimSalida.TryGetValue(vDesde, out float t1) ? t1 : 0f;
            float recorte2 = trimLlegada.TryGetValue(vHasta, out float t2) ? t2 : 0f;

            Vector3 desdeAjustado = desde + dir * recorte1;
            Vector3 hastaAjustado = hasta - dir * recorte2;

            if (Vector3.Dot(hastaAjustado - desdeAjustado, dir) <= 0f)
            {
                Debug.LogWarning($"[{name}] El tramo {vDesde}->{vHasta} es mas corto que el " +
                    "espacio que ocupan las esquinas de sus dos extremos: no entra ninguna " +
                    "fachada recta ahi.");
                continue;
            }

            Vector3 forwardParaRotar = ejeAvance == EjeAvance.X
                ? Quaternion.Euler(0f, -90f, 0f) * dir
                : dir;
            Quaternion rot = Quaternion.LookRotation(forwardParaRotar, Vector3.up) * Quaternion.Euler(0f, rotacionExtraY, 0f);

            colocadas += GenerarSegmento(rng, desdeAjustado, hastaAjustado, dir, rot, padre, ref anterior);
        }

        Debug.Log($"[{name}] {colocadas} fachadas rectas + {esquinasColocadas} esquinas generadas en {totalTramos} tramo(s).");
    }

    // Coloca una pieza de esquina en 'posicion', orientada hacia la bisectriz del giro entre
    // dirIn (direccion de llegada) y dirOut (direccion de salida). Devuelve cuanto de cada
    // tramo vecino queda "tapado" por la pieza, para que GenerarSegmento no rellene encima.
    bool ColocarEsquina(System.Random rng, Vector3 posicion, Vector3 dirIn, Vector3 dirOut, Transform padre, out float consumidoAtras, out float consumidoAdelante)
    {
        consumidoAtras = 0f;
        consumidoAdelante = 0f;

        FachadaOption elegida = ElegirAleatoria(rng, fachadasEsquina, _pesosEsquinas, null);
        if (elegida == null || elegida.prefab == null) return false;

        // Bisectriz del giro: a mitad de camino entre "por donde vengo" y "por donde sigo".
        // Si el giro es casi de 180 grados (dirIn ~ -dirOut) la bisectriz degenera a cero;
        // en ese caso raro se usa dirOut como respaldo para no romper la rotacion.
        Vector3 bisectriz = dirIn + dirOut;
        Vector3 dirParaRotar = bisectriz.sqrMagnitude > 0.0001f ? bisectriz.normalized : dirOut;

        Vector3 forwardParaRotar = ejeAvance == EjeAvance.X
            ? Quaternion.Euler(0f, -90f, 0f) * dirParaRotar
            : dirParaRotar;
        Quaternion rot = Quaternion.LookRotation(forwardParaRotar, Vector3.up) * Quaternion.Euler(0f, rotacionExtraYEsquina, 0f);

        // Medimos la malla real con el objeto SIN rotar y en el origen. Renderer.bounds siempre
        // devuelve un AABB alineado a los ejes del MUNDO: si midieramos DESPUES de aplicar 'rot'
        // (como se hacia antes), en cualquier esquina que no caiga justo en un multiplo de 90
        // grados ese AABB queda mas grande que la malla real, y 'consumidoAtras'/'consumidoAdelante'
        // salen mal -- eso era lo que comia de mas (o de menos) el espacio de los tramos vecinos
        // en calles diagonales.
        GameObject go = InstanciarPrefab(elegida.prefab, Vector3.zero, Quaternion.identity, padre);
        Bounds bLocal = ObtenerBoundsCombinados(go);
        if (bLocal.size.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning($"[{name}] {elegida.prefab.name} (esquina) no tiene Renderer/Collider detectable, se omite.");
            DestruirInmediato(go);
            return false;
        }

        // Ahora rotamos las esquinas REALES de ese bounding box (no las de un AABB ya inflado) y
        // las proyectamos sobre dirIn/dirOut para saber cuanto tapa la pieza en cada tramo vecino.
        MedirExtensionRotada(bLocal, rot, posicion, dirIn, out float minIn, out _);
        consumidoAtras = Mathf.Max(0f, -minIn);

        MedirExtensionRotada(bLocal, rot, posicion, dirOut, out _, out float maxOut);
        consumidoAdelante = Mathf.Max(0f, maxOut);

        // Una rotacion alrededor de Y nunca cambia la coordenada Y de ningun punto, asi que el
        // punto mas bajo de la malla en mundo sigue siendo 'posicion.y + bLocal.min.y' aunque ya
        // hayamos rotado -- no hace falta volver a medir para anclar al piso.
        go.transform.position = anclarAlPiso
            ? new Vector3(posicion.x, posicion.y - bLocal.min.y, posicion.z)
            : posicion;
        go.transform.rotation = rot;

        go.name = elegida.prefab.name + "_Esquina";
        return true;
    }

    // Chequea, ANTES de generar nada, que el root de cada prefab asignado este realmente en
    // 0/0/0 - identidad - 1/1/1. Si algun prefab quedo con un transform "sucio" en el root
    // (por ejemplo por haber tocado el Inspector del prefab a mano despues de correr
    // LimpiarTransformFachadas), el spawner lo va a rotar/ubicar mal sin ningun error visible
    // en consola -- esto lo saca a la luz con nombre y apellido.
    void ValidarPrefabs()
    {
        void Chequear(List<FachadaOption> lista, string nombreLista)
        {
            if (lista == null) return;
            foreach (FachadaOption op in lista)
            {
                if (op == null || op.prefab == null) continue;
                Transform t = op.prefab.transform;
                bool limpio = t.position == Vector3.zero
                    && t.rotation == Quaternion.identity
                    && t.localScale == Vector3.one;

                if (!limpio)
                {
                    Debug.LogWarning($"[{name}] El prefab '{op.prefab.name}' (en {nombreLista}) " +
                        $"tiene el root sucio: pos={t.position} rot={t.rotation.eulerAngles} " +
                        $"scale={t.localScale}. Va a rotarse/ubicarse mal porque el spawner " +
                        "asume que el root esta en 0/0/1. Corre Tools > Fachadas > Limpiar " +
                        "Transform Raiz sobre este prefab (o revisalo a mano si ya paso por ahi).");
                }
            }
        }

        Chequear(fachadas, "Fachadas");
        Chequear(fachadasEsquina, "Fachadas Esquina");
    }

    // Arma la lista ordenada de puntos del recorrido. Si 'Puntos De La Ruta' tiene 2 o mas
    // elementos se usa esa (modo multi-tramo); si no, se cae al modo simple Inicio/Final.
    List<Vector3> ConstruirPuntos()
    {
        if (puntosRuta != null && puntosRuta.Count >= 2)
        {
            List<Vector3> resultado = new List<Vector3>(puntosRuta.Count);
            for (int i = 0; i < puntosRuta.Count; i++)
            {
                Transform t = puntosRuta[i];
                if (t == null)
                {
                    Debug.LogWarning($"[{name}] El elemento {i} de 'Puntos De La Ruta' esta " +
                        "vacio (revisa la lista en el Inspector: hay que completarlo o borrarlo).");
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

    // Pesos precalculados para el sorteo ponderado de CUALQUIER lista de fachadas (recta o
    // esquina), para no recalcularlos en cada pieza colocada.
    PesosCache CalcularPesos(List<FachadaOption> lista)
    {
        var cache = new PesosCache { pesos = new float[lista.Count] };
        for (int i = 0; i < lista.Count; i++)
        {
            cache.pesos[i] = Mathf.Max(lista[i].peso, 0.0001f);
            cache.total += cache.pesos[i];
        }
        return cache;
    }

    FachadaOption ElegirAleatoria(System.Random rng, List<FachadaOption> lista, PesosCache cache, FachadaOption anterior)
    {
        if (lista == null || lista.Count == 0) return null;
        if (lista.Count == 1) return lista[0];

        FachadaOption resultado = null;
        for (int intento = 0; intento < 10; intento++)
        {
            resultado = ElegirPorPeso(rng, lista, cache);
            if (!evitarRepetirVecino || resultado != anterior) break;
        }
        return resultado;
    }

    FachadaOption ElegirPorPeso(System.Random rng, List<FachadaOption> lista, PesosCache cache)
    {
        float valor = (float)(rng.NextDouble() * cache.total);
        float acumulado = 0f;
        for (int i = 0; i < lista.Count; i++)
        {
            acumulado += cache.pesos[i];
            if (valor <= acumulado) return lista[i];
        }
        return lista[lista.Count - 1];
    }

    // Rellena con fachadas el tramo recto entre 'desde' y 'hasta' (ya recortado por las
    // esquinas de sus extremos). 'anterior' se pasa por referencia para que
    // 'evitarRepetirVecino' tambien considere la ultima fachada del tramo previo.
    int GenerarSegmento(System.Random rng, Vector3 desde, Vector3 hasta, Vector3 dir, Quaternion rot, Transform padre, ref FachadaOption anterior)
    {
        float totalDist = Vector3.Distance(desde, hasta);
        float recorrido = 0f;
        int seguridad = 0;
        int colocadas = 0;

        while (recorrido < totalDist - 0.001f && seguridad < 500)
        {
            seguridad++;
            FachadaOption elegida = ElegirAleatoria(rng, fachadas, _pesosFachadas, anterior);
            if (elegida == null || elegida.prefab == null) break;

            // Medimos la malla real con el objeto SIN rotar y en el origen (mismo motivo que en
            // ColocarEsquina: Renderer.bounds despues de rotar viene inflado en cualquier calle
            // que no sea perfectamente horizontal/vertical, y eso generaba los huecos). Al estar
            // sin rotar, el ancho a lo largo del eje de avance (X o Z) se lee directo del
            // bounding box, sin proyectar nada.
            GameObject go = InstanciarPrefab(elegida.prefab, Vector3.zero, Quaternion.identity, padre);
            Bounds bLocal = ObtenerBoundsCombinados(go);

            if (bLocal.size.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning($"[{name}] {elegida.prefab.name} no tiene Renderer/Collider detectable, se omite.");
                DestruirInmediato(go);
                continue;
            }

            float minLocal = ejeAvance == EjeAvance.X ? bLocal.min.x : bLocal.min.z;
            float maxLocal = ejeAvance == EjeAvance.X ? bLocal.max.x : bLocal.max.z;
            float anchoReal = elegida.anchoManual > 0f ? elegida.anchoManual : (maxLocal - minLocal);

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

                // El reescalado es sobre el pivote (0,0,0 local): el rango tambien se escala
                // linealmente, no hace falta volver a consultar los Renderer (mas barato).
                minLocal *= factor;
                maxLocal *= factor;
            }

            // Alinear el borde trasero de la fachada (minLocal) exactamente en 'recorrido', y
            // recien ahora aplicar la rotacion final que la orienta hacia la calle.
            Vector3 posicion = desde + dir * (recorrido - minLocal);

            if (anclarAlPiso)
            {
                // La rotacion final es solo en Y, que nunca mueve la coordenada Y de ningun
                // punto: el punto mas bajo de la malla queda en 'posicion.y + bLocal.min.y' se
                // rote o no, asi que se puede anclar directo sin volver a medir.
                posicion.y = desde.y - bLocal.min.y;
            }

            go.transform.position = posicion;
            go.transform.rotation = rot;
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

    // Proyecta las 8 esquinas REALES de un bounding box medido en rotacion identidad
    // (boundsLocal), rotandolas con 'rot' y ubicandolas en 'origen', sobre la direccion 'dir'.
    // A diferencia de pedirle a Unity el Renderer.bounds DESPUES de rotar el objeto (que
    // siempre devuelve un AABB alineado a los ejes del MUNDO, y por lo tanto mas grande que la
    // malla real en cualquier angulo que no sea multiplo de 90 grados), esto rota las esquinas
    // de la malla real, asi que el resultado es exacto sin importar el angulo de la calle.
    void MedirExtensionRotada(Bounds boundsLocal, Quaternion rot, Vector3 origen, Vector3 dir, out float minT, out float maxT)
    {
        Vector3 c = boundsLocal.center;
        Vector3 e = boundsLocal.extents;
        minT = float.MaxValue;
        maxT = float.MinValue;

        for (int i = 0; i < 8; i++)
        {
            Vector3 cornerLocal = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);

            Vector3 cornerMundo = origen + rot * cornerLocal;
            float t = Vector3.Dot(cornerMundo - origen, dir);
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

        // Esfera en cada punto: magenta = ahi va a ir una pieza de esquina (el angulo de giro
        // supera 'Angulo Minimo Esquina'), celeste = vertice interior pero practicamente recto,
        // amarillo = extremo de una ruta abierta (sin esquina posible).
        for (int i = 0; i < n; i++)
        {
            bool tieneVecinoAtras = loop || i > 0;
            bool tieneVecinoAdelante = loop || i < n - 1;
            bool esVertice = tieneVecinoAtras && tieneVecinoAdelante;

            Color color = Color.yellow;
            if (esVertice)
            {
                int vAnterior = (i - 1 + n) % n;
                int vSiguiente = (i + 1) % n;
                Vector3 dirIn = (puntos[i] - puntos[vAnterior]).normalized;
                Vector3 dirOut = (puntos[vSiguiente] - puntos[i]).normalized;
                float angulo = Vector3.Angle(dirIn, dirOut);
                color = angulo >= anguloMinimoEsquina ? Color.magenta : Color.cyan;
            }

            Gizmos.color = color;
            Gizmos.DrawSphere(puntos[i], 0.3f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = color;
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