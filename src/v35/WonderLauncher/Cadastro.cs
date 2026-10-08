using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace WonderLauncher;

internal static class Cadastro
{
	private const string ChavePublica = "<RSAKeyValue><Modulus>roHpoXqFIy7QS5F44Tv4a+BrH7a+uxKOPj6tNO5CAnYq46LDL7460w07y7BTfHXelhRvbF0I0mpcdSrOWm9ARNIBbkw+IWHejUs2cYVysG02+MaTvb3C9RNnhrGgKiZmmZarWoa8E7NDuFIt4JjWDUZh3l77W6HjPg9CjjdV5gTihoR9E+vW2va8s4W5FvPp4MjlQuDFqr6UiXyFLSOraYr0sJP3z5UjXJtXP1Rc7XVf2/ivchsnWuNWLd1HQoqHAB8Cx3EjJq3VcN8NAuwRkjCjVakCBekfXu9/k2OLYjgHLmdHuA1sGBQ7nzoz5hVDMc617+LXHb5QFa7sqSi+/Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

	private const int UserMin = 4;

	private const int UserMax = 16;

	private const int PassMin = 6;

	private const int PassMax = 32;

	private static string Selar(string user, string senha)
	{
		using RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider();
		rSACryptoServiceProvider.FromXmlString("<RSAKeyValue><Modulus>roHpoXqFIy7QS5F44Tv4a+BrH7a+uxKOPj6tNO5CAnYq46LDL7460w07y7BTfHXelhRvbF0I0mpcdSrOWm9ARNIBbkw+IWHejUs2cYVysG02+MaTvb3C9RNnhrGgKiZmmZarWoa8E7NDuFIt4JjWDUZh3l77W6HjPg9CjjdV5gTihoR9E+vW2va8s4W5FvPp4MjlQuDFqr6UiXyFLSOraYr0sJP3z5UjXJtXP1Rc7XVf2/ivchsnWuNWLd1HQoqHAB8Cx3EjJq3VcN8NAuwRkjCjVakCBekfXu9/k2OLYjgHLmdHuA1sGBQ7nzoz5hVDMc617+LXHb5QFa7sqSi+/Q==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>");
		return Convert.ToBase64String(rSACryptoServiceProvider.Encrypt(Encoding.ASCII.GetBytes("WK2\n" + user + "\n" + senha + "\n"), fOAEP: true));
	}

	public static string Validar(string user, string senha, string confirma)
	{
		if (user.Length < 4 || user.Length > 16)
		{
			return "O usuário deve ter de " + 4 + " a " + 16 + " caracteres.";
		}
		string text = user;
		foreach (char c in text)
		{
			if ((c < 'a' || c > 'z') && (c < '0' || c > '9'))
			{
				return "O usuário só pode ter letras (a-z) e números, sem espaços nem acentos.";
			}
		}
		if (senha.Length < 6 || senha.Length > 32)
		{
			return "A senha deve ter de " + 6 + " a " + 32 + " caracteres.";
		}
		text = senha;
		foreach (char c2 in text)
		{
			if (c2 <= ' ' || c2 > '~')
			{
				return "A senha não pode ter espaços nem acentos.";
			}
		}
		if (senha != confirma)
		{
			return "A confirmação não bate com a senha.";
		}
		return null;
	}

