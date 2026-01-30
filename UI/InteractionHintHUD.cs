using UnityEngine;

public class InteractionHintHUD : MonoBehaviour
{
    public float maxDistance = 3f;
    public LayerMask mask = ~0;

    public Vector2 boxSize = new Vector2(900, 54);
    public float bottomPadding = 22f;
    public int fontSize = 18;

    Camera _cam;
    string _text;

    void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
    }

    void Update()
    {
        _text = "";

        if (RuntimePrefabPlacer.IsAnyPlacementUIOpen || RuntimePrefabPlacer.IsPlacingActive)
        {
            if (RuntimePrefabPlacer.IsPlacingActive)
                _text = "LMB Place    RMB Cancel    R Rotate 90°    Scroll Adjust Distance    TAB Cancel";
            return;
        }

        if (RackSlotInteractable.IsAnyRackMenuOpen || CableManager.IsAnyCableMenuOpen || RouterModuleSlotInteractable.IsAnyModuleMenuOpen)
            return;

        if (TerminalScreen.IsAnyTerminalFocused)
        {
            _text = "Terminal: Type commands and press Enter. ESC to close.";
            return;
        }

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, mask, QueryTriggerInteraction.Ignore))
            return;

        var cable = hit.collider.GetComponentInParent<CableVisual>();
        if (cable != null)
        {
            _text = "Cable: Right Click for options";
            return;
        }

        var rackProxy = hit.collider.GetComponentInParent<RackSlotInstalledProxy>();
        var rackSlot = hit.collider.GetComponentInParent<RackSlotInteractable>();

        if (rackProxy != null || rackSlot != null)
        {
            string slotLabel = "";
            int slotIndex = -1;

            if (rackSlot != null)
            {
                slotLabel = rackSlot.transform != null ? rackSlot.transform.name : "";
                slotIndex = rackSlot.RuntimeIndex;
            }
            else if (rackProxy != null)
            {
                var s = rackProxy.GetComponentInParent<RackSlotInteractable>();
                if (s != null)
                {
                    slotLabel = s.transform != null ? s.transform.name : "";
                    slotIndex = s.RuntimeIndex;
                }
            }

            string slotSuffix = "";
            if (!string.IsNullOrWhiteSpace(slotLabel)) slotSuffix = " (" + slotLabel + ")";
            else if (slotIndex >= 0) slotSuffix = " (Slot " + (slotIndex + 1) + ")";

            bool hasTerminal = hit.collider.GetComponentInParent<TerminalPopupHost>() != null;

            if (hasTerminal)
                _text = "Press E to modify slot" + slotSuffix + "    Press T to open Terminal";
            else
                _text = "Press E to modify slot" + slotSuffix;

            return;
        }

        var termHost = hit.collider.GetComponentInParent<TerminalPopupHost>();
        if (termHost != null)
        {
            _text = "Press T to open Terminal";
            return;
        }

        var interactable = hit.collider.GetComponentInParent<IDeviceInteractable>();
        if (interactable != null)
        {
            _text = "Right Click to interact";
            return;
        }
    }

    void OnGUI()
    {
        if (string.IsNullOrWhiteSpace(_text))
            return;

        var style = new GUIStyle(GUI.skin.label);
        style.fontSize = fontSize;
        style.alignment = TextAnchor.MiddleCenter;
        style.wordWrap = false;

        float w = boxSize.x;
        float h = boxSize.y;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - bottomPadding - h;

        GUI.Box(new Rect(x, y, w, h), "");
        GUI.Label(new Rect(x + 10f, y, w - 20f, h), _text, style);
    }
}
