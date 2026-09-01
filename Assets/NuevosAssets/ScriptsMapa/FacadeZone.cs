using UnityEngine;
using System.Collections.Generic;

public class FacadeZone : MonoBehaviour
{
    [Header("Puntos que definen la zona")]
    public Transform inicio;
    public Transform final;

    [Header("Fachadas disponibles (arrastra aquí los prefabs)")]
    public GameObject[] fachadas;

    [Header("Configuración")]
    public float separacion = 1f;
    public int maxFachadas = 20;

    [Header("Generación")]
    public bool generarAlIniciar = true;

    [Header("Gizmos")]
    public Color colorLinea = Color.green;

    private void Start()
    {
        if (generarAlIniciar)
        {
            GenerarFachadas();
        }
    }

    [ContextMenu("Generar Fachadas")]
    public void GenerarFachadas()
    {
        LimpiarFachadas();

        if (inicio == null || final == null)
        {
            Debug.LogError("FacadeZone: Falta asignar Inicio o Final.");
            return;
        }

        if (fachadas == null || fachadas.Length == 0)
        {
            Debug.LogError("FacadeZone: No hay fachadas asignadas en el array 'fachadas'.");
            return;
        }

        // --------------------------------------------------
        // DIRECCIÓN
        // --------------------------------------------------

        Vector3 diferencia = final.position - inicio.position;
        diferencia.y = 0f;

        float longitudZona = diferencia.magnitude;

        if (longitudZona <= 0.01f)
        {
            Debug.LogError("FacadeZone: Inicio y Final están demasiado cerca.");
            return;
        }

        Vector3 direccion = diferencia.normalized;
        float angulo = Mathf.Atan2(direccion.z, direccion.x) * Mathf.Rad2Deg;

        float distanciaUsada = 0f;
        int contador = 0;

        // --------------------------------------------------
        // GENERAR
        // --------------------------------------------------

        while (contador < maxFachadas)
        {
            List<GameObject> candidatas = new List<GameObject>();

            foreach (GameObject prefab in fachadas)
            {
                if (prefab == null) continue;

                float ancho = ObtenerAncho(prefab);

                if (distanciaUsada + ancho + separacion <= longitudZona)
                {
                    candidatas.Add(prefab);
                }
            }

            if (candidatas.Count == 0)
            {
                break;
            }

            GameObject elegido = candidatas[Random.Range(0, candidatas.Count)];
            float anchoElegido = ObtenerAncho(elegido);

            // --------------------------------------------------
            // POSICIÓN
            // --------------------------------------------------

            Vector3 posicion = inicio.position + direccion * (distanciaUsada + anchoElegido / 2f);
            posicion.y = inicio.position.y;

            // --------------------------------------------------
            // ROTACIÓN (conserva la rotación original del prefab)
            // --------------------------------------------------

            Vector3 rotacionOriginal = elegido.transform.eulerAngles;

            Quaternion rotacion = Quaternion.Euler(
                rotacionOriginal.x,
                angulo + rotacionOriginal.y,
                rotacionOriginal.z
            );

            // --------------------------------------------------
            // CREAR (sin tocar la escala original)
            // --------------------------------------------------

            GameObject nuevaFachada = Instantiate(elegido, posicion, rotacion, transform);
            nuevaFachada.transform.localScale = elegido.transform.localScale;

            distanciaUsada += anchoElegido + separacion;
            contador++;
        }

        Debug.Log("FacadeZone: " + contador + " fachadas generadas.");
        Debug.Log("Espacio utilizado: " + distanciaUsada.ToString("F1") + " / " + longitudZona.ToString("F1") + " metros.");
    }

    // Calcula el ancho real del prefab (mundo, en X) a partir de sus Renderers,
    // respetando la escala que ya trae el prefab. Así no hay que medirlo a mano.
    private float ObtenerAncho(GameObject prefab)
    {
        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning("FacadeZone: " + prefab.name + " no tiene Renderer, se usa ancho 1.");
            return 1f;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds.size.x;
    }

    [ContextMenu("Limpiar Fachadas")]
    public void LimpiarFachadas()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject hijo = transform.GetChild(i).gameObject;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(hijo);
                continue;
            }
#endif
            Destroy(hijo);
        }
    }

    // ======================================================
    // GIZMOS
    // ======================================================

    private void OnDrawGizmos()
    {
        if (inicio == null || final == null)
            return;

        Gizmos.color = colorLinea;

        Gizmos.DrawLine(inicio.position, final.position);

        Gizmos.DrawSphere(inicio.position, 0.5f);
        Gizmos.DrawSphere(final.position, 0.5f);

        Vector3 altura = Vector3.up * 2f;

        Gizmos.DrawLine(inicio.position, inicio.position + altura);
        Gizmos.DrawLine(final.position, final.position + altura);
    }
}