using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WonderLauncher;

internal sealed class StatusServidor
{
	public const int PortaPadrao = 12005;

	private readonly string host;

	private readonly int porta;

	private Timer timer;

	public EstadoServidor Estado { get; private set; }

	public int Jogadores { get; private set; } = -1;

	public int PingMs { get; private set; }

	public bool CadastroAberto { get; private set; }

	public event Action Mudou;

	public StatusServidor(string host, int porta)
	{
		this.host = host;
		this.porta = porta;
	}

	public void Iniciar()
	{
		if (string.IsNullOrEmpty(host))
		{
			Estado = EstadoServidor.Offline;
			return;
		}
		timer = new Timer(delegate
		{
			Consultar();
		}, null, 0, 30000);
	}

	public void Parar()
	{
		timer?.Dispose();
	}

	public void ConsultarAgora()
	{
		ThreadPool.QueueUserWorkItem(delegate
		{
			Consultar();
		});
	}

	private void Consultar()
	{
		try
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			using TcpClient tcpClient = new TcpClient();
			IAsyncResult asyncResult = tcpClient.BeginConnect(host, porta, null, null);
			if (!asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5.0)))
			{
				Atualizar(EstadoServidor.Offline, -1, 0, cadastro: false);
				return;
			}
			tcpClient.EndConnect(asyncResult);
			int ping = (int)stopwatch.ElapsedMilliseconds;
			tcpClient.ReceiveTimeout = 5000;
			tcpClient.SendTimeout = 5000;
			NetworkStream stream = tcpClient.GetStream();
			byte[] bytes = Encoding.ASCII.GetBytes("WKSTATUS 1\n");
			stream.Write(bytes, 0, bytes.Length);
			byte[] array = new byte[128];
			int i;
			int num;
			for (i = 0; i < array.Length; i += num)
			{
				if ((num = stream.Read(array, i, array.Length - i)) <= 0)
				{
					break;
				}
			}
			string[] array2 = Encoding.ASCII.GetString(array, 0, i).Trim().Split(new char[1] { ' ' });
			if (array2.Length >= 3 && array2[0] == "OK" && int.TryParse(array2[1], out var result))
			{
				Atualizar(EstadoServidor.Online, result, ping, array2[2] == "1");
			}
			else
			{
				Atualizar(EstadoServidor.Online, -1, ping, cadastro: false);
			}
		}
		catch (Exception ex) when (ex is SocketException || ex is IOException || ex is ObjectDisposedException)
		{
			Atualizar(EstadoServidor.Offline, -1, 0, cadastro: false);
		}
	}

	private void Atualizar(EstadoServidor estado, int jogadores, int ping, bool cadastro)
	{
		Estado = estado;
		Jogadores = jogadores;
		PingMs = ping;
		CadastroAberto = cadastro;
		Mudou?.Invoke();
	}
}
