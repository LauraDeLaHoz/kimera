using UnityEngine;

// Adjuntar al prefab DamageNumber. Mueve el texto hacia arriba y lo destruye.
public class DamageNumberFloat : MonoBehaviour
{
    [SerializeField] private float speed    = 1.8f;
    [SerializeField] private float lifetime = 1.5f;

    private void Start() => Destroy(gameObject, lifetime);

    private void Update() => transform.position += Vector3.up * speed * Time.deltaTime;
}
