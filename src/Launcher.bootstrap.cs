using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WonderLauncher
{
    public class IniFile
    {
        private string path;
        [DllImport("kernel32")] private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
        [DllImport("kernel32")] private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);
        public IniFile(string iniPath) { path = iniPath; }
        public void Write(string section, string key, string value) { WritePrivateProfileString(section, key, value, path); }
        public string Read(string section, string key, string defaultVal) {
            StringBuilder temp = new StringBuilder(255);
            GetPrivateProfileString(section, key, defaultVal, temp, 255, path);
            return temp.ToString();
        }
    }

    public class MainForm : Form
    {
        private string iniPath;
        private IniFile ini;
        private int loadingProgress = 0;
        private Timer loadingTimer;
        private bool isPlayHover = false, isSettingsHover = false, isCloseHover = false, isMinHover = false;
        private Rectangle playRect = new Rectangle(565, 680, 185, 75);
        private Rectangle settingsRect = new Rectangle(685, 10, 35, 35);
        private Rectangle minRect = new Rectangle(725, 10, 35, 35);
        private Rectangle closeRect = new Rectangle(765, 10, 35, 35);
        
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;
        [DllImportAttribute("user32.dll")] public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")] public static extern bool ReleaseCapture();

        public MainForm()
        {
            this.DoubleBuffered = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Width = 800;
            this.Height = 800;
            this.Text = "Wonder Classic Launcher";

            string bgPath = Path.Combine(Application.StartupPath, "bg_clean.jpg");
            if (!File.Exists(bgPath)) bgPath = Path.Combine(Application.StartupPath, "bg.jpg"); // fallback
            if (File.Exists(bgPath))
            {
                this.BackgroundImage = Image.FromFile(bgPath);
                this.BackgroundImageLayout = ImageLayout.Stretch;
            }

            iniPath = Path.Combine(Directory.GetParent(Application.StartupPath).FullName, "tela_patch.ini");
            ini = new IniFile(iniPath);
            this.MouseDown += MainForm_MouseDown;
            this.MouseClick += MainForm_MouseClick;
            this.MouseMove += MainForm_MouseMove;
            
            loadingTimer = new Timer();
            loadingTimer.Interval = 25;
            loadingTimer.Tick += LoadingTimer_Tick;
            loadingTimer.Start();
        }

        private void LoadingTimer_Tick(object sender, EventArgs e)
        {
            loadingProgress += 2;
            if (loadingProgress >= 100)
            {
                loadingProgress = 100;
                loadingTimer.Stop();
            }
            this.Invalidate(new Rectangle(20, 680, 520, 70));
        }

        private void MainForm_MouseMove(object sender, MouseEventArgs e)
        {
            bool oldPlay = isPlayHover, oldSet = isSettingsHover, oldMin = isMinHover, oldClose = isCloseHover;
            isPlayHover = playRect.Contains(e.Location);
            isSettingsHover = settingsRect.Contains(e.Location);
            isMinHover = minRect.Contains(e.Location);
            isCloseHover = closeRect.Contains(e.Location);

            if (oldPlay != isPlayHover || oldSet != isSettingsHover || oldMin != isMinHover || oldClose != isCloseHover)
            {
                this.Cursor = (isPlayHover || isSettingsHover || isMinHover || isCloseHover) ? Cursors.Hand : Cursors.Default;
                this.Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            
            using (Font font = new Font("Segoe UI", 11, FontStyle.Bold))
            {
                if (loadingProgress < 100)
                {
                    e.Graphics.DrawString(string.Format("Verificando arquivos do cliente... ({0}%)", loadingProgress), font, Brushes.WhiteSmoke, 35, 695);
                    using (SolidBrush barBrush = new SolidBrush(Color.FromArgb(0, 160, 255)))
                    {
                        e.Graphics.FillRectangle(barBrush, 36, 726, (int)(494 * (loadingProgress / 100.0f)), 10);
                    }
                }
                else
                {
                    e.Graphics.DrawString("Pronto! Sistema atualizado e pronto para jogar.", font, Brushes.LimeGreen, 35, 695);
                    using (SolidBrush barBrush = new SolidBrush(Color.LimeGreen))
                    {
                        e.Graphics.FillRectangle(barBrush, 36, 726, 494, 10);
                    }
                }
            }

            if (isPlayHover) e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(45, 255, 255, 255)), playRect);
            if (isSettingsHover) e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(40, 255, 255, 255)), settingsRect);
            if (isMinHover) e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(40, 255, 255, 255)), minRect);
            if (isCloseHover) e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(80, 255, 50, 50)), closeRect);
        }

        private void MainForm_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && e.Y < 80 && e.X < 650) { ReleaseCapture(); SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0); }
        }

        private void MainForm_MouseClick(object sender, MouseEventArgs e)
        {
            if (playRect.Contains(e.Location))
            {
                if (loadingProgress < 100) { MessageBox.Show("Aguarde a verificação dos arquivos!", "Wonder Classic"); return; }
                LaunchGame();
            }
            else if (closeRect.Contains(e.Location)) Application.Exit();
            else if (minRect.Contains(e.Location)) this.WindowState = FormWindowState.Minimized;
            else if (settingsRect.Contains(e.Location)) ShowSettings();
        }

        private void LaunchGame()
        {
            string exePath = Path.Combine(Directory.GetParent(Application.StartupPath).FullName, "Load.exe");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo() { FileName = exePath, WorkingDirectory = Directory.GetParent(Application.StartupPath).FullName });
                Application.Exit();
            }
            else MessageBox.Show("Load.exe não encontrado na pasta principal do jogo!", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowSettings()
        {
            Form settingsForm = new Form();
            settingsForm.Text = "Configurações do Jogo";
            settingsForm.Size = new Size(350, 250);
            settingsForm.StartPosition = FormStartPosition.CenterParent;
            settingsForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            settingsForm.MaximizeBox = false;
            settingsForm.MinimizeBox = false;
            settingsForm.BackColor = Color.FromArgb(32, 40, 52);
            settingsForm.ForeColor = Color.White;

            Label lblModo = new Label() { Text = "Modo de Janela:", Location = new Point(20, 20), AutoSize = true };
            ComboBox cmbModo = new ComboBox() { Location = new Point(20, 40), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbModo.Items.Add("Modo Janela (Redimensionável)");
            cmbModo.Items.Add("Tela Cheia HD (Recomendado)");
            cmbModo.Items.Add("Original Clássico (1024x768)");

            Label lblProp = new Label() { Text = "Proporção de Tela:", Location = new Point(20, 80), AutoSize = true };
            ComboBox cmbProp = new ComboBox() { Location = new Point(20, 100), Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbProp.Items.Add("Manter 4:3 (Com Bordas Pretas)");
            cmbProp.Items.Add("Esticar (Sem Bordas)");

            Button btnSave = new Button() { Text = "Salvar", Location = new Point(20, 160), Width = 100, Height = 30, BackColor = Color.FromArgb(50, 60, 70), FlatStyle = FlatStyle.Flat };
            btnSave.Click += (s, e) => {
                if (cmbModo.SelectedIndex == 0) ini.Write("Tela", "Modo", "1");
                else if (cmbModo.SelectedIndex == 1) ini.Write("Tela", "Modo", "2");
                else if (cmbModo.SelectedIndex == 2) ini.Write("Tela", "Modo", "3");

                if (cmbProp.SelectedIndex == 0) ini.Write("Tela", "ManterProporcao", "1");
                else if (cmbProp.SelectedIndex == 1) ini.Write("Tela", "ManterProporcao", "0");

                MessageBox.Show("Configurações salvas com sucesso!", "Sucesso", MessageBoxButtons.OK, MessageBoxIcon.Information);
                settingsForm.Close();
            };

            string currModo = ini.Read("Tela", "Modo", "2");
            string currProp = ini.Read("Tela", "ManterProporcao", "1");

            if (currModo == "1") cmbModo.SelectedIndex = 0;
            else if (currModo == "3") cmbModo.SelectedIndex = 2;
            else cmbModo.SelectedIndex = 1;

            if (currProp == "0") cmbProp.SelectedIndex = 1;
            else cmbProp.SelectedIndex = 0;

            settingsForm.Controls.Add(lblModo);
            settingsForm.Controls.Add(cmbModo);
            settingsForm.Controls.Add(lblProp);
            settingsForm.Controls.Add(cmbProp);
            settingsForm.Controls.Add(btnSave);

            settingsForm.ShowDialog(this);
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
