using System;
using System.Drawing;
using System.Windows.Forms;

namespace TecnoSpace3D
{
    public sealed partial class MainForm
    {
        private Label _lblHeaderTitle, _lblUsbStatus, _lblCadStatus, _lblLastBtn;
        private Label _lblSecAxes, _lblSecSens, _lblSecBtns;
        private Label[] _lblAxisNames = new Label[6];
        private Label[] _lblSliderNames = new Label[5];
        private Label[] _lblBtnRows = new Label[12];
        private Button _btnZero;
        private ComboBox _cmbLang;

        private Panel _barTx, _barTy, _barTz, _barRx, _barRy, _barRz;
        private Label _lblValTx, _lblValTy, _lblValTz, _lblValRx, _lblValRy, _lblValRz;
        private TrackBar _trkMaster, _trkPan, _trkZoom, _trkRot, _trkDeadzone;
        private Label _lblMasterVal, _lblPanVal, _lblZoomVal, _lblRotVal, _lblDzVal;
        private CheckBox _chkInvTx, _chkInvTy, _chkInvTz, _chkInvRx, _chkInvRy, _chkInvRz;
        private CheckBox _chkSwapYZ, _chkDominant, _chkRotation, _chkAutoStart;
        private ComboBox[] _cmbButtons = new ComboBox[12];

        private MenuItem _mnuOpen, _mnuZero, _mnuLang, _mnuQuit;

