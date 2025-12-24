using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class CableVisual : MonoBehaviour
{
    public enum CableType
    {
        Auto = 0,

        CopperStraightThrough = 1,
        CopperCrossover = 2,

        ConsoleRollover = 3,

        SerialDCE = 4,
        SerialDTE = 5
    }

    [Header("Metadata")]
    public CableType cableType = CableType.Auto;

    public bool serialEndAIsDCE = true;

    [Header("Endpoints")]
    public Port portA;
    public Port portB;

    public Transform endA;
    public Transform endB;

    [Header("Hover Collider")]
    [Tooltip("Thickness of the collider used for hovering (meters).")]
    public float hoverThickness = 0.06f;

    private LineRenderer _lr;
    private BoxCollider _box;

    private void Awake()
    {
        _lr = GetComponent<LineRenderer>();
        _lr.positionCount = 2;
        _lr.useWorldSpace = true;

        _box = GetComponent<BoxCollider>();
        if (_box == null)
            _box = gameObject.AddComponent<BoxCollider>();

        _box.isTrigger = false;
    }

    public void Initialize(Port a, Port b, CableType type, bool endAIsDce)
    {
        portA = a;
        portB = b;

        endA = a != null ? a.transform : null;
        endB = b != null ? b.transform : null;

        cableType = type;
        serialEndAIsDCE = endAIsDce;

        UpdatePositions();
    }

    private void Update()
    {
        UpdatePositions();
    }

    private void UpdatePositions()
    {
        if (_lr == null || endA == null || endB == null) return;

        Vector3 a = endA.position;
        Vector3 b = endB.position;

        _lr.SetPosition(0, a);
        _lr.SetPosition(1, b);

        Vector3 mid = (a + b) * 0.5f;
        Vector3 dir = (b - a);
        float dist = dir.magnitude;

        transform.position = mid;

        if (dist > 0.0001f)
            transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

        _box.center = Vector3.zero;
        _box.size = new Vector3(hoverThickness, hoverThickness, dist);
    }

    public string GetCableLabel()
    {
        switch (cableType)
        {
            case CableType.CopperStraightThrough:
                return "Copper Straight-Through";
            case CableType.CopperCrossover:
                return "Copper Crossover";
            case CableType.ConsoleRollover:
                return "Console (Rollover)";
            case CableType.SerialDCE:
            case CableType.SerialDTE:
                {
                    string left = serialEndAIsDCE ? "DCE" : "DTE";
                    string right = serialEndAIsDCE ? "DTE" : "DCE";
                    return $"Serial ({left} -> {right})";
                }
            case CableType.Auto:
            default:
                return "Cable";
        }
    }

    public string GetEndpointLabel()
    {
        string aDev = portA != null && portA.owner != null ? portA.owner.name : "A";
        string aPort = portA != null ? portA.portName : "?";

        string bDev = portB != null && portB.owner != null ? portB.owner.name : "B";
        string bPort = portB != null ? portB.portName : "?";

        return $"{aDev}/{aPort} <-> {bDev}/{bPort}";
    }
}
