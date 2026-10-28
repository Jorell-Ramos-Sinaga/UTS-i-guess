using UnityEngine;
using UnityEngine.InputSystem;

public class FreeFlyCamera : MonoBehaviour
{
    [Header("Speed Settings")]
    [SerializeField] private float normalSpeed = 15f;
    [SerializeField] private float fastSpeed = 35f; // Saat tahan Shift
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float zoomSpeed = 10f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    private void Start()
    {
        // Simpan rotasi awal kamera
        Vector3 angles = transform.eulerAngles;
        rotationX = angles.y;
        rotationY = angles.x;
    }

    private void Update()
    {
        // 1. Gerakan WASD + QE (Bisa kapan saja tanpa klik)
        HandleMovement();

        // 2. Zoom dengan Scroll Wheel (Bisa kapan saja)
        HandleZoom();

        // 3. Rotasi Pandangan Scene View (HANYA saat Klik Kanan ditahan)
        if (IsRightClickHeld())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            HandleRotation();
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private bool IsRightClickHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.rightButton.isPressed;
#else
        return Input.GetMouseButton(1);
#endif
    }

    private void HandleRotation()
    {
        float mouseX = 0f;
        float mouseY = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            mouseX = delta.x * 0.1f * mouseSensitivity;
            mouseY = delta.y * 0.1f * mouseSensitivity;
        }
#else
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
#endif

        rotationX += mouseX;
        rotationY -= mouseY;
        rotationY = Mathf.Clamp(rotationY, -89f, 89f); // Mencegah kamera kebalik

        transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
    }

    private void HandleMovement()
    {
        float speed = IsShiftHeld() ? fastSpeed : normalSpeed;
        Vector3 moveInput = Vector3.zero;

        // Ambil arah depan dan kanan kamera, lalu ratakan sejajar tanah (Y = 0)
        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveInput += forward;
            if (Keyboard.current.sKey.isPressed) moveInput -= forward;
            if (Keyboard.current.aKey.isPressed) moveInput -= right;
            if (Keyboard.current.dKey.isPressed) moveInput += right;
            if (Keyboard.current.eKey.isPressed) moveInput += Vector3.up;    // Naik vertikal
            if (Keyboard.current.qKey.isPressed) moveInput -= Vector3.up;    // Turun vertikal
        }
#else
        if (Input.GetKey(KeyCode.W)) moveInput += forward;
        if (Input.GetKey(KeyCode.S)) moveInput -= forward;
        if (Input.GetKey(KeyCode.A)) moveInput -= right;
        if (Input.GetKey(KeyCode.D)) moveInput += right;
        if (Input.GetKey(KeyCode.E)) moveInput += Vector3.up;
        if (Input.GetKey(KeyCode.Q)) moveInput -= Vector3.up;
#endif

        if (moveInput.sqrMagnitude > 0.001f)
        {
            transform.position += moveInput.normalized * speed * Time.unscaledDeltaTime;
        }
    }

    private void HandleZoom()
    {
        float scroll = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            scroll = Mouse.current.scroll.ReadValue().y * 0.01f;
        }
#else
        scroll = Input.GetAxis("Mouse ScrollWheel");
#endif

        if (Mathf.Abs(scroll) > 0.01f)
        {
            transform.position += transform.forward * scroll * zoomSpeed;
        }
    }

    private bool IsShiftHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftShift);
#endif
    }
}