	public static string Enviar(string host, int porta, string user, string senha)
	{
		try
		{
			using TcpClient tcpClient = new TcpClient();
			IAsyncResult asyncResult = tcpClient.BeginConnect(host, porta, null, null);
			if (!asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(8.0)))
			{
				return "O servidor não respondeu. Ele pode estar desligado ou em manutenção.";
			}
			tcpClient.EndConnect(asyncResult);
			tcpClient.ReceiveTimeout = 10000;
			tcpClient.SendTimeout = 10000;
			NetworkStream stream = tcpClient.GetStream();
			byte[] bytes = Encoding.ASCII.GetBytes("WKCAD 2\n" + Selar(user, senha) + "\n");
			stream.Write(bytes, 0, bytes.Length);
			MemoryStream memoryStream = new MemoryStream();
			byte[] array = new byte[512];
			int count;
			while (memoryStream.Length < 4096 && (count = stream.Read(array, 0, array.Length)) > 0)
			{
				memoryStream.Write(array, 0, count);
			}
			string[] array2 = Encoding.ASCII.GetString(memoryStream.ToArray()).Replace("\r", "").Split(new char[1] { '\n' });
			if (array2[0] == "OK")
			{
				return null;
			}
			if (array2[0].StartsWith("ERRO") && array2.Length > 1 && array2[1].Length > 0)
			{
				return array2[1];
			}
			return "Resposta inesperada do servidor.";
		}
		catch (SocketException)
		{
			return "Não foi possível conectar ao servidor. Tente de novo em alguns minutos.";
		}
		catch (IOException)
		{
			return "A conexão com o servidor caiu. Tente de novo.";
		}
	}

	public static void Mostrar(IWin32Window dono, string host, int porta)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d6: Expected Obj, but got Unknown
		//IL_00db: Expected Obj, but got Unknown
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_014b: Expected Obj, but got Unknown
		//IL_01fb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Expected Obj, but got Unknown
		//IL_020a: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0252: Expected Obj, but got Unknown
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0258: Unknown result type (might be due to invalid IL or missing references)
		//IL_0263: Unknown result type (might be due to invalid IL or missing references)
		//IL_0278: Unknown result type (might be due to invalid IL or missing references)
		//IL_0287: Unknown result type (might be due to invalid IL or missing references)
		//IL_028e: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b2: Expected Obj, but got Unknown
		//IL_02b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e6: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ed: Unknown result type (might be due to invalid IL or missing references)
		//IL_02f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0305: Unknown result type (might be due to invalid IL or missing references)
		//IL_030e: Expected Obj, but got Unknown
		//IL_0404: Unknown result type (might be due to invalid IL or missing references)
		Color backColor = Color.FromArgb(28, 34, 42);
		Color panel = Color.FromArgb(40, 48, 58);
		Color fg = Color.FromArgb(225, 230, 236);
		Color dim = Color.FromArgb(150, 160, 172);
		Form f = new Form
		{
			Text = "Criar conta",
			ClientSize = new Size(360, 318),
			StartPosition = (FormStartPosition)4,
			FormBorderStyle = (FormBorderStyle)3,
			MaximizeBox = false,
			MinimizeBox = false,
			ShowInTaskbar = false,
			BackColor = backColor,
			ForeColor = fg,
			Font = new Font("Segoe UI", 9.5f)
		};
		Func<string, int, Label> func = (string t, int y) =>
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_000c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0022: Expected Obj, but got Unknown
			return new Label
			{
				Text = t,
				Location = new Point(20, y),
				AutoSize = true
			};
		};
		Func<int, bool, TextBox> func2 = (int y, bool senha) =>
		{
			//IL_0000: Unknown result type (might be due to invalid IL or missing references)
			//IL_0005: Unknown result type (might be due to invalid IL or missing references)
			//IL_0013: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			//IL_0036: Unknown result type (might be due to invalid IL or missing references)
			//IL_003d: Unknown result type (might be due to invalid IL or missing references)
			//IL_0044: Unknown result type (might be due to invalid IL or missing references)
			//IL_0054: Expected Obj, but got Unknown
			return new TextBox
			{
				Location = new Point(20, y),
				Width = 320,
				BackColor = panel,
				ForeColor = fg,
				BorderStyle = (BorderStyle)1,
				UseSystemPasswordChar = senha,
				MaxLength = (senha ? 32 : 16)
			};
		};
		TextBox txtUser = func2(38, arg2: false);
		txtUser.CharacterCasing = (CharacterCasing)2;
		TextBox txtSenha = func2(98, arg2: true);
		TextBox txtConf = func2(158, arg2: true);
		Label val = new Label();
		((Control)val).Text = "Usuário: " + 4 + " a " + 16 + " letras ou números. Senha: " + 6 + " a " + 32 + " caracteres, sem espaços nem acentos.";
		((Control)val).Location = new Point(20, 192);
		((Control)val).Size = new Size(320, 36);
		((Control)val).ForeColor = dim;
		((Control)val).Font = new Font("Segoe UI", 8.5f);
		Label val2 = val;
		Label lblStatus = new Label
		{
			Location = new Point(20, 230),
			Size = new Size(320, 36),
			ForeColor = Color.FromArgb(255, 150, 140)
		};
		Button btnCriar = new Button
		{
			Text = "Criar",
			Location = new Point(150, 272),
			Size = new Size(92, 32),
			FlatStyle = (FlatStyle)0,
			BackColor = Color.FromArgb(38, 110, 190),
			ForeColor = Color.White
		};
		Button val3 = new Button
		{
			Text = "Cancelar",
			Location = new Point(248, 272),
			Size = new Size(92, 32),
			FlatStyle = (FlatStyle)0,
			BackColor = panel,
			ForeColor = fg,
			DialogResult = (DialogResult)2
		};
		((ButtonBase)btnCriar).FlatAppearance.BorderColor = Color.FromArgb(80, 150, 225);
		((ButtonBase)val3).FlatAppearance.BorderColor = Color.FromArgb(70, 80, 92);
		((Control)btnCriar).Click += (object s, EventArgs e) =>
		{
			string user = ((Control)txtUser).Text.Trim();
			string text = Validar(user, ((Control)txtSenha).Text, ((Control)txtConf).Text);
			if (text != null)
			{
				((Control)lblStatus).Text = text;
			}
			else if (string.IsNullOrEmpty(host))
			{
				((Control)lblStatus).Text = "Não achei o IP do servidor no info.ini.";
			}
			else
			{
				string senha = ((Control)txtSenha).Text;
				((Control)btnCriar).Enabled = false;
				TextBox val4 = txtUser;
				TextBox val5 = txtSenha;
				bool flag = (((Control)txtConf).Enabled = false);
				bool enabled = (((Control)val5).Enabled = flag);
				((Control)val4).Enabled = enabled;
				((Control)lblStatus).ForeColor = dim;
				((Control)lblStatus).Text = "Criando a conta...";
				BackgroundWorker backgroundWorker = new BackgroundWorker();
				backgroundWorker.DoWork += (object obj, DoWorkEventArgs e2) =>
				{
					e2.Result = Enviar(host, porta, user, senha);
				};
				backgroundWorker.RunWorkerCompleted += (object obj, RunWorkerCompletedEventArgs e2) =>
				{
					//IL_005f: Unknown result type (might be due to invalid IL or missing references)
					if (!((Control)f).IsDisposed)
					{
						string text2 = ((e2.Error != null) ? e2.Error.Message : ((string)e2.Result));
						if (text2 == null)
						{
							MessageBox.Show((IWin32Window)(object)f, "Conta \"" + user + "\" criada!\nUse esse usuário e a senha para entrar no jogo.", "Wonder Classic", (MessageBoxButtons)0, (MessageBoxIcon)64);
							f.DialogResult = (DialogResult)1;
						}
						else
						{
							((Control)lblStatus).ForeColor = Color.FromArgb(255, 150, 140);
							((Control)lblStatus).Text = text2;
							((Control)btnCriar).Enabled = true;
							TextBox val6 = txtUser;
							TextBox val7 = txtSenha;
							bool flag4 = (((Control)txtConf).Enabled = true);
							bool enabled2 = (((Control)val7).Enabled = flag4);
							((Control)val6).Enabled = enabled2;
						}
					}
				};
				backgroundWorker.RunWorkerAsync();
			}
		};
		((Control)f).Controls.AddRange(new Control[10]
		{
			(Control)func("Usuário", 16),
			(Control)txtUser,
			(Control)func("Senha", 76),
			(Control)txtSenha,
			(Control)func("Confirmar senha", 136),
			(Control)txtConf,
			(Control)val2,
			(Control)lblStatus,
			(Control)btnCriar,
			(Control)val3
		});
		f.AcceptButton = (IButtonControl)(object)btnCriar;
		f.CancelButton = (IButtonControl)(object)val3;
		f.ShowDialog(dono);
		((Component)(object)f).Dispose();
	}
}
