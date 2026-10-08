using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace WonderLauncher;

public class MainForm : Form
{
	private struct Alvo
	{
		public bool Critico;

		public string Rel;

		public string Hash;
	}

	private const float ART = 1024f;

	private const float BarL = 46f;

	private const float BarR = 705f;

	private const float BarTop = 929f;

	private const float BarBot = 955f;

	private const float BarMid = 942f;

	private const float BarTip = 12f;

	public const int WM_NCLBUTTONDOWN = 161;

	public const int HT_CAPTION = 2;

	private readonly HotSpot btnSettings = new HotSpot(882f, 12f, 37f, 37f);

	private readonly HotSpot btnMin = new HotSpot(928f, 12f, 38f, 37f);

	private readonly HotSpot btnClose = new HotSpot(974f, 12f, 38f, 37f);

	private readonly HotSpot btnPlay = new HotSpot(742f, 880f, 246f, 111f);

	private readonly HotSpot btnCadastro = new HotSpot(778f, 529f, 198f, 35f);

	private static readonly RectangleF PlayInner = new RectangleF(757f, 889f, 216f, 92f);

	private static readonly PointF BarTextPos = new PointF(374f, 903f);

	private readonly List<HotSpot> spots;

	private HotSpot pressed;

	private float S;

	private Image art;

	private Bitmap bgScaled;

	private readonly Timer anim;

	private readonly Stopwatch clock = Stopwatch.StartNew();

	private CheckState state;

	private volatile float realProgress;

	private float shownProgress;

	private string statusText = "PREPARANDO...";

	private StatusServidor servidor;

	private string errorText;

	private float readyFlash;

	private readonly string gameDir;

	private readonly IniFile ini;

	private readonly IniFile launcherIni;

	private readonly bool reiniciado;

	private Screen fittedScreen;

	private static readonly string[] Required = new string[6] { "Load.exe", "info.ini", "comprezz.dll", "MFC42D.DLL", "MFCO42D.DLL", "MSVCRTD.DLL" };

	private static readonly char[] HexD = "0123456789abcdef".ToCharArray();

	private static readonly byte[] WKHD = Encoding.ASCII.GetBytes("WKHDMOD");

	private static string serif;

	private const string ReiniciarParaAtualizar = "\u0001reiniciar";

	[DllImport("user32.dll")]
	private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

	[DllImport("user32.dll")]
	private static extern bool ReleaseCapture();

	public MainForm()
	{
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e0: Expected Obj, but got Unknown
		((Control)this).SetStyle((ControlStyles)139282, true);
		((Form)this).FormBorderStyle = (FormBorderStyle)0;
		((Form)this).StartPosition = (FormStartPosition)0;
		((ContainerControl)this).AutoScaleMode = (AutoScaleMode)0;
		((Control)this).BackColor = Color.FromArgb(22, 28, 30);
		((Control)this).Text = "Wonder Classic Launcher";
		try
		{
			((Form)this).Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
		}
		catch
		{
		}
		art = LoadArt();
		FitToScreen(Screen.FromPoint(Cursor.Position), null);
		spots = new List<HotSpot> { btnSettings, btnMin, btnClose, btnPlay, btnCadastro };
		gameDir = FindGameDir();
		ini = new IniFile(Path.Combine(gameDir, "tela_patch.ini"));
		launcherIni = new IniFile(Path.Combine(gameDir, "launcher.ini"));
		reiniciado = Array.IndexOf(Environment.GetCommandLineArgs(), "--atualizado") > 0;
		anim = new Timer
		{
			Interval = 15
		};
		anim.Tick += OnAnimTick;
		anim.Start();
		StartVerification();
		IniciarStatus();
	}

	private string HostServidor()
	{
		return new IniFile(Path.Combine(gameDir, "info.ini")).Read("info", "ip", "").Trim();
	}

	private int PortaLauncher()
	{
		if (!int.TryParse(launcherIni.Read("Launcher", "LauncherPorta", 12005.ToString()), out var result) || result <= 0 || result > 65535)
		{
			return 12005;
		}
		return result;
	}

	private void IniciarStatus()
	{
		servidor = new StatusServidor(HostServidor(), PortaLauncher());
		servidor.Mudou += () =>
		{
			try
			{
				if (((Control)this).IsHandleCreated && !((Control)this).IsDisposed)
				{
					((Control)this).BeginInvoke((Delegate)new Action(((Control)this).Invalidate));
				}
			}
			catch (InvalidOperationException)
			{
			}
		};
		servidor.Iniciar();
	}

