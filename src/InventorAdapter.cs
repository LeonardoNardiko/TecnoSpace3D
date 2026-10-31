#if HAS_INVENTOR
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Inventor;

namespace TecnoSpace3D
{
    public sealed class InventorAdapter : ICadAdapter
    {
        private Inventor.Application _invApp;
        private TransientGeometry _cachedTg;
        private DateTime _lastTryUtc = DateTime.MinValue;

        public string CadName { get { return "Autodesk Inventor 2026"; } }
        public bool IsConnected { get; private set; }
        public string StatusText { get; private set; }

        public InventorAdapter()
        {
            StatusText = "Inventor (Fechado)";
        }

        public bool MatchesProcess(string processName)
        {
            return string.Equals(processName, "Inventor", StringComparison.OrdinalIgnoreCase);
        }

        public bool EnsureConnected()
        {
            if (_invApp != null)
            {
                try
                {
                    if ((DateTime.UtcNow - _lastTryUtc).TotalSeconds < 2.0) return IsConnected;
                    _lastTryUtc = DateTime.UtcNow;
                    if (_invApp.Visible) { IsConnected = true; return true; }
                    Detach();
                }
                catch { Detach(); }
            }

            if ((DateTime.UtcNow - _lastTryUtc).TotalSeconds < 1.5) return false;
            _lastTryUtc = DateTime.UtcNow;

            try
            {
                object obj = Marshal.GetActiveObject("Inventor.Application");
                _invApp = obj as Inventor.Application;
                if (_invApp != null && _invApp.Visible)
                {
                    IsConnected = true;
                    StatusText = "Inventor 2026 (Ativo)";
                    return true;
                }
            }
            catch { }

            Detach();
            return false;
        }

        public void ApplyMotion(double tx, double ty, double tz, double rx, double ry, double rz, double dt, bool hasMotion)
        {
            if (!hasMotion || _invApp == null) return;
            try
            {
                View view = _invApp.ActiveView;
                if (view == null) return;
                Camera cam = view.Camera;
                if (cam == null) return;

                Point eye = cam.Eye;
                Point tgt = cam.Target;
                UnitVector up = cam.UpVector;

                double ex = eye.X, ey = eye.Y, ez = eye.Z;
                double gx = tgt.X, gy = tgt.Y, gz = tgt.Z;
                double ux = up.X, uy = up.Y, uz = up.Z;

                double extW = 10.0, extH = 10.0;
                cam.GetExtents(out extW, out extH);

                bool isDrawing = false;
                try { isDrawing = (_invApp.ActiveDocumentType == DocumentTypeEnum.kDrawingDocumentObject); } catch { }

                double panX = tx * extH * 0.65 * dt;
                double panY = ty * extH * 0.65 * dt;
                double pitch = isDrawing ? 0.0 : rx * 2.2 * dt;
                double yaw = isDrawing ? 0.0 : ry * 2.2 * dt;
                double roll = isDrawing ? 0.0 : rz * 2.0 * dt;

                CameraMath3D.OrbitAndPan(
                    ref ex, ref ey, ref ez,
                    ref gx, ref gy, ref gz,
                    ref ux, ref uy, ref uz,
                    panX, panY, pitch, yaw, roll);

                if (tz != 0.0)
                {
                    double zf = Math.Exp(tz * 1.55 * dt);
                    cam.SetExtents(extW * zf, extH * zf);
                }

                if (_cachedTg == null) _cachedTg = _invApp.TransientGeometry;
                cam.Eye = _cachedTg.CreatePoint(ex, ey, ez);
                cam.Target = _cachedTg.CreatePoint(gx, gy, gz);
                cam.UpVector = _cachedTg.CreateUnitVector(ux, uy, uz);
                cam.ApplyWithoutTransition();
            }
            catch { _cachedTg = null; }
        }

        public void ExecuteAction(string action)
        {
            if (_invApp == null) return;
            try
            {
                View view = _invApp.ActiveView;
                if (view != null)
                {
                    Camera cam = view.Camera;
                    switch (action)
                    {
                        case "ZoomToFit": view.Fit(true); return;
                        case "Isometric": cam.ViewOrientationType = ViewOrientationTypeEnum.kIsoTopRightViewOrientation; cam.Fit(); cam.Apply(); return;
                        case "Front": cam.ViewOrientationType = ViewOrientationTypeEnum.kFrontViewOrientation; cam.Fit(); cam.Apply(); return;
                        case "Back": cam.ViewOrientationType = ViewOrientationTypeEnum.kBackViewOrientation; cam.Fit(); cam.Apply(); return;
                        case "Left": cam.ViewOrientationType = ViewOrientationTypeEnum.kLeftViewOrientation; cam.Fit(); cam.Apply(); return;
                        case "Right": cam.ViewOrientationType = ViewOrientationTypeEnum.kRightViewOrientation; cam.Fit(); cam.Apply(); return;
                        case "Top": cam.ViewOrientationType = ViewOrientationTypeEnum.kTopViewOrientation; cam.Fit(); cam.Apply(); return;
                        case "Bottom": cam.ViewOrientationType = ViewOrientationTypeEnum.kBottomViewOrientation; cam.Fit(); cam.Apply(); return;
                    }
                }

                switch (action)
                {
                    case "NormalTo": ExecuteCommand("AppLookAtCmd"); break;
                    case "Measure": ExecuteCommand("AppMeasureDistanceCmd"); break;
                    case "Undo": ExecuteCommand("AppUndoCmd"); break;
                    case "Rebuild": ExecuteCommand("AppRebuildAllWrapperCmd"); break;
                }
            }
            catch { }
        }

        private void ExecuteCommand(string internalName)
        {
            try
            {
                ControlDefinition def = _invApp.CommandManager.ControlDefinitions[internalName];
                if (def != null) def.Execute();
            }
            catch { }
        }

        public void Detach()
        {
            _invApp = null;
            IsConnected = false;
        }
    }
}
#endif
