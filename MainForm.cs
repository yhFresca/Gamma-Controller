using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Serialization;
using System.Text;

namespace GammaControllerApp
{

    public partial class MainForm : Form
    {
        private ComboBox comboScreens;
        private TrackBar trackGamma, trackBrightness, trackContrast;
        private NumericUpDown numGamma, numBrightness, numContrast;
        private Button btnReset;

        private Label labelProfileTitle;
        private TextBox txtProfileName;
        private TextBox txtHotkeyCapture;
        private Button btnSaveProfile;
        private Button btnDeleteProfile;
        private ListBox listProfiles;

        private Keys tempMainKey = Keys.None;
        private Keys tempModifiers = Keys.None;
        private List<GammaProfile> profiles = new List<GammaProfile>();
        private int currentHotkeyIdCounter = 100;
        private const string SAVE_FILE = "profiles.xml";
        private bool isSyncing = false;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        const int MOD_ALT = 0x0001;
        const int MOD_CONTROL = 0x0002;
        const int MOD_SHIFT = 0x0004;
        const int WM_HOTKEY = 0x0312;

        public MainForm()
        {
            InitializeCustomComponents();
            InitializeScreens();
            BindEvents();
            ResetToDefaultValues();
            LoadProfilesFromFile();
        }

        private void InitializeCustomComponents()
        {
            this.Size = new Size(700, 420);
            this.Text = "Gamma Controller";
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;


            Label lblSc = new Label() { Text = "Monitor:", Location = new Point(20, 25), AutoSize = true };
            comboScreens = new ComboBox() { Location = new Point(100, 20), Size = new Size(200, 25), DropDownStyle = ComboBoxStyle.DropDownList };

            Label l1 = new Label() { Text = "Gamma:", Location = new Point(20, 70), AutoSize = true };
            trackGamma = new TrackBar() { Location = new Point(90, 70), Size = new Size(200, 45), Minimum = 0, Maximum = 30, TickFrequency = 2, Value = 10 };
            numGamma = new NumericUpDown() { Location = new Point(300, 70), Size = new Size(55, 25), DecimalPlaces = 2, Increment = 0.1M, Minimum = 0.0M, Maximum = 3.0M, Value = 1.0M };

            Label l2 = new Label() { Text = "Brightness:", Location = new Point(20, 120), AutoSize = true };
            trackBrightness = new TrackBar() { Location = new Point(90, 120), Size = new Size(200, 45), Minimum = 0, Maximum = 20, TickFrequency = 1, Value = 10 };
            numBrightness = new NumericUpDown() { Location = new Point(300, 120), Size = new Size(55, 25), DecimalPlaces = 2, Increment = 0.1M, Minimum = 0.0M, Maximum = 2.0M, Value = 1.0M };

            Label l3 = new Label() { Text = "Contrast:", Location = new Point(20, 170), AutoSize = true };
            trackContrast = new TrackBar() { Location = new Point(90, 170), Size = new Size(200, 45), Minimum = 40, Maximum = 60, TickFrequency = 1, Value = 50 };
            numContrast = new NumericUpDown() { Location = new Point(300, 170), Size = new Size(55, 25), DecimalPlaces = 3, Increment = 0.01M, Minimum = 0.8M, Maximum = 1.2M, Value = 1.0M };

            btnReset = new Button() { Text = "Reset Default", Location = new Point(100, 230), Size = new Size(200, 35) };
            btnReset.Click += (s, e) => ResetToDefaultValues();

            Label sep = new Label() { Text = "", BorderStyle = BorderStyle.Fixed3D, Location = new Point(380, 15), Size = new Size(2, 350) };


            labelProfileTitle = new Label() { Text = "Profile Manager", Location = new Point(400, 20), AutoSize = true, Font = new Font(this.Font, FontStyle.Bold) };

            Label lName = new Label() { Text = "Name:", Location = new Point(400, 55), AutoSize = true };
            txtProfileName = new TextBox() { Location = new Point(450, 52), Size = new Size(190, 23), Text = "Game Mode" };

            Label lKey = new Label() { Text = "Hotkey:", Location = new Point(400, 85), AutoSize = true };
            txtHotkeyCapture = new TextBox() { Location = new Point(450, 82), Size = new Size(190, 23), Text = "(Click to Set)", ReadOnly = false, BackColor = SystemColors.Window };

            btnSaveProfile = new Button() { Text = "Save Profile", Location = new Point(400, 115), Size = new Size(240, 30) };
            btnSaveProfile.Click += BtnSaveProfile_Click;

            listProfiles = new ListBox() { Location = new Point(400, 155), Size = new Size(240, 160) };

            btnDeleteProfile = new Button() { Text = "Delete Selected", Location = new Point(400, 325), Size = new Size(240, 30) };
            btnDeleteProfile.Click += BtnDeleteProfile_Click;

            this.Controls.AddRange(new Control[] {
                lblSc, comboScreens,
                l1, trackGamma, numGamma,
                l2, trackBrightness, numBrightness,
                l3, trackContrast, numContrast,
                btnReset,
                sep, labelProfileTitle, lName, txtProfileName, lKey, txtHotkeyCapture, btnSaveProfile, listProfiles, btnDeleteProfile
            });
        }