        private void InitializeLayout()
        {
            Size = new Size(880, 620);
            MinimumSize = new Size(860, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(15, 23, 42);
            ForeColor = Color.FromArgb(226, 232, 240);
            Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            Icon = SystemIcons.Application;

            Panel header = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Color.FromArgb(30, 41, 59), Padding = new Padding(14, 8, 14, 8) };
            _lblHeaderTitle = new Label { Font = new Font("Segoe UI", 11.5F, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(14, 8) };
            _lblUsbStatus = new Label { AutoSize = true, Location = new Point(14, 35), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            _lblCadStatus = new Label { AutoSize = true, Location = new Point(320, 35), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };

            _cmbLang = new ComboBox
            {
                Location = new Point(606, 17),
                Size = new Size(88, 26),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(15, 23, 42),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _cmbLang.Items.AddRange(new object[] { "PT-BR", "EN-US" });
            _cmbLang.SelectedIndexChanged += OnLanguageComboChanged;

            _btnZero = new Button
            {
                Size = new Size(148, 34),
                Location = new Point(702, 14),
                BackColor = Color.FromArgb(14, 165, 233),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            _btnZero.FlatAppearance.BorderSize = 0;
            _btnZero.Click += (s, e) => _hid.ZeroSensor();

            header.Controls.AddRange(new Control[] { _lblHeaderTitle, _lblUsbStatus, _lblCadStatus, _cmbLang, _btnZero });
            Controls.Add(header);

            Panel leftBox = new Panel { Location = new Point(14, 76), Size = new Size(425, 492), BackColor = Color.FromArgb(30, 41, 59) };
            _lblSecAxes = MakeSectionLabel("", 12, 10);
            leftBox.Controls.Add(_lblSecAxes);

            _lblAxisNames[0] = CreateAxisRow(leftBox, 38, out _barTx, out _lblValTx, out _chkInvTx);
            _lblAxisNames[1] = CreateAxisRow(leftBox, 68, out _barTy, out _lblValTy, out _chkInvTy);
            _lblAxisNames[2] = CreateAxisRow(leftBox, 98, out _barTz, out _lblValTz, out _chkInvTz);
            _lblAxisNames[3] = CreateAxisRow(leftBox, 128, out _barRx, out _lblValRx, out _chkInvRx);
            _lblAxisNames[4] = CreateAxisRow(leftBox, 158, out _barRy, out _lblValRy, out _chkInvRy);
            _lblAxisNames[5] = CreateAxisRow(leftBox, 188, out _barRz, out _lblValRz, out _chkInvRz);

            _lblSecSens = MakeSectionLabel("", 12, 224);
            leftBox.Controls.Add(_lblSecSens);
            _lblSliderNames[0] = CreateSliderRow(leftBox, 248, 20, 300, 100, out _trkMaster, out _lblMasterVal);
            _lblSliderNames[1] = CreateSliderRow(leftBox, 282, 20, 300, 100, out _trkPan, out _lblPanVal);
            _lblSliderNames[2] = CreateSliderRow(leftBox, 316, 20, 300, 100, out _trkZoom, out _lblZoomVal);
            _lblSliderNames[3] = CreateSliderRow(leftBox, 350, 20, 300, 100, out _trkRot, out _lblRotVal);
            _lblSliderNames[4] = CreateSliderRow(leftBox, 384, 1, 30, 6, out _trkDeadzone, out _lblDzVal);

            _chkRotation = MakeCheck("", 14, 424);
            _chkDominant = MakeCheck("", 200, 424);
            _chkSwapYZ = MakeCheck("", 14, 452);
            _chkAutoStart = MakeCheck("", 200, 452);
            leftBox.Controls.AddRange(new Control[] { _chkRotation, _chkDominant, _chkSwapYZ, _chkAutoStart });
            Controls.Add(leftBox);

            Panel rightBox = new Panel { Location = new Point(451, 76), Size = new Size(400, 492), BackColor = Color.FromArgb(30, 41, 59) };
            _lblSecBtns = MakeSectionLabel("", 12, 10);
            rightBox.Controls.Add(_lblSecBtns);

            _lblLastBtn = new Label { Location = new Point(12, 34), Size = new Size(376, 22), ForeColor = Color.FromArgb(148, 163, 184) };
            rightBox.Controls.Add(_lblLastBtn);

            for (int i = 0; i < 12; i++)
            {
                int rowY = 64 + i * 34;
                Label lb = new Label { Location = new Point(14, rowY + 4), Size = new Size(95, 20), ForeColor = Color.FromArgb(203, 213, 225) };
                ComboBox cb = new ComboBox
                {
                    Location = new Point(115, rowY),
                    Size = new Size(265, 24),
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    BackColor = Color.FromArgb(15, 23, 42),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                cb.SelectedIndexChanged += OnUiSettingChanged;
                _lblBtnRows[i] = lb;
                _cmbButtons[i] = cb;
                rightBox.Controls.Add(lb);
                rightBox.Controls.Add(cb);
            }
            Controls.Add(rightBox);
        }

        private void SetupTrayIcon()
        {
            ContextMenu menu = new ContextMenu();
            _mnuOpen = new MenuItem("", (s, e) => RestoreToFront());
            _mnuZero = new MenuItem("", (s, e) => _hid.ZeroSensor());
            _mnuLang = new MenuItem("", (s, e) => ToggleLanguage());
            _mnuQuit = new MenuItem("", (s, e) => ForceQuit());

            menu.MenuItems.Add(_mnuOpen);
            menu.MenuItems.Add(_mnuZero);
            menu.MenuItems.Add(_mnuLang);
            menu.MenuItems.Add("-");
            menu.MenuItems.Add(_mnuQuit);

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = "TecnoSpace3D - SpaceBall 5000 Multi-CAD",
                ContextMenu = menu,
                Visible = true
            };
            _trayIcon.DoubleClick += (s, e) => RestoreToFront();
        }

        private Label MakeSectionLabel(string txt, int x, int y)
        {
            return new Label { Text = txt, Location = new Point(x, y), AutoSize = true, Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), ForeColor = Color.FromArgb(56, 189, 248) };
        }

        private CheckBox MakeCheck(string txt, int x, int y)
        {
            var chk = new CheckBox { Text = txt, Location = new Point(x, y), AutoSize = true, ForeColor = Color.FromArgb(226, 232, 240) };
            chk.CheckedChanged += OnUiSettingChanged;
            return chk;
        }

        private Label CreateAxisRow(Panel parent, int y, out Panel barFill, out Label valLbl, out CheckBox invChk)
        {
            Label lb = new Label { Location = new Point(12, y + 2), Size = new Size(128, 18) };
            Panel bg = new Panel { Location = new Point(142, y + 3), Size = new Size(154, 14), BackColor = Color.FromArgb(15, 23, 42) };
            Panel centerMark = new Panel { Location = new Point(76, 0), Size = new Size(2, 14), BackColor = Color.FromArgb(71, 85, 105) };
            barFill = new Panel { Location = new Point(77, 2), Size = new Size(2, 10), BackColor = Color.FromArgb(56, 189, 248) };
            bg.Controls.Add(barFill);
            bg.Controls.Add(centerMark);

            valLbl = new Label { Text = " 0.00", Location = new Point(302, y + 2), Size = new Size(48, 18), Font = new Font("Consolas", 8.5F) };
            invChk = new CheckBox { Text = "Inv", Location = new Point(356, y + 1), AutoSize = true };
            invChk.CheckedChanged += OnUiSettingChanged;

            parent.Controls.AddRange(new Control[] { lb, bg, valLbl, invChk });
            return lb;
        }

        private Label CreateSliderRow(Panel parent, int y, int min, int max, int val, out TrackBar trk, out Label valLbl)
        {
            Label lb = new Label { Location = new Point(12, y + 4), Size = new Size(145, 18) };
            trk = new TrackBar { Location = new Point(158, y), Size = new Size(205, 28), Minimum = min, Maximum = max, Value = val, TickStyle = TickStyle.None, AutoSize = false };
            trk.Scroll += OnUiSettingChanged;
            valLbl = new Label { Text = val + "%", Location = new Point(366, y + 4), Size = new Size(48, 18), Font = new Font("Consolas", 8.5F) };
            parent.Controls.AddRange(new Control[] { lb, trk, valLbl });
            return lb;
        }
    }
}
