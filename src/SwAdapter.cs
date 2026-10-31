#if HAS_SW
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;

namespace TecnoSpace3D
{
    public sealed class SwAdapter : ICadAdapter
    {
        private ISldWorks _swApp;
        private IModelDoc2 _cachedDoc;
        private IModelView _cachedView;
        private int _cachedDocType;
        private DateTime _lastViewFetchUtc = DateTime.MinValue;

        private bool _inDynamics;
        private DateTime _lastActiveUtc = DateTime.MinValue;
        private DateTime _lastTryUtc = DateTime.MinValue;

        public string CadName { get { return "SOLIDWORKS 2026"; } }
        public bool IsConnected { get; private set; }
        public string StatusText { get; private set; }

        public SwAdapter()
        {
            StatusText = "SOLIDWORKS (Aguardando janela)";
        }

        public bool MatchesProcess(string processName)
        {
            return processName.IndexOf("SLDWORKS", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public bool EnsureConnected()
        {
            if (_swApp != null)
            {
                try
                {
                    if ((DateTime.UtcNow - _lastTryUtc).TotalSeconds < 2.0)
                        return IsConnected;
                    _lastTryUtc = DateTime.UtcNow;
                    if (_swApp.Visible) { IsConnected = true; return true; }
                    Detach();
                }
                catch { Detach(); }
            }

            if ((DateTime.UtcNow - _lastTryUtc).TotalSeconds < 1.5) return false;
            _lastTryUtc = DateTime.UtcNow;

            try
            {
                object obj = null;
                try
                {
                    obj = Marshal.GetActiveObject("SldWorks.Application");
                }
                catch
                {
                    // Fallback CreateInstance ONLY if an interactive SLDWORKS window is visible
                    bool hasWindow = false;
                    Process[] procs = Process.GetProcessesByName("SLDWORKS");
                    for (int i = 0; i < procs.Length; i++)
                    {
                        if (procs[i].MainWindowHandle != IntPtr.Zero) hasWindow = true;
                        procs[i].Dispose();
                    }
                    if (hasWindow)
                    {
                        Type t = Type.GetTypeFromProgID("SldWorks.Application");
                        if (t != null) obj = Activator.CreateInstance(t);
                    }
                }

                _swApp = obj as ISldWorks;
                if (_swApp != null && _swApp.Visible)
                {
                    IsConnected = true;
                    StatusText = "SOLIDWORKS " + _swApp.RevisionNumber() + " (Turbo 125Hz)";
                    return true;
                }
            }
            catch { }

            Detach();
            StatusText = "SOLIDWORKS (Janela fechada)";
            return false;
        }

        private bool RefreshCachedView()
        {
            if (_cachedView != null && _inDynamics && (DateTime.UtcNow - _lastViewFetchUtc).TotalMilliseconds < 1200)
                return true;

            _cachedDoc = _swApp.IActiveDoc2;
            if (_cachedDoc == null) { _cachedView = null; _inDynamics = false; return false; }
            _cachedView = _cachedDoc.IActiveView;
            if (_cachedView == null) { _inDynamics = false; return false; }
            _cachedDocType = _cachedDoc.GetType();
            _lastViewFetchUtc = DateTime.UtcNow;
            return true;
        }

        public void ApplyMotion(double tx, double ty, double tz, double rx, double ry, double rz, double dt, bool hasMotion)
        {
            if (_swApp == null) return;
            if (!hasMotion && !_inDynamics) return;

            try
            {
                if (!RefreshCachedView()) return;
                IModelView view = _cachedView;
                int docType = _cachedDocType; // 1=Part, 2=Assembly, 3=Drawing

                if (hasMotion)
                {
                    _lastActiveUtc = DateTime.UtcNow;
                    if (!_inDynamics && docType != 3)
                    {
                        view.StartDynamics();
                        _inDynamics = true;
                    }

                    if (docType != 3)
                    {
                        if (Math.Abs(rx) > 0.003 || Math.Abs(ry) > 0.003)
                            view.RotateAboutCenter(rx * 2.35 * dt, ry * 2.35 * dt);
                        if (Math.Abs(rz) > 0.008)
                            view.RollBy(rz * 2.1 * dt);
                    }

                    if (Math.Abs(tx) > 0.005 || Math.Abs(ty) > 0.005)
                        view.TranslateBy(tx * 0.48 * dt, ty * 0.48 * dt);

                    if (Math.Abs(tz) > 0.005)
                        view.ZoomByFactor(Math.Exp(-tz * 1.65 * dt));
                }
                else if (_inDynamics && (DateTime.UtcNow - _lastActiveUtc).TotalMilliseconds > 90)
                {
                    view.StopDynamics();
                    view.GraphicsRedraw(null);
                    _inDynamics = false;
                    _cachedView = null;
                }
            }
            catch
            {
                _inDynamics = false;
                _cachedView = null;
                _cachedDoc = null;
            }
        }

        public void ExecuteAction(string action)
        {
            if (_swApp == null) return;
            try
            {
                IModelDoc2 doc = _swApp.IActiveDoc2;
                if (doc == null) return;
                switch (action)
                {
                    case "ZoomToFit": doc.ViewZoomtofit2(); break;
                    case "Isometric": doc.ShowNamedView2("*Isometric", 7); doc.ViewZoomtofit2(); break;
                    case "Front": doc.ShowNamedView2("*Front", 1); break;
                    case "Back": doc.ShowNamedView2("*Back", 2); break;
                    case "Left": doc.ShowNamedView2("*Left", 3); break;
                    case "Right": doc.ShowNamedView2("*Right", 4); break;
                    case "Top": doc.ShowNamedView2("*Top", 5); break;
                    case "Bottom": doc.ShowNamedView2("*Bottom", 6); break;
                    case "NormalTo": doc.Extension.RunCommand(169, ""); break;
                    case "Measure": doc.Extension.RunCommand(92, ""); break;
                    case "Rebuild": doc.Extension.RunCommand(17, ""); break;
                    case "Undo": doc.Extension.RunCommand(586, ""); break;
                }
            }
            catch { }
        }

        public void Detach()
        {
            _swApp = null;
            _cachedDoc = null;
            _cachedView = null;
            _inDynamics = false;
            IsConnected = false;
        }
    }
}
#endif
