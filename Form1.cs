using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Linq;
using System.Diagnostics;
using System.IO;

namespace CrosshairApp
{
    public partial class Form1 : Form
    {
        private Color chColor = Color.Lime;
        private int chStyle = 0;
        private int chSize = 20;
        private int chThick = 2;
        private int chGap = 5;
        private int chOpacity = 255;
        private Keys chHotkey = Keys.F2;
        private bool chVisible = false;
        private bool chShowOnStart = false;
        private bool chLaunchWithWindows = false;

        private OverlayForm overlay;
        private NotifyIcon tray;
        private Button[] styleBtns = new Button[6];
        private Button btnToggle;
        private Label lblHotkey;
        private TrackBar tbSize, tbThick, tbGap, tbOpacity;
        private Label lblSizeVal, lblThickVal, lblGapVal, lblOpacityVal;
        private Panel pnlSize, pnlGap, pnlOpacity;
        private CheckBox chkOnStart, chkWindows;
        private CheckedListBox gameList;
        private System.Windows.Forms.Timer gameWatcher;
        private bool syncingCheckboxes = false;
        private Dictionary<string, string> installedGames = new();
        private HashSet<string> enabledGames = new();
        private string? lastDetectedGame = null;

        public Form1()
        {
            InitializeComponent();
            overlay = new OverlayForm();
            BuildUI();
            SetupTray();
            LoadSettings();
            SetupGameWatcher();
            bool startedWithWindows = Environment.GetCommandLineArgs().Contains("--startup");
            if (startedWithWindows && chShowOnStart) ShowCrosshair();
            else if (!startedWithWindows) ShowCrosshair();
        }

