using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class thirdPersonMovement : MonoBehaviour
{
    [Header("References")]
    public CharacterController controller;
    public Transform cam;
    public Animator animator;

    [Header("Movement")]
    public float moveSpeed = 4f;
    public float sprintSpeed = 7f;
    public float rotationSmoothTime = 0.12f;
    public float speedChangeRate = 10f;

    [Header("Gravity")]
    public float gravity = -20f;
    public float jumpHeight = 1.2f;

    [Header("Animation")]
    public float animationSmoothTime = 0.1f;

    // Private
    float _speed;
    float _animationBlend;
    float _rotationVelocity;
    float _verticalVelocity;

    void Awake()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        if (cam == null && Camera.main != null)
            cam = Camera.main.transform;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        Move();
    }

    void Move()
    {
        Vector2 input = Vector2.zero;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) input.y += 1;
            if (Keyboard.current.sKey.isPressed) input.y -= 1;
            if (Keyboard.current.dKey.isPressed) input.x += 1;
            if (Keyboard.current.aKey.isPressed) input.x -= 1;
        }

        bool sprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        Vector3 inputDirection = new Vector3(input.x, 0, input.y).normalized;
        float inputMagnitude = Mathf.Clamp01(inputDirection.magnitude);

        float targetMaxSpeed = sprint ? sprintSpeed : moveSpeed;
        float targetSpeed = inputMagnitude * targetMaxSpeed;

        _speed = Mathf.Lerp(_speed, targetSpeed, Time.deltaTime * speedChangeRate);
        if (_speed < 0.01f) _speed = 0f;

        Vector3 moveDirection = Vector3.zero;
        if (inputMagnitude > 0.01f && cam != null)
        {
            Vector3 camForward = cam.forward; camForward.y = 0; camForward.Normalize();
            Vector3 camRight = cam.right; camRight.y = 0; camRight.Normalize();

            moveDirection = (camForward * inputDirection.z + camRight * inputDirection.x).normalized;

            float targetRotation = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            float rotation = Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetRotation,
                ref _rotationVelocity,
                rotationSmoothTime);
            transform.rotation = Quaternion.Euler(0, rotation, 0);
        }

        if (controller.isGrounded)
        {
            if (_verticalVelocity < 0)
                _verticalVelocity = -2f;

            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        else
        {
            _verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 finalMove = moveDirection * _speed;
        finalMove.y = _verticalVelocity;
        controller.Move(finalMove * Time.deltaTime);

        if (animator != null)
        {
            float normalizedSpeed = targetMaxSpeed > 0 ? _speed / sprintSpeed : 0f;
            _animationBlend = Mathf.Lerp(_animationBlend, normalizedSpeed, Time.deltaTime / animationSmoothTime);
            animator.SetFloat("speed", _animationBlend);
        }
    }
}