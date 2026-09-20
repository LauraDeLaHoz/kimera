using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/// <summary>
/// Herramienta de Editor para dejar "limpio" (Position 0,0,0 / Rotation 0,0,0 / Scale 1,1,1)
/// el transform raiz de varios prefabs de fachada a la vez, SIN que cambien de aspecto.
///
/// v2: a diferencia de la version anterior, ahora TAMBIEN arregla el caso en el que el root
/// no esta limpio pero la malla YA esta movida a un hijo (por ejemplo "Malla" de una pasada
/// anterior de esta misma herramienta, o cualquier otra jerarquia). Antes ese caso se
/// saltaba con un warning de "revisalo a mano" y el root quedaba corrupto -> eso es
/// exactamente lo que le paso a Casa1 y casa2 en este proyecto.
///
/// Como lo hace ahora:
///  - Si el root tiene el Mesh directamente (como antes): crea un hijo "Malla", le copia el
///    mesh/materiales, y le pasa el Position/Rotation/Scale que tenia el root.
///  - Si el root YA tiene hijos (la malla esta mas abajo) pero el root no esta en 0/0/1:
///    reemparenta cada hijo directo a null preservando su transform ABSOLUTO, limpia el
///    root, y los vuelve a poner adentro. El resultado visual es identico, pero ahora el
///    root queda en 0/0/1 de verdad.
///  - Si detecta un numero impar de ejes con escala negativa en alguna malla (mirroring),
///    avisa por consola: no lo arregla solo porque puede ser intencional, pero conviene
///    revisarlo (normales invertidas = se puede ver "al reves" desde ciertos angulos).
///
/// USO:
/// 1) Selecciona en la ventana Project los prefabs que queres corregir.
/// 2) Menu Tools > Fachadas > Limpiar Transform Raiz (prefabs seleccionados).
/// 3) Mira la consola: te dice cuantos corrigio, cuantos ya estaban limpios, y si algo
///    quedo espejado.
///
/// IMPORTANTE: hace una modificacion directa sobre los assets. Antes de correrlo asegurate
/// de tener todo en control de versiones (o backup de la carpeta de prefabs).
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

        MeshFilter mfRoot = raiz.GetComponent<MeshFilter>();
        MeshRenderer mrRoot = raiz.GetComponent<MeshRenderer>();
        bool tieneMeshEnRoot = mfRoot != null && mrRoot != null;

        if (yaLimpio)
        {
            Debug.Log($"{nombre}: ya estaba limpio, no se toca.");
            AdvertirSiEstaEspejado(raiz, nombre);
            return false;
        }

        bool huboCambios = false;

        // Caso A: la malla esta directamente en el root -> comportamiento original,
        // crear un hijo "Malla" y pasarle el mesh + el transform que tenia el root.
        if (tieneMeshEnRoot)
        {
            GameObject malla = new GameObject("Malla");
            malla.transform.SetParent(raiz.transform, false);

            MeshFilter mfNuevo = malla.AddComponent<MeshFilter>();
            mfNuevo.sharedMesh = mfRoot.sharedMesh;
            MeshRenderer mrNuevo = malla.AddComponent<MeshRenderer>();
            mrNuevo.sharedMaterials = mrRoot.sharedMaterials;

            malla.transform.localPosition = raiz.transform.position;
            malla.transform.localRotation = raiz.transform.rotation;
            malla.transform.localScale = raiz.transform.localScale;

            Object.DestroyImmediate(mfRoot);
            Object.DestroyImmediate(mrRoot);

            raiz.transform.position = Vector3.zero;
            raiz.transform.rotation = Quaternion.identity;
            raiz.transform.localScale = Vector3.one;

            huboCambios = true;
        }
        // Caso B (el que antes se saltaba con un warning): el root no esta limpio, pero
        // ya tiene hijos -- la malla vive mas abajo en la jerarquia. Empujamos el transform
        // del root hacia cada hijo directo (preservando su aspecto absoluto) y dejamos el
        // root en 0/0/1.
        else if (raiz.transform.childCount > 0)
        {
            if (raiz.transform.localScale != Vector3.one)
            {
                Debug.LogWarning($"{nombre}: el root tenia escala distinta de 1 ademas de " +
                    "posicion/rotacion. Se reemparento igual preservando el aspecto, pero " +
                    "revisa el resultado por si aparece alguna deformacion (shear).");
            }

            var hijos = new List<Transform>();
            for (int i = 0; i < raiz.transform.childCount; i++)
                hijos.Add(raiz.transform.GetChild(i));

            foreach (Transform hijo in hijos)
                hijo.SetParent(null, true); // true = conserva su transform absoluto

            raiz.transform.position = Vector3.zero;
            raiz.transform.rotation = Quaternion.identity;
            raiz.transform.localScale = Vector3.one;

            foreach (Transform hijo in hijos)
                hijo.SetParent(raiz.transform, true); // reconstruye la jerarquia, mismo aspecto

            huboCambios = true;
        }
        else
        {
            // No tiene mesh en el root ni hijos de donde "sacar" el aspecto visual:
            // no hay nada que preservar, se limpia directo.
            raiz.transform.position = Vector3.zero;
            raiz.transform.rotation = Quaternion.identity;
            raiz.transform.localScale = Vector3.one;
            huboCambios = true;
        }

        AdvertirSiEstaEspejado(raiz, nombre);

        if (huboCambios)
            Debug.Log($"{nombre}: corregido.");

        return huboCambios;
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