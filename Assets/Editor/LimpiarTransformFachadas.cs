using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

/// <summary>
/// Herramienta de Editor para dejar "limpio" (Position 0,0,0 / Rotation 0,0,0 / Scale 1,1,1)
/// el transform raiz de varios prefabs de fachada a la vez, SIN que cambien de aspecto.
///
/// v3 (FIX decoraciones): en el caso "el mesh esta directamente en el root", la version
/// anterior le pasaba el transform del root (ej. escala 100 + rotacion) a un hijo nuevo
/// "Malla", pero las DECORACIONES que ya eran hijos del root (rejas, puertas, macetas...)
/// se quedaban colgando del root ya limpio. Como perdian la escala/rotacion de su padre,
/// quedaban minusculas o mal ubicadas, escondidas dentro del mesh: parecia que habian
/// desaparecido. Ahora esos hijos se sacan preservando su posicion ABSOLUTA, se limpia el
/// root, y se vuelven a meter -> el aspecto final es identico al original.
///
/// Ademas, el mesh se mueve a "Malla" copiando los componentes completos (MeshFilter,
/// MeshRenderer y Colliders) con ComponentUtility, asi no se pierden sombras, light probes,
/// motion vectors, etc.
///
/// Casos que cubre:
///  - A) El root tiene el Mesh directamente: crea hijo "Malla" con mesh/renderer/colliders
///       y el transform original del root; los hijos existentes conservan su aspecto.
///  - B) El root no tiene mesh pero tiene hijos: reemparenta preservando el transform absoluto.
///  - Avisa por consola si detecta escala espejada (numero impar de ejes negativos).
///
/// USO:
/// 1) Selecciona en la ventana Project los prefabs que queres corregir.
/// 2) Menu Tools > Fachadas > Limpiar Transform Raiz (prefabs seleccionados).
/// 3) Mira la consola.
///
/// IMPORTANTE: modifica los assets directamente. Si un prefab ya fue limpiado con la version
/// anterior, las decoraciones ya quedaron con transforms rotos en el asset: restauralo desde
/// control de versiones/backup (o volve a armar el prefab desde la escena) y recien ahi
/// corre esta version.
/// </summary>
public class LimpiarTransformFachadas
{
    [MenuItem("Tools/Fachadas/Limpiar Transform Raiz (prefabs seleccionados)")]
    static void Limpiar()
    {
        Object[] seleccionados = Selection.GetFiltered(typeof(GameObject), SelectionMode.Assets);

        if (seleccionados.Length == 0)
        {
            Debug.LogWarning("No hay nada seleccionado en la ventana Project. " +
                              "Selecciona uno o varios prefabs de fachada primero.");
            return;
        }

        int corregidos = 0;
        int saltados = 0;

        foreach (Object obj in seleccionados)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
            {
                continue; // no es un prefab, se ignora sin contar como "salteado"
            }

            GameObject raiz = PrefabUtility.LoadPrefabContents(path);

            bool tocado = ProcesarRaiz(raiz, obj.name);

            if (tocado)
            {
                PrefabUtility.SaveAsPrefabAsset(raiz, path);
                corregidos++;
            }
            else
            {
                saltados++;
            }

            PrefabUtility.UnloadPrefabContents(raiz);
        }

