using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TerminalScreen : MonoBehaviour
{

    public static int LastEscapeHandledFrame = -1;

    public static int FocusedCount = 0;

    public static bool EscapeHandledThisFrame => LastEscapeHandledFrame == Time.frameCount;
    public static bool IsAnyTerminalFocused => FocusedCount > 0;

    [Header("UI")]
    public TMP_Text outputText;
    public TMP_Text inputLineText;
    public ScrollRect outputScroll;

    
    public Button pasteButton;
    public Button copyButton;
[Header("Device Link")]
    public RouterDevice routerDevice;
    public SwitchDevice switchDevice;
    public PcDevice pcDevice;

    [Header("Settings")]
    public int maxLines = 200;
    public int maxHistory = 50;
    public bool startFocused = false;

    private ITerminalSession _session;
    private string _currentInput = "";
    private bool _active;

    private readonly Queue<string> _lineBuffer = new();
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private string _historyDraft = "";

    private Device _ownerDevice;

    private void Awake()
    {
        ResolveOwnerDevice();

        SetFocused(startFocused);
        ClearScreen();

        if (pasteButton) pasteButton.onClick.AddListener(PasteFromClipboard);
        if (copyButton) copyButton.onClick.AddListener(CopyAllToClipboard);
    
    }

    private void ResolveOwnerDevice()
    {
        if (routerDevice != null) _ownerDevice = routerDevice;
        else if (switchDevice != null) _ownerDevice = switchDevice;
        else if (pcDevice != null) _ownerDevice = pcDevice;
        else _ownerDevice = GetComponentInParent<Device>();
    }

    private bool HasPower()
    {
        return _ownerDevice == null || _ownerDevice.IsPoweredOn;
    }

    private void Update()
    {

        if (!HasPower())
        {
            if (_active)
                SetFocused(false);

            ClearScreen();
            return;
        }

        if (!_active) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LastEscapeHandledFrame = Time.frameCount;
            SetFocused(false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            HistoryUp();
            RedrawInputLine();
            return;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            HistoryDown();
            RedrawInputLine();
            return;
        }

        
bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

if (ctrl && Input.GetKeyDown(KeyCode.V))
{
    PasteFromClipboard();
    return;
}

if (ctrl && Input.GetKeyDown(KeyCode.C))
{
    CopyAllToClipboard();
    return;
}

foreach (char c in Input.inputString)
        {
            if (c == '\b')
            {
                if (_currentInput.Length > 0)
                    _currentInput = _currentInput[..^1];
                ExitHistoryBrowseIfNeeded();
            }
            else if (c == '\n' || c == '\r')
            {
                SubmitLine();
                return;
            }
            else if (!char.IsControl(c))
            {
                _currentInput += c;
                ExitHistoryBrowseIfNeeded();
            }
        }

        RedrawInputLine();
    }

    public void Focus()
    {

        if (!HasPower())
            return;

        SetFocused(true);
    }

    public void SetFocused(bool on)
    {
        if (_active == on) return;

        _active = on;

        if (on)
        {
            FocusedCount++;
            BuildSession();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _historyIndex = -1;
            _historyDraft = "";
        }
        else
        {
            FocusedCount = Mathf.Max(0, FocusedCount - 1);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _historyIndex = -1;
            _historyDraft = "";
        }

        RedrawInputLine();
        SnapToBottom();
    }

    private void BuildSession()
    {
        if (!HasPower())
        {
            _session = null;
            return;
        }

        if (pcDevice != null)
        {
            var rs232 = pcDevice.GetRs232Port();
            if (rs232 != null && rs232.connectedTo != null)
            {
                var other = rs232.connectedTo;
                if (other.owner is RouterDevice r)
                {
                    _session = new IosSession(r);
                    return;
                }
            }

            _session = new PcSession(pcDevice);
            return;
        }

        if (switchDevice != null)
        {
            _session = new SwitchSession(switchDevice);
            return;
        }

        _session = new IosSession(routerDevice);
    }

    
public void PasteFromClipboard()
{
    if (!_active) return;
    if (_session == null) return;
    if (!HasPower()) return;

    string clip = GUIUtility.systemCopyBuffer ?? "";
    if (string.IsNullOrEmpty(clip)) return;

    if (clip.IndexOf('\n') >= 0 || clip.IndexOf('\r') >= 0)
    {
        PasteAndExecute(clip);
        return;
    }

    _currentInput += clip;
    ExitHistoryBrowseIfNeeded();
    RedrawInputLine();
    SnapToBottom();
}

public void CopyAllToClipboard()
{
    if (_lineBuffer == null) return;
    GUIUtility.systemCopyBuffer = string.Join("\n", _lineBuffer);
}

private void PasteAndExecute(string text)
{
    string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
    var lines = normalized.Split('\n');
    for (int i = 0; i < lines.Length; i++)
    {
        string cmd = lines[i].TrimEnd();
        if (string.IsNullOrWhiteSpace(cmd)) continue;
        SubmitCommand(cmd);
    }
}

private void SubmitCommand(string cmdRaw)
{
    if (_session == null) return;

    string cmd = cmdRaw.TrimEnd();

    AppendText($"{_session.Prompt} {cmd}");

    if (!string.IsNullOrWhiteSpace(cmd))
        AddToHistory(cmd);

    string result = _session.Execute(cmd);

    if (result == "logout")
    {
        SetFocused(false);
        return;
    }

    if (!string.IsNullOrWhiteSpace(result))
        AppendText(result);

    _currentInput = "";
    RedrawInputLine();
    SnapToBottom();
}

private void SubmitLine()
    {
        string cmd = _currentInput.TrimEnd();
        _currentInput = "";
        if (string.IsNullOrWhiteSpace(cmd))
        {
            RedrawInputLine();
            SnapToBottom();
            return;
        }
        SubmitCommand(cmd);
    }

    private void AddToHistory(string cmd)
    {
        if (_history.Count > 0 && _history[^1] == cmd) return;
        _history.Add(cmd);
        while (_history.Count > maxHistory)
            _history.RemoveAt(0);
    }

    private void HistoryUp()
    {
        if (_history.Count == 0) return;
        if (_historyIndex == -1)
        {
            _historyDraft = _currentInput;
            _historyIndex = _history.Count - 1;
        }
        else _historyIndex = Mathf.Max(0, _historyIndex - 1);

        _currentInput = _history[_historyIndex];
    }

    private void HistoryDown()
    {
        if (_historyIndex == -1) return;
        _historyIndex++;

        if (_historyIndex >= _history.Count)
        {
            _historyIndex = -1;
            _currentInput = _historyDraft;
            return;
        }

        _currentInput = _history[_historyIndex];
    }

    private void ExitHistoryBrowseIfNeeded()
    {
        _historyIndex = -1;
        _historyDraft = "";
    }

    private void AppendText(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        _lineBuffer.Enqueue(line);
        while (_lineBuffer.Count > maxLines)
            _lineBuffer.Dequeue();

        RebuildOutput();
    }

    private void ClearScreen()
    {
        _lineBuffer.Clear();
        if (outputText) outputText.text = "";
        if (inputLineText) inputLineText.text = "";
    }

    private void RebuildOutput()
    {
        if (outputText)
            outputText.text = string.Join("\n", _lineBuffer);
    }

    private void RedrawInputLine()
    {
        if (inputLineText)
            inputLineText.text = _session != null ? $"{_session.Prompt} {_currentInput}" : "";
    }

    private void SnapToBottom()
    {
        if (!outputScroll) return;
        Canvas.ForceUpdateCanvases();
        outputScroll.verticalNormalizedPosition = 0f;
    }
}
