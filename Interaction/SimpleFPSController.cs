using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleFPSController : MonoBehaviour
{
    public float moveSpeed = 4.5f;
    public float lookSpeed = 2.0f;
    public float gravity = -9.81f;

    CharacterController _cc;
    Transform _cam;
    float _pitch;
    Vector3 _vel;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        var cam = GetComponentInChildren<Camera>();
        if (cam != null) _cam = cam.transform;
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Update()
    {
        if (TerminalScreen.EscapeHandledThisFrame)
        {
        }
        else
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                bool inTerminal = TerminalScreen.IsAnyTerminalFocused;
                bool inModuleMenu = RouterModuleSlotInteractable.IsAnyModuleMenuOpen;
                bool inCableMenu = CableManager.IsAnyCableMenuOpen;
                bool inRackMenu = RackSlotInteractable.IsAnyRackMenuOpen;

                if (Input.GetMouseButtonDown(0) && !inTerminal && !inModuleMenu && !inCableMenu && !inRackMenu)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (_cc.isGrounded && _vel.y < 0) _vel.y = -2f;
            _vel.y += gravity * Time.deltaTime;
            _cc.Move(_vel * Time.deltaTime);
            return;
        }

        if (_cam != null)
        {
            float mx = Input.GetAxis("Mouse X") * lookSpeed;
            float my = Input.GetAxis("Mouse Y") * lookSpeed;

            transform.Rotate(Vector3.up * mx);

            _pitch -= my;
            _pitch = Mathf.Clamp(_pitch, -85f, 85f);
            _cam.localRotation = Quaternion.Euler(_pitch, 0, 0);
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = (transform.right * h + transform.forward * v) * moveSpeed;

        if (_cc.isGrounded && _vel.y < 0) _vel.y = -2f;
        _vel.y += gravity * Time.deltaTime;

        _cc.Move((move + _vel) * Time.deltaTime);
    }
}