        private void BuildUI()
        {
            this.Text = "Crosshair Overlay";
            this.BackColor = Color.FromArgb(18, 18, 18);
            this.ForeColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(Screen.PrimaryScreen.WorkingArea.Width / 2 + 200, Screen.PrimaryScreen.WorkingArea.Height / 2 - 500);
            this.Font = new Font("Segoe UI", 10f);
            this.ShowInTaskbar = false;
            this.Visible = false;

            int pad = 28;
            int W = 660;
            int W2 = W - pad * 2;
            int y = pad;

            // TITRE
            Add(new Label { Text = "CROSSHAIR OVERLAY", Font = new Font("Segoe UI", 16f, FontStyle.Bold), ForeColor = Color.FromArgb(91, 155, 213), Size = new Size(W2, 40), Location = new Point(pad, y), TextAlign = ContentAlignment.MiddleCenter });
            y += 60;

            // STYLE
            AddSep(pad, y, W2); y += 14;
            AddLabel("Style", pad, y); y += 36;
            string[] sn = { "+ Croix", "• Point", "⊕ Croix+Pt", "⊞ CS Gap", "○ Cercle", "⊙ Cerc+Croix" };
            int bw = (W2 - 10) / 3;
            for (int i = 0; i < 6; i++)
            {
                int idx = i;
                var b = MakeBtn(sn[i], pad + (i % 3) * (bw + 5), y + (i / 3) * 48, bw, 42);
                b.Font = new Font("Segoe UI", 10f);
                b.BackColor = i == 0 ? Color.FromArgb(30, 100, 200) : Color.FromArgb(40, 40, 40);
                b.Click += (s, e) =>
                {
                    chStyle = idx;
                    foreach (var x in styleBtns) x.BackColor = Color.FromArgb(40, 40, 40);
                    b.BackColor = Color.FromArgb(30, 100, 200);
                    UpdateSliderVisibility();
                    UpdateOverlay();
                };
                styleBtns[i] = b;
            }
            y += 106;

            // COULEUR
            AddSep(pad, y, W2); y += 14;
            AddLabel("Couleur", pad, y); y += 36;
            var bpick = MakeBtn("🎨  Personnalisée", pad, y, 150, 40);
            bpick.BackColor = Color.FromArgb(50, 50, 50);
            bpick.Click += (s, e) =>
            {
                using var cd = new ColorDialog { FullOpen = true, Color = chColor };
                if (cd.ShowDialog() == DialogResult.OK) { chColor = cd.Color; UpdateOverlay(); }
            };
            Color[] qc = { Color.Lime, Color.Red, Color.Cyan, Color.White, Color.Yellow, Color.Magenta, Color.Orange, Color.DeepPink };
            int qw = (W2 - 155) / 8;
            for (int i = 0; i < 8; i++)
            {
                Color c = qc[i];
                var qb = MakeBtn("", pad + 155 + i * (qw + 3), y, qw, 40);
                qb.BackColor = c;
                qb.Click += (s, e) => { chColor = c; UpdateOverlay(); };
            }
            y += 60;

            // RÉGLAGES
            AddSep(pad, y, W2); y += 14;
            AddLabel("Réglages du crosshair", pad, y); y += 36;

            pnlSize = MakeSlider("Taille", pad, y, W2, 1, 100, 20, v => { chSize = v; UpdateOverlay(); }, out tbSize, out lblSizeVal);
            Add(pnlSize); y += 80;

            var pnlThick = MakeSlider("Épaisseur", pad, y, W2, 1, 20, 2, v => { chThick = v; UpdateOverlay(); }, out tbThick, out lblThickVal);
            Add(pnlThick); y += 80;

            pnlGap = MakeSlider("Gap Centre", pad, y, W2, 0, 40, 5, v => { chGap = v; UpdateOverlay(); }, out tbGap, out lblGapVal);
            Add(pnlGap); y += 80;

            pnlOpacity = MakeSlider("Opacité", pad, y, W2, 10, 255, 255, v => { chOpacity = v; UpdateOverlay(); }, out tbOpacity, out lblOpacityVal, true);
            Add(pnlOpacity); y += 80;

            // JEUX
            AddSep(pad, y, W2); y += 14;
            Add(new Label { Text = "DÉMARRAGE AUTOMATIQUE PAR JEU", Location = new Point(pad, y), Size = new Size(400, 26), ForeColor = Color.FromArgb(130, 130, 130), Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
            var btnRefresh = MakeBtn("🔄 Scanner", W - pad - 110, y - 2, 110, 28);
            btnRefresh.BackColor = Color.FromArgb(40, 70, 40);
            btnRefresh.Font = new Font("Segoe UI", 9f);
            btnRefresh.Click += (s, e) => RefreshGameList();
            y += 36;

            gameList = new CheckedListBox
            {
                Location = new Point(pad, y),
                Size = new Size(W2, 160),
                BackColor = Color.FromArgb(28, 28, 28),
                ForeColor = Color.FromArgb(210, 210, 210),
                CheckOnClick = true,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10f),
                ItemHeight = 28
            };
            gameList.ItemCheck += (s, e) =>
            {
                this.BeginInvoke(new Action(() =>
                {
                    enabledGames.Clear();
                    for (int i = 0; i < gameList.Items.Count; i++)
                    {
                        if (gameList.GetItemChecked(i))
                        {
                            var gn = gameList.Items[i].ToString()!;
                            var proc = installedGames.FirstOrDefault(x => x.Value == gn).Key;
                            if (!string.IsNullOrEmpty(proc)) enabledGames.Add(proc);
                        }
                    }
                }));
            };
            Add(gameList);
            y += 178;

            // OPTIONS
            AddSep(pad, y, W2); y += 14;
            AddLabel("Options", pad, y); y += 36;

            // Checkbox afficher au démarrage
            chkOnStart = new CheckBox
            {
                Text = "Afficher le crosshair au démarrage",
                Location = new Point(pad, y),
                AutoSize = true,
                ForeColor = Color.FromArgb(190, 190, 190),
                BackColor = Color.FromArgb(18, 18, 18),
                Font = new Font("Segoe UI", 10f)
            };
            chkOnStart.CheckedChanged += (s, e) =>
            {
                if (syncingCheckboxes) return;
                syncingCheckboxes = true;
                if (chkOnStart.Checked) chkWindows.Checked = true;
                else chkWindows.Checked = false;
                syncingCheckboxes = false;
            };
            Add(chkOnStart);
            y += 36;

            // Checkbox lancer avec Windows + ⓘ dans un panel
            var pnlWin = new Panel
            {
                Location = new Point(pad, y),
                Size = new Size(W2, 34),
                BackColor = Color.FromArgb(18, 18, 18)
            };

            chkWindows = new CheckBox
            {
                Text = "Lancer au démarrage",
                Location = new Point(0, 3),
                AutoSize = true,
                ForeColor = Color.FromArgb(190, 190, 190),
                BackColor = Color.FromArgb(18, 18, 18),
                Font = new Font("Segoe UI", 10f)
            };
            chkWindows.CheckedChanged += (s, e) =>
            {
                if (syncingCheckboxes) return;
                syncingCheckboxes = true;
                if (!chkWindows.Checked) chkOnStart.Checked = false;
                syncingCheckboxes = false;
            };
            pnlWin.Controls.Add(chkWindows);

            var lblInfo = new Label
            {
                Text = "ⓘ",
                Location = new Point(175, 4),
                Size = new Size(24, 28),
                ForeColor = Color.FromArgb(91, 155, 213),
                Font = new Font("Segoe UI", 12f),
                BackColor = Color.FromArgb(18, 18, 18),
                TextAlign = ContentAlignment.MiddleCenter
            };

            // Repositionne le ⓘ après que le checkbox soit rendu
            pnlWin.Layout += (s, e) =>
            {
                lblInfo.Location = new Point(chkWindows.Width + 6, 4);
            };

            var tooltip = new ToolTip { AutoPopDelay = 10000, InitialDelay = 200, ShowAlways = true };
            tooltip.SetToolTip(lblInfo, "Démarre en arrière-plan au lancement de Windows.\nLe crosshair s'active uniquement via le raccourci clavier\nou si un jeu sélectionné est lancé,\nsauf si \"Afficher le crosshair au démarrage\" est coché.");
            pnlWin.Controls.Add(lblInfo);
            Add(pnlWin);
            y += 58;

            // RACCOURCI
            AddSep(pad, y, W2); y += 14;
            AddLabel("Raccourci clavier", pad, y); y += 32;
            lblHotkey = new Label
            {
                Text = "F2",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(91, 155, 213),
                Size = new Size(W2 - 130, 36),
                Location = new Point(pad, y),
                TextAlign = ContentAlignment.MiddleLeft
            };
            Add(lblHotkey);
            var bhk = MakeBtn("Changer", W - pad - 120, y, 120, 36);
            bhk.BackColor = Color.FromArgb(50, 50, 50);
            bhk.Click += (s, e) =>
            {
                var d = new HotkeyDialog(chHotkey);
                if (d.ShowDialog() == DialogResult.OK)
                {
                    chHotkey = d.SelectedKey;
                    lblHotkey.Text = chHotkey.ToString();
                    UnregisterHotKey(this.Handle, 1);
                    RegisterHotKey(this.Handle, 1, 0, (uint)chHotkey);
                }
            };
            y += 50;

            // BOUTONS
            AddSep(pad, y, W2); y += 20;
            int bw5 = (W2 - 20) / 5;

            btnToggle = MakeBtn("⏸  Cacher", pad, y, bw5, 50);
            btnToggle.BackColor = Color.FromArgb(25, 100, 55);
            btnToggle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnToggle.Click += (s, e) => Toggle();

            var btnMin = MakeBtn("Réduire", pad + bw5 + 5, y, bw5, 50);
            btnMin.BackColor = Color.FromArgb(55, 55, 55);
            btnMin.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            btnMin.Click += (s, e) => HideSettings();

            var bs = MakeBtn("Sauvegarder", pad + (bw5 + 5) * 2, y, bw5, 50);
            bs.BackColor = Color.FromArgb(20, 65, 120);
            bs.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            bs.Click += Save;

            var breset = MakeBtn("Réinitialiser", pad + (bw5 + 5) * 3, y, bw5, 50);
            breset.BackColor = Color.FromArgb(80, 60, 20);
            breset.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            breset.Click += (s, e) =>
            {
                if (MessageBox.Show("Réinitialiser tous les paramètres ?", "Confirmer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    ResetSettings();
            };

            var bq = MakeBtn("✕  Quitter", pad + (bw5 + 5) * 4, y, bw5, 50);
            bq.BackColor = Color.FromArgb(100, 25, 25);
            bq.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            bq.Click += (s, e) => Quit();

            y += 68;
            this.ClientSize = new Size(W, y);

            System.Threading.Tasks.Task.Run(() =>
            {
                installedGames = GameDatabase.DetectInstalledGames();
                this.Invoke(new Action(() => RefreshGameList()));
            });
        }

        private void RefreshGameList()
        {
            gameList.Items.Clear();
            var sorted = installedGames.Values.OrderBy(x => x).ToList();
            foreach (var game in sorted)
            {
                var proc = installedGames.FirstOrDefault(x => x.Value == game).Key;
                bool isChecked = enabledGames.Contains(proc);
                gameList.Items.Add(game, isChecked);
            }
            if (gameList.Items.Count == 0)
                gameList.Items.Add("Aucun jeu compatible détecté — cliquez sur 🔄 Scanner");
        }

        private void UpdateSliderVisibility()
        {
            pnlSize.Visible = chStyle != 1;
            pnlGap.Visible = chStyle == 3;
            pnlOpacity.Visible = true;
        }

        private void AddSep(int x, int y, int w)
        {
            Add(new Panel { Location = new Point(x, y), Size = new Size(w, 1), BackColor = Color.FromArgb(45, 45, 45) });
        }

        private void AddLabel(string text, int x, int y)
        {
            Add(new Label { Text = text.ToUpper(), Location = new Point(x, y), Size = new Size(400, 26), ForeColor = Color.FromArgb(130, 130, 130), Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        }

        private Panel MakeSlider(string name, int x, int y, int w, int min, int max, int val, Action<int> onChange, out TrackBar tbOut, out Label lblOut, bool pct = false)
        {
            var pnl = new Panel { Location = new Point(x, y), Size = new Size(w, 76), BackColor = Color.Transparent };
            pnl.Controls.Add(new Label { Text = name, Location = new Point(0, 4), Size = new Size(w - 80, 28), ForeColor = Color.White, Font = new Font("Segoe UI", 11f, FontStyle.Bold) });
            var vl = new Label { Text = pct ? "100%" : val.ToString(), Location = new Point(w - 78, 4), Size = new Size(78, 28), ForeColor = Color.FromArgb(91, 155, 213), Font = new Font("Segoe UI", 11f, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight };
            pnl.Controls.Add(vl);
            var tb = new TrackBar { Minimum = min, Maximum = max, Value = Math.Clamp(val, min, max), Location = new Point(-6, 34), Size = new Size(w + 12, 38), BackColor = Color.FromArgb(18, 18, 18), TickStyle = TickStyle.None };
            tb.Scroll += (s, e) => { vl.Text = pct ? (tb.Value * 100 / 255) + "%" : tb.Value.ToString(); onChange(tb.Value); };
            pnl.Controls.Add(tb);
            tbOut = tb;
            lblOut = vl;
            return pnl;
        }

        private void Add(Control c) => this.Controls.Add(c);

        private Button MakeBtn(string text, int x, int y, int w, int h)
        {
            var b = new Button { Text = text, Size = new Size(w, h), Location = new Point(x, y), FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Color.FromArgb(40, 40, 40), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderSize = 0;
            Add(b);
            return b;
        }

        private void SetupGameWatcher()
        {
            gameWatcher = new System.Windows.Forms.Timer();
            gameWatcher.Interval = 2000;
            gameWatcher.Tick += (s, e) =>
            {
                try
                {
                    var running = Process.GetProcesses().Select(p => p.ProcessName.ToLower()).ToHashSet();
                    string? detected = enabledGames.FirstOrDefault(g => running.Contains(g.ToLower()));
                    if (detected != null && lastDetectedGame == null) { if (!chVisible) ShowCrosshair(); lastDetectedGame = detected; }
                    else if (detected == null && lastDetectedGame != null) { HideCrosshair(); lastDetectedGame = null; }
                }
                catch { }
            };
            gameWatcher.Start();
        }

        private void UpdateOverlay()
        {
            if (chVisible) overlay.UpdateSettings(chColor, chSize, chThick, chGap, chOpacity, chStyle);
        }

        private void ShowCrosshair()
        {
            overlay.UpdateSettings(chColor, chSize, chThick, chGap, chOpacity, chStyle);
            overlay.Show();
            chVisible = true;
            if (btnToggle != null) btnToggle.Text = "⏸  Cacher";
        }

        private void HideCrosshair()
        {
            overlay.Hide();
            chVisible = false;
            if (btnToggle != null) btnToggle.Text = "▶  Afficher";
        }

        private void Toggle()
        {
            if (chVisible) HideCrosshair();
            else ShowCrosshair();
        }

        private void HideSettings()
        {
            this.Visible = false;
            this.ShowInTaskbar = false;
        }

        private void SetupTray()
        {
            tray = new NotifyIcon { Icon = this.Icon ?? SystemIcons.Application, Text = "Crosshair Overlay", Visible = true };
            var m = new ContextMenuStrip();
            m.Items.Add("Paramètres", null, (s, e) => ShowSettings());
            m.Items.Add("Afficher/Cacher", null, (s, e) => Toggle());
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Quitter", null, (s, e) => Quit());
            tray.ContextMenuStrip = m;
            tray.DoubleClick += (s, e) => ShowSettings();
        }

        private void ShowSettings()
        {
            this.ShowInTaskbar = true;
            this.Visible = true;
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
        }

        private void ResetSettings()
        {
            chColor = Color.Lime;
            chStyle = 0; chSize = 20; chThick = 2; chGap = 5; chOpacity = 255;
            chHotkey = Keys.F2;
            tbSize.Value = 20; tbThick.Value = 2; tbGap.Value = 5; tbOpacity.Value = 255;
            lblSizeVal.Text = "20"; lblThickVal.Text = "2"; lblGapVal.Text = "5"; lblOpacityVal.Text = "100%";
            lblHotkey.Text = "F2";
            syncingCheckboxes = true;
            chkOnStart.Checked = false; chkWindows.Checked = false;
            syncingCheckboxes = false;
            foreach (var b in styleBtns) b.BackColor = Color.FromArgb(40, 40, 40);
            styleBtns[0].BackColor = Color.FromArgb(30, 100, 200);
            for (int i = 0; i < gameList.Items.Count; i++) gameList.SetItemChecked(i, false);
            enabledGames.Clear();
            UpdateSliderVisibility();
            UpdateOverlay();
            Registry.CurrentUser.DeleteSubKey(@"SOFTWARE\CrosshairApp", false);
            var run = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            run?.DeleteValue("CrosshairApp", false);
            MessageBox.Show("✅ Paramètres réinitialisés !", "Réinitialisation", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Save(object sender, EventArgs e)
        {
            chShowOnStart = chkOnStart.Checked;
            chLaunchWithWindows = chkWindows.Checked;
            var k = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\CrosshairApp");
            k.SetValue("Color", chColor.ToArgb());
            k.SetValue("Size", chSize);
            k.SetValue("Thick", chThick);
            k.SetValue("Gap", chGap);
            k.SetValue("Opacity", chOpacity);
            k.SetValue("Style", chStyle);
            k.SetValue("Hotkey", (int)chHotkey);
            k.SetValue("ShowOnStart", chShowOnStart ? 1 : 0);
            k.SetValue("LaunchWithWindows", chLaunchWithWindows ? 1 : 0);
            k.SetValue("EnabledGames", string.Join(",", enabledGames));
            var run = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (chLaunchWithWindows)
                run?.SetValue("CrosshairApp", $"\"{Application.ExecutablePath}\" --startup");
            else
                run?.DeleteValue("CrosshairApp", false);
            MessageBox.Show("✅ Sauvegardé !", "Crosshair", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void LoadSettings()
        {
            var k = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\CrosshairApp");
            if (k == null) return;
            try
            {
                chColor = Color.FromArgb((int)k.GetValue("Color", Color.Lime.ToArgb()));
                chSize = (int)k.GetValue("Size", 20);
                chThick = (int)k.GetValue("Thick", 2);
                chGap = (int)k.GetValue("Gap", 5);
                chOpacity = (int)k.GetValue("Opacity", 255);
                chStyle = (int)k.GetValue("Style", 0);
                chHotkey = (Keys)(int)k.GetValue("Hotkey", (int)Keys.F2);
                chShowOnStart = (int)k.GetValue("ShowOnStart", 0) == 1;
                chLaunchWithWindows = (int)k.GetValue("LaunchWithWindows", 0) == 1;
                var savedGames = k.GetValue("EnabledGames", "")?.ToString() ?? "";
                enabledGames = savedGames.Split(',', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
                tbSize.Value = Math.Clamp(chSize, 1, 100);
                tbThick.Value = Math.Clamp(chThick, 1, 20);
                tbGap.Value = Math.Clamp(chGap, 0, 40);
                tbOpacity.Value = Math.Clamp(chOpacity, 10, 255);
                lblSizeVal.Text = chSize.ToString();
                lblThickVal.Text = chThick.ToString();
                lblGapVal.Text = chGap.ToString();
                lblOpacityVal.Text = (chOpacity * 100 / 255) + "%";
                lblHotkey.Text = chHotkey.ToString();
                syncingCheckboxes = true;
                chkOnStart.Checked = chShowOnStart;
                chkWindows.Checked = chLaunchWithWindows;
                syncingCheckboxes = false;
                foreach (var b in styleBtns) b.BackColor = Color.FromArgb(40, 40, 40);
                if (chStyle >= 0 && chStyle < 6) styleBtns[chStyle].BackColor = Color.FromArgb(30, 100, 200);
                UpdateSliderVisibility();
            }
            catch { }
        }

        private void Quit()
        {
            gameWatcher?.Stop();
            tray.Visible = false;
            overlay.Dispose();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideSettings(); }
            else base.OnFormClosing(e);
        }

        public static void DrawCH(Graphics g, int cx, int cy, int size, int thick, int gap, Color color, int style)
        {
            using var pen = new Pen(color, thick) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            using var brush = new SolidBrush(color);
            switch (style)
            {
                case 0: g.DrawLine(pen, cx - size, cy, cx + size, cy); g.DrawLine(pen, cx, cy - size, cx, cy + size); break;
                case 1: int r = Math.Max(thick, 1); g.FillEllipse(brush, cx - r, cy - r, r * 2, r * 2); break;
                case 2: g.DrawLine(pen, cx - size, cy, cx + size, cy); g.DrawLine(pen, cx, cy - size, cx, cy + size); g.FillEllipse(brush, cx - thick, cy - thick, thick * 2, thick * 2); break;
                case 3: g.DrawLine(pen, cx - size, cy, cx - gap, cy); g.DrawLine(pen, cx + gap, cy, cx + size, cy); g.DrawLine(pen, cx, cy - size, cx, cy - gap); g.DrawLine(pen, cx, cy + gap, cx, cy + size); break;
                case 4: g.DrawEllipse(pen, cx - size, cy - size, size * 2, size * 2); break;
                case 5: g.DrawEllipse(pen, cx - size, cy - size, size * 2, size * 2); g.DrawLine(pen, cx - size, cy, cx + size, cy); g.DrawLine(pen, cx, cy - size, cx, cy + size); break;
            }
        }

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RegisterHotKey(this.Handle, 1, 0, (uint)chHotkey); }
        protected override void WndProc(ref Message m) { if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1) Toggle(); base.WndProc(ref m); }
    }

    public class HotkeyDialog : Form
    {
        public Keys SelectedKey { get; private set; }
        private Label lblKey;
        private Button btnOk;

        public HotkeyDialog(Keys current)
        {
            SelectedKey = current;
            this.Text = "Changer le raccourci";
            this.Size = new Size(360, 240);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(22, 22, 22);
            this.KeyPreview = true;

            this.Controls.Add(new Label { Text = "Appuie sur la touche souhaitée :", Location = new Point(24, 24), Size = new Size(312, 26), ForeColor = Color.FromArgb(160, 160, 160), Font = new Font("Segoe UI", 11f) });
            lblKey = new Label { Text = current.ToString(), Location = new Point(24, 60), Size = new Size(312, 58), Font = new Font("Segoe UI", 24f, FontStyle.Bold), ForeColor = Color.FromArgb(91, 155, 213), TextAlign = ContentAlignment.MiddleCenter };
            this.Controls.Add(lblKey);

            btnOk = new Button { Text = "✅  Confirmer", Size = new Size(148, 44), Location = new Point(24, 136), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(25, 100, 55), ForeColor = Color.White, Enabled = false, Font = new Font("Segoe UI", 11f) };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) => { this.DialogResult = DialogResult.OK; this.Close(); };
            this.Controls.Add(btnOk);

            var bc = new Button { Text = "Annuler", Size = new Size(140, 44), Location = new Point(184, 136), FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(80, 25, 25), ForeColor = Color.White, Font = new Font("Segoe UI", 11f) };
            bc.FlatAppearance.BorderSize = 0;
            bc.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(bc);

            this.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Escape && e.KeyCode != Keys.Enter)
                {
                    SelectedKey = e.KeyCode;
                    lblKey.Text = e.KeyCode.ToString();
                    btnOk.Enabled = true;
                }
            };
        }
    }

    public class OverlayForm : Form
    {
        private Color color = Color.Lime;
        private int size = 20, thick = 2, gap = 5, opacity = 255, style = 0;

        public OverlayForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.FromArgb(1, 1, 1);
            this.TransparencyKey = Color.FromArgb(1, 1, 1);
            this.WindowState = FormWindowState.Maximized;
            int ex = NativeMethods.GetWindowLong(this.Handle, -20);
            NativeMethods.SetWindowLong(this.Handle, -20, ex | 0x80000 | 0x20);
        }

        public void UpdateSettings(Color c, int s, int t, int g, int o, int st)
        {
            color = Color.FromArgb(o, c.R, c.G, c.B);
            size = s; thick = t; gap = g; opacity = o; style = st;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int cx = this.Width / 2;
            int cy = this.Height / 2;
            Form1.DrawCH(e.Graphics, cx, cy, size, thick, gap, color, style);
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    }
}