	private void FitToScreen(Screen scr, Point? center)
	{
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected Obj, but got Unknown
		Rectangle workingArea = scr.WorkingArea;
		int val = (int)Math.Min(1024f, Math.Min((float)workingArea.Height * 0.82f, (float)workingArea.Width * 0.9f));
		val = Math.Max(val, Math.Min(420, Math.Min(workingArea.Width, workingArea.Height)));
		fittedScreen = scr;
		if (bgScaled == null || ((Image)bgScaled).Width != val)
		{
			Bitmap val2 = new Bitmap(val, val);
			Graphics val3 = Graphics.FromImage((Image)(object)val2);
			try
			{
				val3.InterpolationMode = (InterpolationMode)7;
				val3.PixelOffsetMode = (PixelOffsetMode)2;
				val3.DrawImage(art, new Rectangle(0, 0, val, val));
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
			if (bgScaled != null)
			{
				((Image)bgScaled).Dispose();
			}
			bgScaled = val2;
			S = (float)val / 1024f;
		}
		Point point = center ?? new Point(workingArea.Left + workingArea.Width / 2, workingArea.Top + workingArea.Height / 2);
		int x = Math.Max(workingArea.Left, Math.Min(point.X - val / 2, workingArea.Right - val));
		int y = Math.Max(workingArea.Top, Math.Min(point.Y - val / 2, workingArea.Bottom - val));
		((Control)this).Bounds = new Rectangle(x, y, val, val);
		((Control)this).Invalidate();
	}

	protected override void OnResizeEnd(EventArgs e)
	{
		((Form)this).OnResizeEnd(e);
		Screen val = Screen.FromControl((Control)(object)this);
		if (fittedScreen == null || val.DeviceName != fittedScreen.DeviceName)
		{
			FitToScreen(val, new Point(((Control)this).Left + ((Control)this).Width / 2, ((Control)this).Top + ((Control)this).Height / 2));
		}
	}

	private static Image LoadArt()
	{
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected Obj, but got Unknown
		Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WonderLauncher.bg.png");
		if (manifestResourceStream != null)
		{
			return Image.FromStream(manifestResourceStream);
		}
		string text = Path.Combine(Application.StartupPath, "bg_launcher.png");
		if (File.Exists(text))
		{
			return Image.FromFile(text);
		}
		Bitmap val = new Bitmap(1024, 1024);
		Graphics val2 = Graphics.FromImage((Image)(object)val);
		try
		{
			val2.Clear(Color.FromArgb(30, 36, 40));
			return (Image)(object)val;
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private static string FindGameDir()
	{
		string startupPath = Application.StartupPath;
		if (File.Exists(Path.Combine(startupPath, "Load.exe")))
		{
			return startupPath;
		}
		DirectoryInfo parent = Directory.GetParent(startupPath);
		if (parent == null)
		{
			return startupPath;
		}
		return parent.FullName;
	}

	private void StartVerification()
	{
		BackgroundWorker backgroundWorker = new BackgroundWorker();
		backgroundWorker.DoWork += (object s, DoWorkEventArgs e) =>
		{
			e.Result = Verify();
		};
		backgroundWorker.RunWorkerCompleted += (object s, RunWorkerCompletedEventArgs e) =>
		{
			string text = ((e.Error != null) ? e.Error.Message : ((string)e.Result));
			if (text == "\u0001reiniciar")
			{
				try
				{
					Atualizador.Reiniciar();
					((Form)this).Close();
					return;
				}
				catch (Exception ex)
				{
					text = "NAO FOI POSSIVEL REABRIR O LAUNCHER (" + ex.Message + ")";
				}
			}
			if (text == null)
			{
				realProgress = 1f;
			}
			else
			{
				errorText = text;
				state = CheckState.Error;
			}
		};
		backgroundWorker.RunWorkerAsync();
	}

	private bool Atualizar()
	{
		Atualizador.LimparSobras();
		if (launcherIni.Read("Launcher", "Atualizar", "1") == "0")
		{
			return false;
		}
		string url = launcherIni.Read("Launcher", "UrlAtualizacao", "https://github.com/guisq1515/wonder-launcher/releases/latest/download/atualizacao.txt").Trim();
		Atualizador atualizador = new Atualizador(gameDir, url, (string t, float p) =>
		{
			statusText = t;
		});
		try
		{
			return atualizador.Executar(!reiniciado);
		}
		catch (Exception)
		{
			return false;
		}
	}

	private string Verify()
	{
		if (Atualizar())
		{
			return "\u0001reiniciar";
		}
		statusText = "INSTALANDO MOD HD";
		RemoveOldPatch();
		string text = DeployDll();
		if (text != null)
		{
			return text;
		}
		EnsurePatchIni();
		string[] required = Required;
		foreach (string text2 in required)
		{
			string text3 = Path.Combine(gameDir, text2);
			if (!File.Exists(text3) || new FileInfo(text3).Length == 0L)
			{
				return "FALTA O ARQUIVO " + text2 + " (CLIENTE INCOMPLETO)";
			}
		}
		List<Alvo> list = CarregarManifesto(out var infoVer);
		if (list == null)
		{
			return null;
		}
		if (infoVer != null)
		{
			string text4 = new IniFile(Path.Combine(gameDir, "info.ini")).Read("info", "ver", "");
			if (text4.Trim() != infoVer)
			{
				return "VERSAO DO CLIENTE INCORRETA (info.ini ver=" + ((text4 == "") ? "?" : text4) + ", esperado " + infoVer + ")";
			}
		}
		int count = list.Count;
		int num = 0;
		using (SHA256 sHA = SHA256.Create())
		{
			foreach (Alvo item in list)
			{
				string path = Path.Combine(gameDir, item.Rel.Replace('/', Path.DirectorySeparatorChar));
				if (!File.Exists(path))
				{
					return (item.Critico ? "FALTA O ARQUIVO " : "FALTA O DADO ") + item.Rel;
				}
				string a;
				try
				{
					using FileStream inputStream = File.OpenRead(path);
					a = Hex(sHA.ComputeHash(inputStream));
				}
				catch (IOException)
				{
					return "ARQUIVO EM USO: " + item.Rel;
				}
				catch (UnauthorizedAccessException)
				{
					return "SEM PERMISSAO: " + item.Rel;
				}
				if (!string.Equals(a, item.Hash, StringComparison.OrdinalIgnoreCase))
				{
					return (item.Critico ? "VERSAO ERRADA: " : "DADO ADULTERADO: ") + item.Rel;
				}
				statusText = (item.Critico ? "VERIFICANDO VERSAO DO CLIENTE" : "VERIFICANDO DADOS DO SERVIDOR");
				realProgress = (float)(++num) / (float)count;
			}
		}
		realProgress = 1f;
		return null;
	}

	private static List<Alvo> CarregarManifesto(out string infoVer)
	{
		infoVer = null;
		Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("WonderLauncher.manifesto.txt");
		if (manifestResourceStream == null)
		{
			return null;
		}
		List<Alvo> list = new List<Alvo>();
		using StreamReader streamReader = new StreamReader(manifestResourceStream);
		string text;
		while ((text = streamReader.ReadLine()) != null)
		{
			if (text.Length != 0 && text[0] != '#')
			{
				string[] array = text.Split(new char[1] { '|' });
				if (array[0] == "INFOVER" && array.Length >= 2)
				{
					infoVer = array[1].Trim();
				}
				else if (array.Length >= 3 && (array[0] == "C" || array[0] == "V"))
				{
					list.Add(new Alvo
					{
						Critico = (array[0] == "C"),
						Rel = array[1],
						Hash = array[2]
					});
				}
			}
		}
		return list;
	}

	private static string Hex(byte[] b)
	{
		char[] array = new char[b.Length * 2];
		for (int i = 0; i < b.Length; i++)
		{
			array[i * 2] = HexD[b[i] >> 4];
			array[i * 2 + 1] = HexD[b[i] & 0xF];
		}
		return new string(array);
	}

	private string DeployDll()
	{
		string text = DeployResourceDll("WonderLauncher.wkhdmod.dll", "wkhdmod.dll");
		if (text != null)
		{
			return text;
		}
		string text2 = DeployResourceDll("WonderLauncher.dinput8.dll", "dinput8.dll");
		if (text2 != null)
		{
			return text2;
		}
		EnsureSyncIni();
		return null;
	}

	private string DeployResourceDll(string recurso, string nomeArquivo)
	{
		byte[] array;
		using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(recurso))
		{
			if (stream == null)
			{
				return null;
			}
			array = new byte[stream.Length];
			int num = 0;
			int num2;
			while ((num2 = stream.Read(array, num, array.Length - num)) > 0)
			{
				num += num2;
			}
		}
		string path = Path.Combine(gameDir, nomeArquivo);
		try
		{
			if (File.Exists(path) && BytesIguais(File.ReadAllBytes(path), array))
			{
				return null;
			}
			File.WriteAllBytes(path, array);
			return null;
		}
		catch (IOException)
		{
			return File.Exists(path) ? null : ("NAO FOI POSSIVEL INSTALAR " + nomeArquivo + " (FECHE O JOGO)");
		}
		catch (UnauthorizedAccessException)
		{
			return "SEM PERMISSAO PARA INSTALAR " + nomeArquivo + " (RODE COMO ADMINISTRADOR)";
		}
	}

	private static bool BytesIguais(byte[] a, byte[] b)
	{
		if (a.Length != b.Length)
		{
			return false;
		}
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i] != b[i])
			{
				return false;
			}
		}
		return true;
	}

	private void EnsurePatchIni()
	{
		string path = Path.Combine(gameDir, "tela_patch.ini");
		if (!File.Exists(path))
		{
			File.WriteAllText(path, "; Configuracao do patch de tela (wkhdmod.dll)\r\n[Tela]\r\nModo=2\r\nLargura=0\r\nAltura=0\r\nManterProporcao=1\r\nEscalaInteira=0\r\nMonitor=0\r\nAltEnter=1\r\nDpiAware=1\r\nFiltro=2\r\nEscala=0\r\nFOV=0\r\nLog=0\r\n");
		}
	}

	private void EnsureSyncIni()
	{
		string path = Path.Combine(gameDir, "sync_patch.ini");
		if (!File.Exists(path))
		{
			File.WriteAllText(path, "; Configuracao do patch de sincronizacao (dinput8.dll)\r\n[Sync]\r\nHeartbeat=1\r\nHeartbeatFrames=15\r\nSmoothCorrection=1\r\nSmoothPercent=20\r\nDeadZone=2\r\nSnapDistance=160\r\nLog=0\r\nStatsSeconds=60\r\nIdleCorrection=1\r\nIdleCorrectionFrames=15\r\nIdleCorrectionDistance=3\r\nLadderHeartbeat=1\r\nYCorrection=1\r\nYCorrectionDistance=8\r\nPvpDelayMargin=25\r\nPvpRedundancy=1\r\nPvpCopies=3\r\nPvpCopyMaxAge=400\r\nPvpResends=2\r\nPvpResendMs=35\r\nPvpStateFrames=0\r\n");
		}
	}

	private void RemoveOldPatch()
	{
		bool permitido = LoadImportaWkhdmod();
		ApagaPatchAntigo("version.dll", permitido: true);
		ApagaPatchAntigo("hd.dll", permitido);
	}

	private void ApagaPatchAntigo(string nome, bool permitido)
	{
		if (!permitido)
		{
			return;
		}
		string text = Path.Combine(gameDir, nome);
		if (!File.Exists(text))
		{
			return;
		}
		try
		{
			byte[] bytes = File.ReadAllBytes(text);
			if (Encoding.ASCII.GetString(bytes).IndexOf("tela_patch", StringComparison.Ordinal) < 0)
			{
				return;
			}
		}
		catch (IOException)
		{
			return;
		}
		try
		{
			File.Delete(text);
		}
		catch (Exception)
		{
			try
			{
				File.Move(text, text + ".antiga");
			}
			catch
			{
			}
		}
	}

	private bool LoadImportaWkhdmod()
	{
		try
		{
			return IndexOfIgnoreCase(File.ReadAllBytes(Path.Combine(gameDir, "Load.exe")), WKHD) >= 0;
		}
		catch
		{
			return false;
		}
	}

	private static int IndexOfIgnoreCase(byte[] hay, byte[] needle)
	{
		for (int i = 0; i + needle.Length <= hay.Length; i++)
		{
			int j;
			for (j = 0; j < needle.Length; j++)
			{
				byte b = hay[i + j];
				byte b2 = needle[j];
				if (b >= 97 && b <= 122)
				{
					b -= 32;
				}
				if (b2 >= 97 && b2 <= 122)
				{
					b2 -= 32;
				}
				if (b != b2)
				{
					break;
				}
			}
			if (j == needle.Length)
			{
				return i;
			}
		}
		return -1;
	}

	private void OnAnimTick(object sender, EventArgs e)
	{
		foreach (HotSpot spot in spots)
		{
			float num = ((spot.Over && IsActive(spot)) ? 1f : 0f);
			spot.Hover += (num - spot.Hover) * 0.22f;
			if (Math.Abs(num - spot.Hover) < 0.01f)
			{
				spot.Hover = num;
			}
		}
		float num2 = realProgress;
		float num3 = Math.Max(0.004f, (num2 - shownProgress) * 0.12f);
		shownProgress = Math.Min(num2, shownProgress + num3);
		if (state == CheckState.Checking && shownProgress >= 0.999f && realProgress >= 1f)
		{
			state = CheckState.Ready;
			readyFlash = 1f;
		}
		if (readyFlash > 0f)
		{
			readyFlash = Math.Max(0f, readyFlash - 0.02f);
		}
		((Control)this).Invalidate();
	}

	private bool IsActive(HotSpot h)
	{
		if (h == btnPlay)
		{
			return state == CheckState.Ready;
		}
		return true;
	}

	protected override void OnPaint(PaintEventArgs e)
	{
		Graphics graphics = e.Graphics;
		graphics.DrawImageUnscaled((Image)(object)bgScaled, 0, 0);
		graphics.SmoothingMode = (SmoothingMode)4;
		graphics.PixelOffsetMode = (PixelOffsetMode)2;
		graphics.TextRenderingHint = (TextRenderingHint)3;
		graphics.ScaleTransform(S, S);
		DrawBar(graphics);
		DrawPlay(graphics);
		DrawWindowButton(graphics, btnSettings, isClose: false);
		DrawWindowButton(graphics, btnMin, isClose: false);
		DrawWindowButton(graphics, btnClose, isClose: true);
		DrawStatus(graphics);
		DrawCadastro(graphics);
		DrawShadowText(graphics, "v" + Atualizador.VersaoAtual.ToString(2), new PointF(990f, 1010f), 12f, Color.FromArgb(150, 160, 170));
	}

	private static void DrawPanelText(Graphics g, string text, float x, float y, float size, Color color, StringAlignment align, bool bold)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected Obj, but got Unknown
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Expected Obj, but got Unknown
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Expected Obj, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Expected Obj, but got Unknown
		Font val = new Font("Segoe UI", size, (FontStyle)(bold ? 1 : 0), (GraphicsUnit)2);
		try
		{
			StringFormat val2 = new StringFormat
			{
				Alignment = align,
				LineAlignment = (StringAlignment)1
			};
			try
			{
				SolidBrush val3 = new SolidBrush(Color.FromArgb(150, 0, 0, 0));
				try
				{
					g.DrawString(text, val, (Brush)(object)val3, new PointF(x + 1f, y + 1.2f), val2);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
				SolidBrush val4 = new SolidBrush(color);
				try
				{
					g.DrawString(text, val, (Brush)(object)val4, new PointF(x, y), val2);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private void DrawStatus(Graphics g)
	{
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Expected Obj, but got Unknown
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Expected Obj, but got Unknown
		StatusServidor statusServidor = servidor;
		EstadoServidor estadoServidor = statusServidor?.Estado ?? EstadoServidor.Verificando;
		Color color = Color.FromArgb(96, 220, 120);
		Color color2 = Color.FromArgb(240, 100, 90);
		Color color3 = Color.FromArgb(175, 185, 195);
		Color color4 = Color.FromArgb(225, 232, 240);
		Color color5 = estadoServidor switch
		{
			EstadoServidor.Offline => color2, 
			EstadoServidor.Online => color, 
			_ => color3, 
		};
		string text = estadoServidor switch
		{
			EstadoServidor.Offline => "OFFLINE", 
			EstadoServidor.Online => "ONLINE", 
			_ => "VERIFICANDO...", 
		};
		DrawPanelText(g, "Servidor", 877f, 279f, 22f, color4, (StringAlignment)1, bold: false);
		float num = 0.5f + 0.5f * (float)Math.Sin(clock.Elapsed.TotalSeconds * 3.0);
		float num2 = 792f;
		float num3 = 338f;
		SolidBrush val = new SolidBrush(Color.FromArgb((int)(60f + 70f * num), color5));
		try
		{
			float num4 = 13f + 4f * num;
			g.FillEllipse((Brush)(object)val, num2 - num4, num3 - num4, num4 * 2f, num4 * 2f);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		SolidBrush val2 = new SolidBrush(color5);
		try
		{
			g.FillEllipse((Brush)(object)val2, num2 - 8f, num3 - 8f, 16f, 16f);
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		DrawPanelText(g, text, 814f, 338f, 26f, color5, (StringAlignment)0, bold: true);
		DrawPanelText(g, estadoServidor switch
		{
			EstadoServidor.Offline => "Em manutenção", 
			EstadoServidor.Online => "Pronto para jogar", 
			_ => "Conectando...", 
		}, 814f, 370f, 16f, color3, (StringAlignment)0, bold: false);
		bool flag = estadoServidor == EstadoServidor.Online;
		string text2 = ((!flag || statusServidor.Jogadores < 0) ? "—" : statusServidor.Jogadores.ToString());
		string text3 = ((!flag) ? "—" : (statusServidor.PingMs + " ms"));
		Color color6;
		if (flag)
		{
			if (statusServidor.PingMs <= 80)
			{
				color6 = color;
			}
			else
			{
				color6 = ((statusServidor.PingMs <= 160) ? Color.FromArgb(240, 200, 90) : color2);
			}
		}
		else
		{
			color6 = color3;
		}
		string text4;
		if (flag)
		{
			text4 = (statusServidor.CadastroAberto ? "Aberto" : "Fechado");
		}
		else
		{
			text4 = "—";
		}
		DrawPanelText(g, "Jogadores online", 784f, 430f, 18f, color4, (StringAlignment)0, bold: false);
		DrawPanelText(g, text2, 970f, 430f, 18f, color4, (StringAlignment)2, bold: true);
		DrawPanelText(g, "Ping", 784f, 465f, 18f, color4, (StringAlignment)0, bold: false);
		DrawPanelText(g, text3, 970f, 465f, 18f, color6, (StringAlignment)2, bold: true);
		DrawPanelText(g, "Cadastro", 784f, 500f, 18f, color4, (StringAlignment)0, bold: false);
		DrawPanelText(g, text4, 970f, 500f, 18f, (flag && statusServidor.CadastroAberto) ? color : color3, (StringAlignment)2, bold: true);
		DrawPanelText(g, "[Status do servidor:", 52f, 588f, 19f, color4, (StringAlignment)0, bold: false);
		DrawPanelText(g, text + "]", 52f, 613f, 19f, color5, (StringAlignment)0, bold: false);
		if (flag && statusServidor.Jogadores >= 0)
		{
			DrawPanelText(g, "(" + statusServidor.Jogadores + " jogador" + ((statusServidor.Jogadores == 1) ? "" : "es") + " online agora)", 52f, 640f, 15f, color3, (StringAlignment)0, bold: false);
		}
	}

	private void DrawCadastro(Graphics g)
	{
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Expected Obj, but got Unknown
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b7: Expected Obj, but got Unknown
		HotSpot hotSpot = btnCadastro;
		RectangleF r = RectangleF.Inflate(hotSpot.Rect, -2f, -2.5f);
		if (hotSpot.Hover > 0.001f || hotSpot.Down)
		{
			GraphicsPath val = RoundRect(r, 5f);
			try
			{
				SolidBrush val2 = new SolidBrush(Color.FromArgb((int)((float)(hotSpot.Down ? 15 : 40) * hotSpot.Hover), 190, 225, 255));
				try
				{
					g.FillPath((Brush)(object)val2, val);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				Pen val3 = new Pen(Color.FromArgb((int)(140f * hotSpot.Hover), 200, 230, 255), 1.2f);
				try
				{
					g.DrawPath(val3, val);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		Color color = Color.FromArgb(225 + (int)(30f * hotSpot.Hover), 232 + (int)(23f * hotSpot.Hover), 240 + (int)(15f * hotSpot.Hover));
		DrawShadowText(g, "Criar Conta", new PointF(r.X + r.Width / 2f, r.Y + r.Height / 2f), 17f, color);
	}

	private static GraphicsPath RoundRect(RectangleF r, float rad)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Expected Obj, but got Unknown
		GraphicsPath val = new GraphicsPath();
		float num = rad * 2f;
		val.AddArc(r.X, r.Y, num, num, 180f, 90f);
		val.AddArc(r.Right - num, r.Y, num, num, 270f, 90f);
		val.AddArc(r.Right - num, r.Bottom - num, num, num, 0f, 90f);
		val.AddArc(r.X, r.Bottom - num, num, num, 90f, 90f);
		val.CloseFigure();
		return val;
	}

	private static GraphicsPath BarChannel()
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Expected Obj, but got Unknown
		GraphicsPath val = new GraphicsPath();
		val.AddPolygon(new PointF[6]
		{
			new PointF(46f, 942f),
			new PointF(58f, 929f),
			new PointF(693f, 929f),
			new PointF(705f, 942f),
			new PointF(693f, 955f),
			new PointF(58f, 955f)
		});
		return val;
	}

	private void DrawBar(Graphics g)
	{
		//IL_01c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c7: Expected Obj, but got Unknown
		//IL_01c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Expected Obj, but got Unknown
		//IL_025c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Expected Obj, but got Unknown
		//IL_02e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ed: Expected Obj, but got Unknown
		//IL_03c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03cd: Expected Obj, but got Unknown
		//IL_02ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f5: Expected Obj, but got Unknown
		//IL_0429: Unknown result type (might be due to invalid IL or missing references)
		//IL_0430: Expected Obj, but got Unknown
		float num = (float)clock.Elapsed.TotalSeconds;
		float num2 = ((state == CheckState.Error) ? Math.Max(shownProgress, 1f) : shownProgress);
		float num3 = 46f + 659f * num2;
		Color color;
		Color color2;
		Color color3;
		Color color4;
		Color color5;
		if (state == CheckState.Error)
		{
			color = Color.FromArgb(230, 120, 110);
			color2 = Color.FromArgb(190, 45, 40);
			color3 = Color.FromArgb(135, 20, 22);
			color4 = Color.FromArgb(90, 12, 14);
			color5 = Color.FromArgb(255, 170, 160);
		}
		else if (state == CheckState.Ready)
		{
			color = Color.FromArgb(150, 235, 150);
			color2 = Color.FromArgb(55, 175, 75);
			color3 = Color.FromArgb(25, 120, 50);
			color4 = Color.FromArgb(14, 80, 34);
			color5 = Color.FromArgb(200, 255, 200);
		}
		else
		{
			color = Color.FromArgb(90, 160, 220);
			color2 = Color.FromArgb(25, 92, 188);
			color3 = Color.FromArgb(12, 50, 138);
			color4 = Color.FromArgb(8, 32, 110);
			color5 = Color.FromArgb(150, 200, 255);
		}
		GraphicsState val = g.Save();
		GraphicsPath val2 = BarChannel();
		try
		{
			g.SetClip(val2);
			if (num3 > 46.5f)
			{
				RectangleF rectangleF = new RectangleF(44f, 929f, num3 - 46f + 2f, 26f);
				LinearGradientBrush val3 = new LinearGradientBrush(new PointF(0f, 929f), new PointF(0f, 955.01f), color, color4);
				try
				{
					ColorBlend val4 = new ColorBlend(5);
					val4.Colors = new Color[5] { color, color2, color2, color3, color4 };
					val4.Positions = new float[5] { 0f, 0.12f, 0.48f, 0.52f, 1f };
					val3.InterpolationColors = val4;
					g.FillRectangle((Brush)(object)val3, rectangleF);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
				Pen val5 = new Pen(Color.FromArgb(110, 255, 255, 255), 1f);
				try
				{
					g.DrawLine(val5, 46f, 930.5f, num3, 930.5f);
				}
				finally
				{
					((IDisposable)val5)?.Dispose();
				}
				if (state == CheckState.Checking)
				{
					float num4 = 90f;
					float num5 = 659f + num4 * 2f;
					float x = 46f - num4 + num * 260f % num5;
					RectangleF rectangleF2 = new RectangleF(x, 929f, num4, 26f);
					LinearGradientBrush val6 = new LinearGradientBrush(rectangleF2, Color.Transparent, Color.Transparent, 0f);
					try
					{
						ColorBlend val7 = new ColorBlend(3);
						val7.Colors = new Color[3]
						{
							Color.FromArgb(0, 255, 255, 255),
							Color.FromArgb(70, 255, 255, 255),
							Color.FromArgb(0, 255, 255, 255)
						};
						val7.Positions = new float[3] { 0f, 0.5f, 1f };
						val6.InterpolationColors = val7;
						g.SetClip(new RectangleF(46f, 929f, num3 - 46f, 26f), (CombineMode)1);
						g.FillRectangle((Brush)(object)val6, rectangleF2);
					}
					finally
					{
						((IDisposable)val6)?.Dispose();
					}
				}
				if (num2 < 0.999f)
				{
					Pen val8 = new Pen(color5, 1.5f);
					try
					{
						g.DrawLine(val8, num3 - 0.75f, 929f, num3 - 0.75f, 955f);
					}
					finally
					{
						((IDisposable)val8)?.Dispose();
					}
				}
				if (readyFlash > 0f)
				{
					SolidBrush val9 = new SolidBrush(Color.FromArgb((int)(120f * readyFlash), 255, 255, 255));
					try
					{
						g.FillRectangle((Brush)(object)val9, rectangleF);
					}
					finally
					{
						((IDisposable)val9)?.Dispose();
					}
				}
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
		g.Restore(val);
		Color color6 = Color.FromArgb(206, 212, 220);
		string text;
		if (state == CheckState.Error)
		{
			text = "ERRO: " + errorText;
			color6 = Color.FromArgb(255, 150, 140);
		}
		else if (state == CheckState.Ready)
		{
			text = "PRONTO PARA JOGAR";
			color6 = Color.FromArgb(170, 240, 170);
		}
		else
		{
			text = $"{statusText} ({(int)Math.Round(shownProgress * 100f)}%)";
		}
		DrawShadowText(g, text, BarTextPos, 19f, color6);
	}

	private static void DrawShadowText(Graphics g, string text, PointF center, float size, Color color)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Expected Obj, but got Unknown
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Expected Obj, but got Unknown
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Expected Obj, but got Unknown
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Expected Obj, but got Unknown
		Font val = new Font(PickSerif(), size, (FontStyle)1, (GraphicsUnit)2);
		try
		{
			StringFormat val2 = new StringFormat
			{
				Alignment = (StringAlignment)1,
				LineAlignment = (StringAlignment)1
			};
			try
			{
				SolidBrush val3 = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
				try
				{
					g.DrawString(text, val, (Brush)(object)val3, new PointF(center.X + 1f, center.Y + 1.5f), val2);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
				SolidBrush val4 = new SolidBrush(color);
				try
				{
					g.DrawString(text, val, (Brush)(object)val4, center, val2);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val2)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static string PickSerif()
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected Obj, but got Unknown
		if (serif != null)
		{
			return serif;
		}
		InstalledFontCollection val = new InstalledFontCollection();
		try
		{
			string[] array = new string[3] { "Palatino Linotype", "Georgia", "Times New Roman" };
			foreach (string text in array)
			{
				FontFamily[] families = ((FontCollection)val).Families;
				for (int j = 0; j < families.Length; j++)
				{
					if (families[j].Name == text)
					{
						return serif = text;
					}
				}
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
		return serif = FontFamily.GenericSerif.Name;
	}

	private void DrawPlay(Graphics g)
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected Obj, but got Unknown
		//IL_010b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Expected Obj, but got Unknown
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Expected Obj, but got Unknown
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Expected Obj, but got Unknown
		//IL_02de: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Expected Obj, but got Unknown
		//IL_031b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0322: Expected Obj, but got Unknown
		//IL_036b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0372: Expected Obj, but got Unknown
		HotSpot hotSpot = btnPlay;
		float hover = hotSpot.Hover;
		if (state != CheckState.Ready)
		{
			GraphicsPath val = RoundRect(PlayInner, 3f);
			try
			{
				SolidBrush val2 = new SolidBrush(Color.FromArgb(120, 10, 14, 20));
				try
				{
					g.FillPath((Brush)(object)val2, val);
					return;
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		if (hover <= 0.001f && readyFlash <= 0f)
		{
			return;
		}
		for (int i = 1; i <= 6; i++)
		{
			GraphicsPath val3 = RoundRect(RectangleF.Inflate(new RectangleF(hotSpot.Rect.X + 4f, hotSpot.Rect.Y + 1f, hotSpot.Rect.Width - 8f, hotSpot.Rect.Height - 2f), (float)i * 1.6f, (float)i * 1.6f), 6 + i);
			try
			{
				Pen val4 = new Pen(Color.FromArgb((int)((float)(70 - i * 10) * hover), 255, 210, 120), 2f);
				try
				{
					g.DrawPath(val4, val3);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		GraphicsState val5 = g.Save();
		GraphicsPath val6 = RoundRect(PlayInner, 3f);
		try
		{
			g.SetClip(val6);
			float num = (hotSpot.Down ? 0.4f : 1f);
			LinearGradientBrush val7 = new LinearGradientBrush(PlayInner, Color.FromArgb((int)(75f * hover * num), 190, 230, 255), Color.FromArgb((int)(15f * hover * num), 120, 190, 255), 90f);
			try
			{
				g.FillRectangle((Brush)(object)val7, PlayInner);
			}
			finally
			{
				((IDisposable)val7)?.Dispose();
			}
			float num2 = (float)clock.Elapsed.TotalSeconds;
			RectangleF playInner = PlayInner;
			float num3 = playInner.X - 80f;
			float num4 = num2 * 220f;
			playInner = PlayInner;
			float num5 = num3 + num4 % (playInner.Width + 240f);
			GraphicsPath val8 = new GraphicsPath();
			try
			{
				PointF[] array = new PointF[4];
				playInner = PlayInner;
				array[0] = new PointF(num5, playInner.Bottom);
				float x = num5 + 40f;
				playInner = PlayInner;
				array[1] = new PointF(x, playInner.Top);
				float x2 = num5 + 75f;
				playInner = PlayInner;
				array[2] = new PointF(x2, playInner.Top);
				float x3 = num5 + 35f;
				playInner = PlayInner;
				array[3] = new PointF(x3, playInner.Bottom);
				val8.AddPolygon(array);
				SolidBrush val9 = new SolidBrush(Color.FromArgb((int)(40f * hover), 255, 255, 255));
				try
				{
					g.FillPath((Brush)(object)val9, val8);
				}
				finally
				{
					((IDisposable)val9)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val8)?.Dispose();
			}
			if (hotSpot.Down)
			{
				SolidBrush val10 = new SolidBrush(Color.FromArgb(70, 0, 0, 0));
				try
				{
					g.FillRectangle((Brush)(object)val10, PlayInner);
				}
				finally
				{
					((IDisposable)val10)?.Dispose();
				}
			}
			if (readyFlash > 0f)
			{
				SolidBrush val11 = new SolidBrush(Color.FromArgb((int)(90f * readyFlash), 255, 255, 255));
				try
				{
					g.FillRectangle((Brush)(object)val11, PlayInner);
				}
				finally
				{
					((IDisposable)val11)?.Dispose();
				}
			}
		}
		finally
		{
			((IDisposable)val6)?.Dispose();
		}
		g.Restore(val5);
	}

	private void DrawWindowButton(Graphics g, HotSpot b, bool isClose)
	{
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0161: Expected Obj, but got Unknown
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Expected Obj, but got Unknown
		//IL_0199: Unknown result type (might be due to invalid IL or missing references)
		//IL_019f: Expected Obj, but got Unknown
		//IL_00d9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Expected Obj, but got Unknown
		float hover = b.Hover;
		if (hover <= 0.001f && !b.Down)
		{
			return;
		}
		RectangleF rect = b.Rect;
		GraphicsPath val = RoundRect(RectangleF.Inflate(rect, -1.5f, -1.5f), 5f);
		try
		{
			if (isClose)
			{
				SolidBrush val2 = new SolidBrush(Color.FromArgb((int)(235f * hover), b.Down ? 150 : 200, 40, 34));
				try
				{
					g.FillPath((Brush)(object)val2, val);
				}
				finally
				{
					((IDisposable)val2)?.Dispose();
				}
				float num = rect.X + rect.Width / 2f;
				float num2 = rect.Y + rect.Height / 2f;
				float num3 = 8.5f;
				Pen val3 = new Pen(Color.FromArgb((int)(255f * hover), 255, 255, 255), 2.6f)
				{
					StartCap = (LineCap)2,
					EndCap = (LineCap)2
				};
				try
				{
					g.DrawLine(val3, num - num3, num2 - num3, num + num3, num2 + num3);
					g.DrawLine(val3, num + num3, num2 - num3, num - num3, num2 + num3);
				}
				finally
				{
					((IDisposable)val3)?.Dispose();
				}
			}
			else
			{
				SolidBrush val4 = new SolidBrush(Color.FromArgb((int)((float)(b.Down ? 20 : 45) * hover), 255, 255, 255));
				try
				{
					g.FillPath((Brush)(object)val4, val);
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			Pen val5 = new Pen(Color.FromArgb((int)(120f * hover), 255, 255, 255), 1.2f);
			try
			{
				g.DrawPath(val5, val);
			}
			finally
			{
				((IDisposable)val5)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private PointF ToArt(Point p)
	{
		return new PointF((float)p.X / S, (float)p.Y / S);
	}

	private HotSpot HitTest(Point p)
	{
		PointF pt = ToArt(p);
		foreach (HotSpot spot in spots)
		{
			if (spot.Rect.Contains(pt))
			{
				return spot;
			}
		}
		return null;
	}

	protected override void OnMouseMove(MouseEventArgs e)
	{
		((Control)this).OnMouseMove(e);
		HotSpot hotSpot = HitTest(e.Location);
		foreach (HotSpot spot in spots)
		{
			spot.Over = spot == hotSpot;
		}
		((Control)this).Cursor = ((hotSpot != null && IsActive(hotSpot)) ? Cursors.Hand : Cursors.Default);
	}

	protected override void OnMouseLeave(EventArgs e)
	{
		((Control)this).OnMouseLeave(e);
		foreach (HotSpot spot in spots)
		{
			spot.Over = false;
			spot.Down = false;
		}
	}

	protected override void OnMouseDown(MouseEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_0012: Invalid comparison between Unknown and I4
		((Control)this).OnMouseDown(e);
		if ((int)e.Button != 1048576)
		{
			return;
		}
		HotSpot hotSpot = HitTest(e.Location);
		if (hotSpot != null)
		{
			if (IsActive(hotSpot))
			{
				pressed = hotSpot;
				hotSpot.Down = true;
			}
		}
		else
		{
			ReleaseCapture();
			SendMessage(((Control)this).Handle, 161, 2, 0);
		}
	}

	protected override void OnMouseUp(MouseEventArgs e)
	{
		((Control)this).OnMouseUp(e);
		HotSpot hotSpot = pressed;
		pressed = null;
		if (hotSpot == null)
		{
			return;
		}
		hotSpot.Down = false;
		if (HitTest(e.Location) == hotSpot)
		{
			if (hotSpot == btnClose)
			{
				((Form)this).Close();
			}
			else if (hotSpot == btnMin)
			{
				((Form)this).WindowState = (FormWindowState)1;
			}
			else if (hotSpot == btnSettings)
			{
				ShowSettings();
			}
			else if (hotSpot == btnPlay)
			{
				LaunchGame();
			}
			else if (hotSpot == btnCadastro)
			{
				ShowCadastro();
			}
		}
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Invalid comparison between Unknown and I4
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Invalid comparison between Unknown and I4
		((Control)this).OnKeyDown(e);
		if ((int)e.KeyCode == 27)
		{
			((Form)this).Close();
		}
		else if ((int)e.KeyCode == 13 && state == CheckState.Ready)
		{
			LaunchGame();
		}
	}

	private void LaunchGame()
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		string text = Path.Combine(gameDir, "Load.exe");
		if (!File.Exists(text))
		{
			MessageBox.Show((IWin32Window)(object)this, "Load.exe não encontrado na pasta do jogo:\n" + gameDir, "Wonder Classic", (MessageBoxButtons)0, (MessageBoxIcon)16);
			return;
		}
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = text,
				WorkingDirectory = gameDir,
				UseShellExecute = true
			});
			((Form)this).Close();
		}
		catch (Exception ex)
		{
			MessageBox.Show((IWin32Window)(object)this, "Não foi possível abrir o jogo:\n" + ex.Message, "Wonder Classic", (MessageBoxButtons)0, (MessageBoxIcon)16);
		}
	}

	private void ShowCadastro()
	{
		Cadastro.Mostrar((IWin32Window)(object)this, HostServidor(), PortaLauncher());
		servidor?.ConsultarAgora();
	}

	private void ShowSettings()
	{
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_005f: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ca: Expected Obj, but got Unknown
		//IL_00cf: Expected Obj, but got Unknown
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0104: Expected Obj, but got Unknown
		//IL_0148: Unknown result type (might be due to invalid IL or missing references)
		//IL_014d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0158: Unknown result type (might be due to invalid IL or missing references)
		//IL_0167: Unknown result type (might be due to invalid IL or missing references)
		//IL_0170: Expected Obj, but got Unknown
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cf: Expected Obj, but got Unknown
		//IL_0216: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0226: Unknown result type (might be due to invalid IL or missing references)
		//IL_0238: Unknown result type (might be due to invalid IL or missing references)
		//IL_0241: Expected Obj, but got Unknown
		//IL_0280: Unknown result type (might be due to invalid IL or missing references)
		//IL_0285: Unknown result type (might be due to invalid IL or missing references)
		//IL_0290: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ab: Expected Obj, but got Unknown
		//IL_031a: Unknown result type (might be due to invalid IL or missing references)
		//IL_031f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0331: Unknown result type (might be due to invalid IL or missing references)
		//IL_0343: Unknown result type (might be due to invalid IL or missing references)
		//IL_034a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0355: Unknown result type (might be due to invalid IL or missing references)
		//IL_035f: Expected Obj, but got Unknown
		//IL_0364: Expected Obj, but got Unknown
		//IL_0365: Unknown result type (might be due to invalid IL or missing references)
		//IL_036a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0375: Unknown result type (might be due to invalid IL or missing references)
		//IL_0387: Unknown result type (might be due to invalid IL or missing references)
		//IL_0393: Expected Obj, but got Unknown
		//IL_0394: Unknown result type (might be due to invalid IL or missing references)
		//IL_0399: Unknown result type (might be due to invalid IL or missing references)
		//IL_03a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c2: Expected Obj, but got Unknown
		//IL_03c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_0411: Unknown result type (might be due to invalid IL or missing references)
		//IL_041e: Expected Obj, but got Unknown
		//IL_041e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0423: Unknown result type (might be due to invalid IL or missing references)
		//IL_042e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0443: Unknown result type (might be due to invalid IL or missing references)
		//IL_0452: Unknown result type (might be due to invalid IL or missing references)
		//IL_0459: Unknown result type (might be due to invalid IL or missing references)
		//IL_0465: Unknown result type (might be due to invalid IL or missing references)
		//IL_0471: Unknown result type (might be due to invalid IL or missing references)
		//IL_047a: Expected Obj, but got Unknown
		//IL_0743: Unknown result type (might be due to invalid IL or missing references)
		Color backColor = Color.FromArgb(28, 34, 42);
		Color panel = Color.FromArgb(40, 48, 58);
		Color fg = Color.FromArgb(225, 230, 236);
		Color foreColor = Color.FromArgb(150, 160, 172);
		Form f = new Form
		{
			Text = "Configurações de Tela",
			ClientSize = new Size(400, 494),
			StartPosition = (FormStartPosition)4,
			FormBorderStyle = (FormBorderStyle)3,
			MaximizeBox = false,
			MinimizeBox = false,
			ShowInTaskbar = false,
			BackColor = backColor,
			ForeColor = fg,
			Font = new Font("Segoe UI", 9.5f)
		};
		Func<int, ComboBox> func = (int y) =>
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0038: Unknown result type (might be due to invalid IL or missing references)
			//IL_0045: Expected Obj, but got Unknown
			return new ComboBox
			{
				Location = new Point(20, y),
				Width = 360,
				DropDownStyle = (ComboBoxStyle)2,
				FlatStyle = (FlatStyle)0,
				BackColor = panel,
				ForeColor = fg
			};
		};
		Label val = new Label
		{
			Text = "Modo de janela",
			Location = new Point(20, 16),
			AutoSize = true
		};
		ComboBox cmbModo = func(38);
		cmbModo.Items.AddRange(new object[4] { "Original do jogo (sem mod)", "Janela redimensionável", "Tela cheia sem borda (recomendado)", "Tela cheia exclusiva 1024x768 (sem filtros)" });
		Label val2 = new Label
		{
			Text = "Proporção",
			Location = new Point(20, 76),
			AutoSize = true
		};
		ComboBox cmbProp = func(98);
		cmbProp.Items.AddRange(new object[2] { "Manter proporção (faixas pretas se sobrar)", "Esticar para preencher a tela" });
		Label val3 = new Label
		{
			Text = "Campo de visão (widescreen nativo)",
			Location = new Point(20, 136),
			AutoSize = true
		};
		ComboBox cmbFov = func(158);
		cmbFov.Items.AddRange(new object[4] { "Original 4:3 (1024x768)", "16:10 — experimental", "16:9 nativo — experimental", "21:9 ultrawide — experimental" });
		Label val4 = new Label
		{
			Text = "Qualidade da imagem",
			Location = new Point(20, 196),
			AutoSize = true
		};
		ComboBox cmbFiltro = func(218);
		cmbFiltro.Items.AddRange(new object[3] { "Bilinear (mais leve, um pouco borrado)", "Nítido (pixels limpos, sem borrão)", "HD - xBR (bordas suaves, recomendado)" });
		Label val5 = new Label
		{
			Text = "Escala do HD",
			Location = new Point(20, 256),
			AutoSize = true
		};
		ComboBox cmbEscala = func(278);
		cmbEscala.Items.AddRange(new object[6] { "Automática (recomendado)", "2x (leve)", "3x (mais suave, pesa mais)", "4x (placa de vídeo dedicada)", "6x (placa forte)", "8x (topo de linha)" });
		int[] escalaVals = new int[6] { 0, 2, 3, 4, 6, 8 };
		Label lblDica = new Label
		{
			Location = new Point(20, 310),
			Size = new Size(360, 44),
			ForeColor = foreColor,
			Font = new Font("Segoe UI", 8.5f)
		};
		CheckBox chkInt = new CheckBox
		{
			Text = "Escala inteira (pixels nítidos, bom para 4K)",
			Location = new Point(20, 360),
			AutoSize = true
		};
		CheckBox chkAlt = new CheckBox
		{
			Text = "Alt+Enter alterna janela / tela cheia",
			Location = new Point(20, 386),
			AutoSize = true
		};
		Button val6 = new Button
		{
			Text = "Salvar",
			Location = new Point(190, 442),
			Size = new Size(92, 32),
			FlatStyle = (FlatStyle)0,
			BackColor = Color.FromArgb(38, 110, 190),
			ForeColor = Color.White
		};
		Button val7 = new Button
		{
			Text = "Cancelar",
			Location = new Point(288, 442),
			Size = new Size(92, 32),
			FlatStyle = (FlatStyle)0,
			BackColor = panel,
			ForeColor = fg,
			DialogResult = (DialogResult)2
		};
		((ButtonBase)val6).FlatAppearance.BorderColor = Color.FromArgb(80, 150, 225);
		((ButtonBase)val7).FlatAppearance.BorderColor = Color.FromArgb(70, 80, 92);
		if (!int.TryParse(ini.Read("Tela", "Modo", "2"), out var result) || result < 0 || result > 3)
		{
			result = 2;
		}
		if (!int.TryParse(ini.Read("Tela", "Filtro", "2"), out var result2) || result2 < 0 || result2 > 2)
		{
			result2 = 2;
		}
		if (!int.TryParse(ini.Read("Tela", "Escala", "0"), out var result3) || Array.IndexOf(escalaVals, result3) < 0)
		{
			result3 = 0;
		}
		if (!int.TryParse(ini.Read("Tela", "FOV", "0"), out var result4) || result4 < 0 || result4 > 3)
		{
			result4 = 0;
		}
		((ListControl)cmbModo).SelectedIndex = result;
		((ListControl)cmbProp).SelectedIndex = ((ini.Read("Tela", "ManterProporcao", "1") == "0") ? 1 : 0);
		((ListControl)cmbFov).SelectedIndex = result4;
		((ListControl)cmbFiltro).SelectedIndex = result2;
		((ListControl)cmbEscala).SelectedIndex = Math.Max(0, Array.IndexOf(escalaVals, result3));
		chkInt.Checked = ini.Read("Tela", "EscalaInteira", "0") == "1";
		chkAlt.Checked = ini.Read("Tela", "AltEnter", "1") == "1";
		EventHandler eventHandler = (object s, EventArgs e) =>
		{
			bool flag = ((ListControl)cmbModo).SelectedIndex == 1 || ((ListControl)cmbModo).SelectedIndex == 2;
			((Control)cmbFiltro).Enabled = flag;
			((Control)cmbEscala).Enabled = flag && ((ListControl)cmbFiltro).SelectedIndex == 2;
			ComboBox val8 = cmbProp;
			CheckBox val9 = chkInt;
			bool flag2 = (((Control)cmbFov).Enabled = flag);
			bool flag4 = flag2;
			flag2 = (((Control)val9).Enabled = flag4);
			bool enabled = flag2;
			((Control)val8).Enabled = enabled;
			if (!flag)
			{
				((Control)lblDica).Text = "Neste modo o jogo roda em 1024x768 sem os filtros nem o widescreen.";
			}
			else if (((ListControl)cmbFov).SelectedIndex != 0)
			{
				((Control)lblDica).Text = "Widescreen nativo é EXPERIMENTAL: o mundo fica mais largo, mas partes da interface (menus, minimapa) podem ficar fora do lugar. Em teste.";
			}
			else if (((ListControl)cmbFiltro).SelectedIndex != 2)
			{
				((Control)lblDica).Text = "A escala só vale para o filtro HD.";
			}
			else
			{
				((Control)lblDica).Text = "Automática usa 2x em telas até 2048 px (1080p) e 3x em 4K. Se o jogo ficar lento, use 2x ou o filtro Nítido.";
			}
		};
		cmbModo.SelectedIndexChanged += eventHandler;
		cmbFiltro.SelectedIndexChanged += eventHandler;
		cmbFov.SelectedIndexChanged += eventHandler;
		eventHandler(null, null);
		((Control)val6).Click += (object s, EventArgs e) =>
		{
			ini.Write("Tela", "Modo", ((ListControl)cmbModo).SelectedIndex.ToString());
			ini.Write("Tela", "ManterProporcao", (((ListControl)cmbProp).SelectedIndex == 0) ? "1" : "0");
			ini.Write("Tela", "FOV", ((ListControl)cmbFov).SelectedIndex.ToString());
			ini.Write("Tela", "Filtro", ((ListControl)cmbFiltro).SelectedIndex.ToString());
			ini.Write("Tela", "Escala", escalaVals[((ListControl)cmbEscala).SelectedIndex].ToString());
			ini.Write("Tela", "EscalaInteira", chkInt.Checked ? "1" : "0");
			ini.Write("Tela", "AltEnter", chkAlt.Checked ? "1" : "0");
			f.DialogResult = (DialogResult)1;
		};
		((Control)f).Controls.AddRange(new Control[15]
		{
			(Control)val,
			(Control)cmbModo,
			(Control)val2,
			(Control)cmbProp,
			(Control)val3,
			(Control)cmbFov,
			(Control)val4,
			(Control)cmbFiltro,
			(Control)val5,
			(Control)cmbEscala,
			(Control)lblDica,
			(Control)chkInt,
			(Control)chkAlt,
			(Control)val6,
			(Control)val7
		});
		f.AcceptButton = (IButtonControl)(object)val6;
		f.CancelButton = (IButtonControl)(object)val7;
		f.ShowDialog((IWin32Window)(object)this);
		((Component)(object)f).Dispose();
	}

	protected override void Dispose(bool disposing)
	{
		servidor?.Parar();
		if (disposing)
		{
			((Component)(object)anim).Dispose();
			if (bgScaled != null)
			{
				((Image)bgScaled).Dispose();
			}
			if (art != null)
			{
				art.Dispose();
			}
		}
		((Form)this).Dispose(disposing);
	}

	[STAThread]
	private static void Main()
	{
		Application.EnableVisualStyles();
		Application.SetCompatibleTextRenderingDefault(false);
		Application.Run((Form)(object)new MainForm());
	}
}
