using System;
using System.IO;
using UnityEngine;

public class CliConfigStorage : MonoBehaviour
{
    public TextAsset baseConfig;

    public bool autoApplyOnStart = true;

    public bool preferLastSaved = true;

    public string deviceId = "";

    public string loadedConfigPath = "";
    public string lastSavedConfigPath = "";

    bool _applied;

    void Awake()
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            deviceId = Guid.NewGuid().ToString("N");
    }

    void Start()
    {
        if (!autoApplyOnStart) return;
        ApplyStartupConfig();
    }

    string PlayerPrefsKey => $"CliConfigStorage.{deviceId}.path";

    public void ApplyStartupConfig()
    {
        if (_applied) return;

        string text = ResolveStartupConfigText(out string path);
        loadedConfigPath = path;

        if (string.IsNullOrWhiteSpace(text))
        {
            _applied = true;
            return;
        }

        var router = GetComponent<RouterDevice>();
        if (router != null)
        {
            var s = new IosSession(router);
            ExecuteScript(s, text);
            router.RefreshProtocolStates();
            _applied = true;
            return;
        }

        var sw = GetComponent<SwitchDevice>();
        if (sw != null)
        {
            var s = new SwitchSession(sw);
            ExecuteScript(s, text);
            _applied = true;
            return;
        }

        _applied = true;
    }

    string ResolveStartupConfigText(out string path)
    {
        path = "";

        if (preferLastSaved)
        {
            string saved = PlayerPrefs.GetString(PlayerPrefsKey, "");
            if (!string.IsNullOrWhiteSpace(saved) && File.Exists(saved))
            {
                path = saved;
                lastSavedConfigPath = saved;
                return SafeReadAllText(saved);
            }
        }

        if (baseConfig != null)
        {
            path = baseConfig.name;
            return baseConfig.text;
        }

        return "";
    }

    static void ExecuteScript(ITerminalSession session, string script)
    {
        if (session == null) return;
        if (script == null) return;

        var lines = script.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i];
            if (raw == null) continue;

            string line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("!")) continue;
            if (line.StartsWith("#")) continue;
            if (line.StartsWith("//")) continue;

            session.Execute(line);
        }
    }

    static string SafeReadAllText(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return ""; }
    }

    public string GetRunningConfigText()
    {
        var router = GetComponent<RouterDevice>();
        if (router != null) return RouterConfigSerializer.Generate(router);

        var sw = GetComponent<SwitchDevice>();
        if (sw != null) return SwitchConfigSerializer.Generate(sw);

        return "";
    }

    public string SaveRunningConfigAsNewFile()
    {
        string cfg = GetRunningConfigText();
        if (string.IsNullOrWhiteSpace(cfg)) return "";

        string dir = Path.Combine(Application.persistentDataPath, "DeviceConfigs");
        Directory.CreateDirectory(dir);

        string devName = gameObject.name;
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string file = $"{devName}_{stamp}.cfg";
        string full = Path.Combine(dir, file);

        try
        {
            File.WriteAllText(full, cfg);
        }
        catch
        {
            return "";
        }

        lastSavedConfigPath = full;
        PlayerPrefs.SetString(PlayerPrefsKey, full);
        PlayerPrefs.Save();

        return full;
    }
}