        Debug.Log($"[LimpiarTransformFachadas] Listo. Corregidos: {corregidos} | Sin tocar: {saltados}");
    }

    static bool ProcesarRaiz(GameObject raiz, string nombre)
    {
        bool yaLimpio = raiz.transform.position == Vector3.zero
            && raiz.transform.rotation == Quaternion.identity
            && raiz.transform.localScale == Vector3.one;

        if (yaLimpio)
        {
            Debug.Log($"{nombre}: ya estaba limpio, no se toca.");
            AdvertirSiEstaEspejado(raiz, nombre);
            return false;
        }

        MeshFilter mfRoot = raiz.GetComponent<MeshFilter>();
        MeshRenderer mrRoot = raiz.GetComponent<MeshRenderer>();
        bool tieneMeshEnRoot = mfRoot != null && mrRoot != null;

        if (raiz.transform.localScale != Vector3.one && raiz.transform.childCount > 0)
        {
            // Escala uniforme (ej. 100,100,100) no deforma nada. Solo avisamos si es rara.
            Vector3 s = raiz.transform.localScale;
            bool uniforme = Mathf.Approximately(s.x, s.y) && Mathf.Approximately(s.y, s.z);
            if (!uniforme)
            {
                Debug.LogWarning($"{nombre}: el root tiene escala NO uniforme {s}. Los hijos se " +
                    "reemparentan preservando el aspecto, pero revisa el resultado por si " +
                    "aparece alguna deformacion (shear).");
            }
        }

        // Hijos directos ORIGINALES (decoraciones, etc.), antes de crear nada nuevo.
        var hijosOriginales = new List<Transform>();
        for (int i = 0; i < raiz.transform.childCount; i++)
            hijosOriginales.Add(raiz.transform.GetChild(i));

        // 1) Sacar los hijos conservando su transform ABSOLUTO.
        foreach (Transform hijo in hijosOriginales)
            hijo.SetParent(null, true);

        // 2) Si el mesh esta en el root, pasarlo (completo) a un hijo "Malla" que se queda
        //    con el transform original del root.
        if (tieneMeshEnRoot)
        {
            Vector3 posOriginal = raiz.transform.position;
            Quaternion rotOriginal = raiz.transform.rotation;
            Vector3 escalaOriginal = raiz.transform.localScale;

            GameObject malla = new GameObject("Malla");
            malla.layer = raiz.layer;
            malla.tag = raiz.tag;
            malla.transform.SetParent(raiz.transform, false);

            // Copia COMPLETA de los componentes (sombras, probes, motion vectors, etc.).
            CopiarComponente(mfRoot, malla);
            CopiarComponente(mrRoot, malla);

            Collider[] colliders = raiz.GetComponents<Collider>();
            foreach (Collider c in colliders)
                CopiarComponente(c, malla);

            malla.transform.localPosition = posOriginal;
            malla.transform.localRotation = rotOriginal;
            malla.transform.localScale = escalaOriginal;

            foreach (Collider c in colliders)
                Object.DestroyImmediate(c);
            Object.DestroyImmediate(mrRoot);
            Object.DestroyImmediate(mfRoot);
        }

        // 3) Limpiar el root.
        raiz.transform.position = Vector3.zero;
        raiz.transform.rotation = Quaternion.identity;
        raiz.transform.localScale = Vector3.one;

        // 4) Devolver los hijos originales: mismo aspecto absoluto que antes.
        foreach (Transform hijo in hijosOriginales)
            hijo.SetParent(raiz.transform, true);

        AdvertirSiEstaEspejado(raiz, nombre);

        Debug.Log($"{nombre}: corregido ({hijosOriginales.Count} hijo(s) original(es) preservado(s)).");
        return true;
    }

    static void CopiarComponente(Component origen, GameObject destino)
    {
        if (ComponentUtility.CopyComponent(origen))
            ComponentUtility.PasteComponentAsNew(destino);
    }

    // Un numero impar de ejes con escala negativa invierte la malla (normales hacia
    // adentro). No se arregla solo -- puede ser intencional -- pero conviene saberlo.
    static void AdvertirSiEstaEspejado(GameObject raiz, string nombre)
    {
        foreach (MeshRenderer mr in raiz.GetComponentsInChildren<MeshRenderer>(true))
        {
            Vector3 s = mr.transform.lossyScale;
            int negativos = (s.x < 0 ? 1 : 0) + (s.y < 0 ? 1 : 0) + (s.z < 0 ? 1 : 0);
            if (negativos % 2 == 1)
            {
                Debug.LogWarning($"{nombre}/{mr.name}: tiene escala espejada (numero impar " +
                    "de ejes negativos). Puede verse con las normales invertidas. Conviene " +
                    "corregirlo en Blender (aplicar escala/rotacion) antes de exportar.");
            }
        }
    }
}