        private void BindEvents()
        {
            comboScreens.SelectedIndexChanged += (s, e) => ApplyCurrentSettings();

            listProfiles.SelectedIndexChanged += (s, e) => {
                if (listProfiles.SelectedItem is GammaProfile profile) ApplyProfileToUI(profile);
            };

            trackGamma.Scroll += (s, e) => SyncTrackToNum(trackGamma, numGamma);
            trackBrightness.Scroll += (s, e) => SyncTrackToNum(trackBrightness, numBrightness);
            trackContrast.Scroll += (s, e) => SyncTrackToNum(trackContrast, numContrast, 50);

            numGamma.ValueChanged += (s, e) => { SyncNumToTrack(numGamma, trackGamma); ApplyCurrentSettings(); };
            numBrightness.ValueChanged += (s, e) => { SyncNumToTrack(numBrightness, trackBrightness); ApplyCurrentSettings(); };
            numContrast.ValueChanged += (s, e) => { SyncNumToTrack(numContrast, trackContrast, 50); ApplyCurrentSettings(); };

            txtHotkeyCapture.KeyDown += (s, e) => {
                e.SuppressKeyPress = true;
                if (e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.Menu) return;

                tempMainKey = e.KeyCode;
                tempModifiers = e.Modifiers;
                txtHotkeyCapture.Text = FormatHotkeyString(tempModifiers, tempMainKey);

                labelProfileTitle.Focus();
            };

            txtHotkeyCapture.Enter += (s, e) => { txtHotkeyCapture.BackColor = Color.LightYellow; txtHotkeyCapture.Text = "Press Keys..."; };
            txtHotkeyCapture.Leave += (s, e) => {
                txtHotkeyCapture.BackColor = SystemColors.Window;
                if (tempMainKey != Keys.None) txtHotkeyCapture.Text = FormatHotkeyString(tempModifiers, tempMainKey);
                else txtHotkeyCapture.Text = "(Click to Set)";
            };
        }

        private void InitializeScreens()
        {
            comboScreens.Items.Add("All Screens");
            foreach (var screen in Screen.AllScreens) comboScreens.Items.Add(screen.DeviceName.Replace(@"\\.\", ""));
            comboScreens.SelectedIndex = 0;
        }

        private void SyncTrackToNum(TrackBar track, NumericUpDown num, int factor = 10)
        {
            if (isSyncing) return;
            isSyncing = true;
            decimal val = (decimal)(track.Value / (decimal)factor);
            if (val < num.Minimum) val = num.Minimum;
            if (val > num.Maximum) val = num.Maximum;
            num.Value = val;
            isSyncing = false;
        }

        private void SyncNumToTrack(NumericUpDown num, TrackBar track, int factor = 10)
        {
            if (isSyncing) return;
            isSyncing = true;
            int val = (int)(num.Value * factor);
            if (val < track.Minimum) val = track.Minimum;
            if (val > track.Maximum) val = track.Maximum;
            track.Value = val;
            isSyncing = false;
        }

        private void ApplyCurrentSettings()
        {
            double g = (double)numGamma.Value;
            double b = (double)numBrightness.Value;
            double c = (double)numContrast.Value;
            if (comboScreens.SelectedIndex == 0)
                foreach (var screen in Screen.AllScreens) GammaManager.SetGamma(g, b, c, screen.DeviceName);
            else
            {
                string name = null;
                if (comboScreens.SelectedIndex > 0) name = Screen.AllScreens[comboScreens.SelectedIndex - 1].DeviceName;
                if (name != null) GammaManager.SetGamma(g, b, c, name);
            }
        }

        private void ApplyProfileToUI(GammaProfile p)
        {
            numGamma.Value = (decimal)p.Gamma;
            numBrightness.Value = (decimal)p.Brightness;
            numContrast.Value = (decimal)p.Contrast;
            txtProfileName.Text = p.Name;
            tempMainKey = p.MainKey;
            tempModifiers = p.Modifiers;
            txtHotkeyCapture.Text = FormatHotkeyString(p.Modifiers, p.MainKey);
        }

        private void ResetToDefaultValues()
        {
            numGamma.Value = 1.0M;
            numBrightness.Value = 1.0M;
            numContrast.Value = 1.0M;
            tempMainKey = Keys.None;
            tempModifiers = Keys.None;
            txtHotkeyCapture.Text = "(Click to Set)";
        }

        private string FormatHotkeyString(Keys mods, Keys key)
        {
            var sb = new StringBuilder();
            if ((mods & Keys.Control) != 0) sb.Append("Ctrl + ");
            if ((mods & Keys.Alt) != 0) sb.Append("Alt + ");
            if ((mods & Keys.Shift) != 0) sb.Append("Shift + ");
            sb.Append(key.ToString());
            return sb.ToString();
        }

        private int GetWin32Modifiers(Keys modifiers)
        {
            int fsModifiers = 0;
            if ((modifiers & Keys.Alt) != 0) fsModifiers |= MOD_ALT;
            if ((modifiers & Keys.Control) != 0) fsModifiers |= MOD_CONTROL;
            if ((modifiers & Keys.Shift) != 0) fsModifiers |= MOD_SHIFT;
            return fsModifiers;
        }

        private void BtnSaveProfile_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProfileName.Text)) { MessageBox.Show("Please enter a name."); return; }
            if (tempMainKey == Keys.None) { MessageBox.Show("Please click the box and press a key."); return; }

