using System;
using System.IO;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace TecnoSpace3D
{
    public sealed class AppConfig
    {
        public double MasterSpeed { get; set; }
        public double PanSpeed { get; set; }
        public double ZoomSpeed { get; set; }
        public double RotateSpeed { get; set; }
        public double Deadzone { get; set; }
        public double ExpoCurve { get; set; }
        public double Smoothing { get; set; }

        public bool InvertTx { get; set; }
        public bool InvertTy { get; set; }
        public bool InvertTz { get; set; }
        public bool InvertRx { get; set; }
        public bool InvertRy { get; set; }
        public bool InvertRz { get; set; }

        public bool SwapPanZoomYZ { get; set; }
        public bool DominantAxis { get; set; }
        public bool RotationEnabled { get; set; }
        public bool OnlyWhenSwFocused { get; set; }
        public bool StartMinimized { get; set; }
        public string Language { get; set; }

        public string[] ButtonActions { get; set; }

        public static readonly string[] AvailableActions = new string[]
        {
            "ZoomToFit",
            "Isometric",
            "NormalTo",
            "Front",
            "Top",
            "Right",
            "Back",
            "Left",
            "Bottom",
            "Measure",
            "Undo",
            "Rebuild",
            "Escape",
            "ToggleDominant",
            "ToggleRotation",
            "ZeroSensor",
            "SpeedUp",
            "SpeedDown",
            "None"
        };

        public AppConfig()
        {
            MasterSpeed = 1.00;
            PanSpeed = 1.00;
            ZoomSpeed = 1.00;
            RotateSpeed = 1.00;
            Deadzone = 0.06;
            ExpoCurve = 0.35;
            Smoothing = 0.38;

            InvertTx = false;
            InvertTy = false;
            InvertTz = false;
            InvertRx = false;
            InvertRy = false;
            InvertRz = false;

            SwapPanZoomYZ = false;
            DominantAxis = false;
            RotationEnabled = true;
            OnlyWhenSwFocused = false;
            StartMinimized = false;
            Language = "pt";

            ButtonActions = new string[12]
            {
                "ZoomToFit",      // Botao 1
                "NormalTo",       // Botao 2
                "Isometric",      // Botao 3
                "Front",          // Botao 4
                "Top",            // Botao 5
                "Right",          // Botao 6
                "Measure",        // Botao 7
                "Undo",           // Botao 8
                "Escape",         // Botao 9
                "ToggleDominant", // Botao A (10)
                "ToggleRotation", // Botao B (11)
                "ZeroSensor"      // Botao C (12)
            };
        }

        public static string ConfigDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TecnoSpace3D"); }
        }

        public static string ConfigPath
        {
            get { return Path.Combine(ConfigDir, "config.json"); }
        }

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    string json = File.ReadAllText(ConfigPath);
                    var ser = new JavaScriptSerializer();
                    var cfg = ser.Deserialize<AppConfig>(json);
                    if (cfg != null)
                    {
                        if (cfg.ButtonActions == null || cfg.ButtonActions.Length < 12)
                        {
                            var def = new AppConfig();
                            if (cfg.ButtonActions != null)
                                Array.Copy(cfg.ButtonActions, def.ButtonActions, Math.Min(cfg.ButtonActions.Length, 12));
                            cfg.ButtonActions = def.ButtonActions;
                        }
                        return cfg;
                    }
                }
            }
            catch { }
            var fresh = new AppConfig();
            fresh.Save();
            return fresh;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                    Directory.CreateDirectory(ConfigDir);
                var ser = new JavaScriptSerializer();
                string json = ser.Serialize(this);
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "TecnoSpace3D_SolidWorks";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    return key != null && key.GetValue(RunValueName) != null;
                }
            }
            catch { return false; }
        }

        public static void SetAutoStart(bool enable, string exePath)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (enable)
                        key.SetValue(RunValueName, "\"" + exePath + "\" --minimized");
                    else if (key.GetValue(RunValueName) != null)
                        key.DeleteValue(RunValueName, false);
                }
            }
            catch { }
        }
    }
}
