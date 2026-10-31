using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace TecnoSpace3D
{
    public sealed partial class MainForm : Form
    {
        private readonly AppConfig _cfg;
        private readonly SpaceBallHid _hid;
        private readonly MultiCadHub _hub;
        private readonly bool _startMinimized;
        private readonly Action _exitCallback;

        private NotifyIcon _trayIcon;
        private System.Windows.Forms.Timer _uiTimer;
        private bool _allowClose;
        private bool _suppressUiEvents;
        private int _lastPressedBtn = -1;
        private DateTime _lastPressedUtc = DateTime.MinValue;

        public MainForm(bool startMinimized, Action exitCallback)
        {
            _startMinimized = startMinimized;
            _exitCallback = exitCallback;
            _cfg = AppConfig.Load();
            if (string.IsNullOrEmpty(_cfg.Language)) _cfg.Language = "pt";

            _hid = new SpaceBallHid();
            _hub = new MultiCadHub(_hid, _cfg);

            InitializeLayout();
            SetupTrayIcon();
            ApplyLanguage();
            LoadConfigToUi();
            CreateHandle();

            _hid.ButtonChanged += OnPhysicalButton;
            _hub.ConfigMutatedByButton += () =>
            {
                if (IsHandleCreated)
                    BeginInvoke(new Action(LoadConfigToUi));
            };

            _hid.Start();
            _hub.Start();

            _uiTimer = new System.Windows.Forms.Timer { Interval = 50 };
            _uiTimer.Tick += OnUiTick;
            _uiTimer.Start();
        }

        private void OnLanguageComboChanged(object sender, EventArgs e)
        {
            if (_suppressUiEvents) return;
            _cfg.Language = (_cmbLang.SelectedIndex == 1) ? "en" : "pt";
            _cfg.Save();
            ApplyLanguage();
        }

        private void ToggleLanguage()
        {
            _cfg.Language = I18n.IsEn(_cfg.Language) ? "pt" : "en";
            _cfg.Save();
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            _suppressUiEvents = true;
            try
            {
                string lg = _cfg.Language;
                _cmbLang.SelectedIndex = I18n.IsEn(lg) ? 1 : 0;

                Text = I18n.T(lg, "WinTitle");
                _lblHeaderTitle.Text = I18n.T(lg, "HeaderTitle");
                _btnZero.Text = I18n.T(lg, "BtnZero");

                _lblSecAxes.Text = I18n.T(lg, "SecAxes");
                _lblAxisNames[0].Text = I18n.T(lg, "AxisTx");
                _lblAxisNames[1].Text = I18n.T(lg, "AxisTy");
                _lblAxisNames[2].Text = I18n.T(lg, "AxisTz");
                _lblAxisNames[3].Text = I18n.T(lg, "AxisRx");
                _lblAxisNames[4].Text = I18n.T(lg, "AxisRy");
                _lblAxisNames[5].Text = I18n.T(lg, "AxisRz");

                _lblSecSens.Text = I18n.T(lg, "SecSens");
                _lblSliderNames[0].Text = I18n.T(lg, "SlMaster");
                _lblSliderNames[1].Text = I18n.T(lg, "SlPan");
                _lblSliderNames[2].Text = I18n.T(lg, "SlZoom");
                _lblSliderNames[3].Text = I18n.T(lg, "SlRot");
                _lblSliderNames[4].Text = I18n.T(lg, "SlDz");

                _chkRotation.Text = I18n.T(lg, "ChkRot");
                _chkDominant.Text = I18n.T(lg, "ChkDom");
                _chkSwapYZ.Text = I18n.T(lg, "ChkSwap");
                _chkAutoStart.Text = I18n.T(lg, "ChkAuto");

                _lblSecBtns.Text = I18n.T(lg, "SecBtns");
                string btnWord = I18n.T(lg, "BtnWord");
                for (int i = 0; i < 12; i++)
                {
                    _lblBtnRows[i].Text = btnWord + ButtonDisplayName(i);
                    ComboBox cb = _cmbButtons[i];
                    cb.Items.Clear();
                    int selIdx = 0;
                    for (int a = 0; a < AppConfig.AvailableActions.Length; a++)
                    {
                        string key = AppConfig.AvailableActions[a];
                        cb.Items.Add(new ActionItem(key, I18n.TranslateAction(lg, key)));
                        if (key == _cfg.ButtonActions[i]) selIdx = a;
                    }
                    cb.SelectedIndex = selIdx;
                }

                _mnuOpen.Text = I18n.T(lg, "TrayOpen");
                _mnuZero.Text = I18n.T(lg, "TrayZero");
                _mnuLang.Text = I18n.T(lg, "TrayLang");
                _mnuQuit.Text = I18n.T(lg, "TrayQuit");
                RefreshDynamicTexts();
            }
            finally { _suppressUiEvents = false; }
        }

        private void OnPhysicalButton(int idx, bool pressed)
        {
            if (!pressed) return;
            _lastPressedBtn = idx;
            _lastPressedUtc = DateTime.UtcNow;
        }

        private void OnUiTick(object sender, EventArgs e)
        {
            if (!Visible) return;
            RefreshDynamicTexts();

            UpdateAxisBar(_barTx, _lblValTx, _hub.NormTx);
            UpdateAxisBar(_barTy, _lblValTy, _hub.NormTy);
            UpdateAxisBar(_barTz, _lblValTz, _hub.NormTz);
            UpdateAxisBar(_barRx, _lblValRx, _hub.NormRx);
            UpdateAxisBar(_barRy, _lblValRy, _hub.NormRy);
            UpdateAxisBar(_barRz, _lblValRz, _hub.NormRz);
        }

        private void RefreshDynamicTexts()
        {
            string lg = _cfg.Language;
            bool usbOk = _hid.IsConnected;
            _lblUsbStatus.Text = I18n.T(lg, "UsbPrefix") + I18n.TranslateRuntimeStatus(lg, _hid.ConnectedDeviceName);
            _lblUsbStatus.ForeColor = usbOk ? Color.FromArgb(74, 222, 128) : Color.FromArgb(248, 113, 113);

            _lblCadStatus.Text = I18n.T(lg, "CadPrefix") + I18n.TranslateRuntimeStatus(lg, _hub.ActiveStatusText);
            _lblCadStatus.ForeColor = _hub.IsAnyCadAttached ? Color.FromArgb(56, 189, 248) : Color.FromArgb(250, 204, 21);

            if (_lastPressedBtn >= 0 && (DateTime.UtcNow - _lastPressedUtc).TotalSeconds < 2.5)
            {
                string bName = ButtonDisplayName(_lastPressedBtn);
                string actLabel = I18n.TranslateAction(lg, _cfg.ButtonActions[_lastPressedBtn]);
                _lblLastBtn.Text = I18n.T(lg, "PressedPrefix") + "[" + bName + "] -> " + actLabel;
                _lblLastBtn.ForeColor = Color.FromArgb(74, 222, 128);
            }
            else
            {
                _lblLastBtn.Text = I18n.T(lg, "HintPressBtn");
                _lblLastBtn.ForeColor = Color.FromArgb(148, 163, 184);
            }
        }

        private static void UpdateAxisBar(Panel barFill, Label valLabel, double val)
        {
            double clamped = Math.Max(-1.0, Math.Min(1.0, val));
            valLabel.Text = val.ToString("+0.00;-0.00; 0.00");
            int parentW = barFill.Parent.ClientSize.Width;
            int center = parentW / 2;
            int px = (int)Math.Round(Math.Abs(clamped) * (center - 2));
            barFill.Left = clamped >= 0 ? center : center - px;
            barFill.Width = Math.Max(2, px);
            barFill.BackColor = clamped >= 0 ? Color.FromArgb(56, 189, 248) : Color.FromArgb(244, 114, 182);
        }

        private void LoadConfigToUi()
        {
            _suppressUiEvents = true;
            try
            {
                _trkMaster.Value = ClampTrack((int)Math.Round(_cfg.MasterSpeed * 100), 20, 300);
                _trkPan.Value = ClampTrack((int)Math.Round(_cfg.PanSpeed * 100), 20, 300);
                _trkZoom.Value = ClampTrack((int)Math.Round(_cfg.ZoomSpeed * 100), 20, 300);
                _trkRot.Value = ClampTrack((int)Math.Round(_cfg.RotateSpeed * 100), 20, 300);
                _trkDeadzone.Value = ClampTrack((int)Math.Round(_cfg.Deadzone * 100), 1, 30);
                UpdateSliderLabels();

                _chkInvTx.Checked = _cfg.InvertTx; _chkInvTy.Checked = _cfg.InvertTy; _chkInvTz.Checked = _cfg.InvertTz;
                _chkInvRx.Checked = _cfg.InvertRx; _chkInvRy.Checked = _cfg.InvertRy; _chkInvRz.Checked = _cfg.InvertRz;
                _chkSwapYZ.Checked = _cfg.SwapPanZoomYZ; _chkDominant.Checked = _cfg.DominantAxis;
                _chkRotation.Checked = _cfg.RotationEnabled; _chkAutoStart.Checked = AppConfig.IsAutoStartEnabled();

                for (int i = 0; i < 12; i++)
                {
                    int idx = Array.IndexOf(AppConfig.AvailableActions, _cfg.ButtonActions[i]);
                    if (idx >= 0 && idx < _cmbButtons[i].Items.Count) _cmbButtons[i].SelectedIndex = idx;
                }
            }
            finally { _suppressUiEvents = false; }
        }

        private void OnUiSettingChanged(object sender, EventArgs e)
        {
            if (_suppressUiEvents) return;
            _cfg.MasterSpeed = _trkMaster.Value / 100.0;
            _cfg.PanSpeed = _trkPan.Value / 100.0;
            _cfg.ZoomSpeed = _trkZoom.Value / 100.0;
            _cfg.RotateSpeed = _trkRot.Value / 100.0;
            _cfg.Deadzone = _trkDeadzone.Value / 100.0;

            _cfg.InvertTx = _chkInvTx.Checked; _cfg.InvertTy = _chkInvTy.Checked; _chkInvTz.Checked = _chkInvTz.Checked;
            _cfg.InvertTz = _chkInvTz.Checked;
            _cfg.InvertRx = _chkInvRx.Checked; _cfg.InvertRy = _chkInvRy.Checked; _cfg.InvertRz = _chkInvRz.Checked;

            _cfg.SwapPanZoomYZ = _chkSwapYZ.Checked;
            _cfg.DominantAxis = _chkDominant.Checked;
            _cfg.RotationEnabled = _chkRotation.Checked;

            for (int i = 0; i < 12; i++)
            {
                ActionItem item = _cmbButtons[i].SelectedItem as ActionItem;
                if (item != null) _cfg.ButtonActions[i] = item.Key;
            }

            AppConfig.SetAutoStart(_chkAutoStart.Checked, Assembly.GetExecutingAssembly().Location);
            UpdateSliderLabels();
            _cfg.Save();
        }

        private void UpdateSliderLabels()
        {
            _lblMasterVal.Text = _trkMaster.Value + "%";
            _lblPanVal.Text = _trkPan.Value + "%";
            _lblZoomVal.Text = _trkZoom.Value + "%";
            _lblRotVal.Text = _trkRot.Value + "%";
            _lblDzVal.Text = _trkDeadzone.Value + "%";
        }

        private static int ClampTrack(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
        private static string ButtonDisplayName(int idx) { return idx < 9 ? (idx + 1).ToString() : (idx == 9 ? "A (10)" : (idx == 10 ? "B (11)" : "C (12)")); }

        public void RestoreToFront()
        {
            ShowInTaskbar = true; Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            BringToFront(); Activate();
        }

        public void ForceQuit() { _allowClose = true; Close(); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; Hide(); ShowInTaskbar = false;
                _trayIcon.ShowBalloonTip(1500, I18n.T(_cfg.Language, "BalloonTitle"), I18n.T(_cfg.Language, "BalloonBody"), ToolTipIcon.Info);
                return;
            }
            _uiTimer.Stop(); _hub.Dispose(); _hid.Dispose();
            if (_trayIcon != null) { _trayIcon.Visible = false; _trayIcon.Dispose(); }
            base.OnFormClosing(e);
            if (_exitCallback != null) _exitCallback();
        }
    }
}