            if (profiles.Any(p => p.MainKey == tempMainKey && p.Modifiers == tempModifiers))
            { MessageBox.Show("Hotkey already used!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }

            var profile = new GammaProfile
            {
                Name = txtProfileName.Text,
                Gamma = (double)numGamma.Value,
                Brightness = (double)numBrightness.Value,
                Contrast = (double)numContrast.Value,
                MainKey = tempMainKey,
                Modifiers = tempModifiers,
                HotkeyId = currentHotkeyIdCounter++
            };

            int fsModifiers = GetWin32Modifiers(profile.Modifiers);
            if (RegisterHotKey(this.Handle, profile.HotkeyId, fsModifiers, (int)profile.MainKey))
            {
                profiles.Add(profile);
                RefreshProfileList();
                SaveProfilesToFile();
            }
            else MessageBox.Show("Failed to register hotkey.");
        }

        private void SaveProfilesToFile()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<GammaProfile>));
                using (FileStream fs = new FileStream(SAVE_FILE, FileMode.Create)) serializer.Serialize(fs, profiles);
            }
            catch (Exception ex) { MessageBox.Show("Save error: " + ex.Message); }
        }

        private void LoadProfilesFromFile()
        {
            if (!File.Exists(SAVE_FILE)) return;
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<GammaProfile>));
                using (FileStream fs = new FileStream(SAVE_FILE, FileMode.Open)) profiles = (List<GammaProfile>)serializer.Deserialize(fs);
                foreach (var p in profiles)
                {
                    int fsModifiers = GetWin32Modifiers(p.Modifiers);
                    if (!RegisterHotKey(this.Handle, p.HotkeyId, fsModifiers, (int)p.MainKey)) p.HotkeyId = -1;
                }
                if (profiles.Count > 0) currentHotkeyIdCounter = profiles.Max(p => p.HotkeyId) + 1;
                RefreshProfileList();
            }
            catch { }
        }

        private void BtnDeleteProfile_Click(object sender, EventArgs e)
        {
            if (listProfiles.SelectedItem is GammaProfile profile)
            {
                UnregisterHotKey(this.Handle, profile.HotkeyId);
                profiles.Remove(profile);
                RefreshProfileList();
                SaveProfilesToFile();
            }
        }

        private void RefreshProfileList() { listProfiles.DataSource = null; listProfiles.DataSource = profiles; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            foreach (var p in profiles) UnregisterHotKey(this.Handle, p.HotkeyId);
            foreach (var screen in Screen.AllScreens) GammaManager.SetGamma(1.0, 1.0, 1.0, screen.DeviceName);
            base.OnFormClosing(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                var profile = profiles.FirstOrDefault(p => p.HotkeyId == id);
                if (profile != null) ApplyProfileToUI(profile);
            }
            base.WndProc(ref m);
        }
    }
    public class GammaProfile
    {
        public string Name { get; set; }
        public double Gamma { get; set; }
        public double Brightness { get; set; }
        public double Contrast { get; set; }
        public Keys MainKey { get; set; }
        public Keys Modifiers { get; set; }
        public int HotkeyId { get; set; }

        public GammaProfile() { }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append(Name).Append(" [");
            if ((Modifiers & Keys.Control) != 0) sb.Append("Ctrl+");
            if ((Modifiers & Keys.Alt) != 0) sb.Append("Alt+");
            if ((Modifiers & Keys.Shift) != 0) sb.Append("Shift+");
            sb.Append(MainKey.ToString());
            sb.Append("]");
            return sb.ToString();
        }
    }
}