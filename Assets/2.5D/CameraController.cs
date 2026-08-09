using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Rotation")]
    public float angleY = 35f;
    public bool invertY = false;
    public float rotationSmoothing = 10f;
    public float rotationSensitivity = 7f;
    public float verticalRotLimitMin = -20f;
    public float verticalRotLimitMax = 40f;

    [Header("Distance / Zoom")]
    public float distance = 10f;
    public float minDistance = 3f;
    public float maxDistance = 12f;
    public float zoomSpeed = 4f;

    Vector3 _angle;
    Quaternion _oldRotation;
    Transform _t;

    public Vector2 CurrentRotation => _angle;

    void Start()
    {
        _t = transform;
        _oldRotation = _t.rotation;
        _angle.y = angleY;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void ClampAngle(ref Vector3 angle)
    {
        if (angle.x < -180f) angle.x += 360f;
        else if (angle.x > 180f) angle.x -= 360f;

        angle.y = Mathf.Clamp(angle.y, verticalRotLimitMin, verticalRotLimitMax);

        if (angle.z < -180f) angle.z += 360f;
        else if (angle.z > 180f) angle.z -= 360f;
    }

    void Update()
    {
        if (target == null || Mouse.current == null) return;

        
        Vector2 delta = Mouse.current.delta.ReadValue();
        float dx = delta.x * rotationSensitivity * 0.08f;
        float dy = (invertY ? -delta.y : delta.y) * rotationSensitivity * 0.08f;

        _angle.x += dx;
        _angle.y += dy;
        ClampAngle(ref _angle);


        float scroll = Mouse.current.scroll.ReadValue().y;
        distance -= scroll * zoomSpeed * Time.deltaTime;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Quaternion angleRotation = Quaternion.Euler(_angle.y, _angle.x, 0f);
        Quaternion currentRotation = Quaternion.Lerp(_oldRotation, angleRotation, Time.deltaTime * rotationSmoothing);
        _oldRotation = currentRotation;

        _t.position = target.position - currentRotation * Vector3.forward * distance;
        _t.LookAt(target.position, Vector3.up);
    }
}