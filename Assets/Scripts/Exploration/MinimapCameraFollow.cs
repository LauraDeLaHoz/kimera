using UnityEngine;

/// <summary>
/// Pon este script en la cámara del minimapa.
/// La deja mirando hacia abajo y la mueve siguiendo al jugador.
/// </summary>
public class MinimapCameraFollow : MonoBehaviour
{
    [Tooltip("Vacío = busca el objeto con tag Player.")]
    public Transform target;

    [Tooltip("Altura de la cámara sobre el jugador (que quede por encima de los edificios).")]
    public float height = 100f;

    private void LateUpdate()
    {
        if (target == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p == null) return;
            target = p.transform;
        }

        transform.position = new Vector3(target.position.x, target.position.y + height, target.position.z);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // mirando hacia abajo, norte arriba
    }
}