using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace WonderLauncher;

internal sealed class Atualizador
{
	public const string UrlPadrao = "https://github.com/guisq1515/wonder-launcher/releases/latest/download/atualizacao.txt";

	private const string ChavePublica = "<RSAKeyValue><Modulus>tN91p3KQeHpWrZqhSLGF3Es/SeZmc5rsRMQ8A/ik7JaFf2olzHRLHQgTSlONJ5IbwWMlS+cY2mD7Vxy8bz6CDpwfP/eoqrGNcVlPoy+hQq0blYGwlgQdoCxXy9Z4hGuWLtcVQ1a+vPQVeMr6fgUbSKO4JJm+N49mRF7JIGreRraKz7tNZWeXJs1eZAQBu182kalAZBLhjn38jdv+S93JwDfialnbLRFj0CIEIi5/X3PVDDy14eZJo8aLi6W54UISdvdTEC0meCwer2kpYQewKQEe6yzk+EpE4eFtJeK61GBTZrnxp7V+tnog7kpmKw+NR3A4Br/mhja6HWaxawU6tsWrrHlWjPyyQVMeersmck5MXViGhD3YyIakRMnA2+BN3vY07zpDowlrPodVsrkJachPE5mv19LG+j1GuiWa9eJ7YmptqTmli6G16E2mrAw00lV8xHVzLWh7tWIVv+7wTm9GvXcmLhC5lUkKKDTSe5FB8pD6Qwz3EQBnwKtnutL9</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

	public const string ArgReiniciado = "--atualizado";

	private readonly string gameDir;

	private readonly string url;

	private readonly Action<string, float> progresso;

	public string Nota;

	public static Version VersaoAtual => Assembly.GetExecutingAssembly().GetName().Version;

	private static string ExePath => Assembly.GetExecutingAssembly().Location;

	public Atualizador(string gameDir, string url, Action<string, float> progresso)
	{
		this.gameDir = gameDir;
		this.url = url;
		this.progresso = progresso;
	}

	public static void LimparSobras()
	{
		string path = ExePath + ".velho";
		for (int i = 0; i < 20; i++)
		{
			if (!File.Exists(path))
			{
				break;
			}
			try
			{
				File.Delete(path);
			}
			catch (Exception)
			{
				Thread.Sleep(250);
			}
		}
	}

	public bool Executar(bool podeTrocarLauncher)
	{
		try
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
		}
		catch (NotSupportedException)
		{
		}
		progresso("PROCURANDO ATUALIZACAO", 0f);
		Uri uri = new Uri(url);
		if (!LerManifesto(Baixar(uri, 8000), out var campos, out var arquivos))
		{
			throw new InvalidDataException("assinatura da atualizacao invalida");
		}
		campos.TryGetValue("nota", out Nota);
		foreach (string[] item in arquivos)
		{
			AtualizarArquivo(uri, item[1], item[2], item[3]);
		}
		if (!podeTrocarLauncher || !campos.ContainsKey("versao") || !campos.ContainsKey("launcher"))
		{
			return false;
		}
		if (new Version(campos["versao"]) <= VersaoAtual)
		{
			return false;
		}
		string[] array = campos["launcher"].Split(new char[1] { '|' });
		if (array.Length < 2)
		{
			return false;
		}
		progresso("BAIXANDO LAUNCHER " + campos["versao"], 0.3f);
		byte[] array2 = Baixar(new Uri(uri, array[0]), 120000);
		if (!string.Equals(Sha256(array2), array[1], StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("launcher baixado veio corrompido");
		}
		TrocarExe(array2);
		return true;
	}

	public static void Reiniciar()
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = ExePath,
			Arguments = "--atualizado",
			WorkingDirectory = Path.GetDirectoryName(ExePath),
			UseShellExecute = false
		});
	}

	private static void TrocarExe(byte[] exe)
	{
		string exePath = ExePath;
		string text = exePath + ".novo";
		string text2 = exePath + ".velho";
		File.WriteAllBytes(text, exe);
		if (File.Exists(text2))
		{
			File.Delete(text2);
		}
		File.Move(exePath, text2);
		try
		{
			File.Move(text, exePath);
		}
		catch
		{
			File.Move(text2, exePath);
			throw;
		}
	}

	private void AtualizarArquivo(Uri baseUri, string rel, string urlArq, string hash)
	{
		rel = rel.Replace('/', Path.DirectorySeparatorChar);
		if (rel.Length == 0 || Path.IsPathRooted(rel) || rel.Contains(".."))
		{
			return;
		}
		string text = Path.Combine(gameDir, rel);
		if (File.Exists(text))
		{
			using FileStream inputStream = File.OpenRead(text);
			using SHA256 sHA = SHA256.Create();
			if (string.Equals(Hex(sHA.ComputeHash(inputStream)), hash, StringComparison.OrdinalIgnoreCase))
			{
				return;
			}
		}
		progresso("BAIXANDO " + Path.GetFileName(rel).ToUpperInvariant(), 0.1f);
		byte[] array = Baixar(new Uri(baseUri, urlArq), 120000);
		if (!string.Equals(Sha256(array), hash, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException(rel + " baixado veio corrompido");
		}
		string directoryName = Path.GetDirectoryName(text);
		if (!Directory.Exists(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
		string text2 = text + ".baixando";
		File.WriteAllBytes(text2, array);
		if (File.Exists(text))
		{
			File.Delete(text);
		}
		File.Move(text2, text);
	}

	internal static bool LerManifesto(byte[] bruto, out Dictionary<string, string> campos, out List<string[]> arquivos)
	{
		campos = new Dictionary<string, string>();
		arquivos = new List<string[]>();
		string text = Encoding.UTF8.GetString(bruto).TrimStart(new char[1] { '\ufeff' }).Replace("\r", "");
		StringBuilder stringBuilder = new StringBuilder();
		string text2 = null;
		string[] array = text.Split(new char[1] { '\n' });
		foreach (string text3 in array)
		{
			if (text3.Length != 0)
			{
				if (text3.StartsWith("assinatura|"))
				{
					text2 = text3.Substring(11).Trim();
					break;
				}
				stringBuilder.Append(text3).Append('\n');
				string[] array2 = text3.Split(new char[1] { '|' });
				if (array2[0] == "arquivo" && array2.Length >= 4)
				{
					arquivos.Add(array2);
				}
				else if (array2.Length >= 2 && array2[0] != "arquivo")
				{
					campos[array2[0]] = text3.Substring(array2[0].Length + 1);
				}
			}
		}
		if (text2 == null || !campos.TryGetValue("WKUPD", out var value) || value != "1")
		{
			return false;
		}
		byte[] signature;
		try
		{
			signature = Convert.FromBase64String(text2);
		}
		catch (FormatException)
		{
			return false;
		}
		using RSACryptoServiceProvider rSACryptoServiceProvider = new RSACryptoServiceProvider(new CspParameters(24));
		rSACryptoServiceProvider.PersistKeyInCsp = false;
		rSACryptoServiceProvider.FromXmlString("<RSAKeyValue><Modulus>tN91p3KQeHpWrZqhSLGF3Es/SeZmc5rsRMQ8A/ik7JaFf2olzHRLHQgTSlONJ5IbwWMlS+cY2mD7Vxy8bz6CDpwfP/eoqrGNcVlPoy+hQq0blYGwlgQdoCxXy9Z4hGuWLtcVQ1a+vPQVeMr6fgUbSKO4JJm+N49mRF7JIGreRraKz7tNZWeXJs1eZAQBu182kalAZBLhjn38jdv+S93JwDfialnbLRFj0CIEIi5/X3PVDDy14eZJo8aLi6W54UISdvdTEC0meCwer2kpYQewKQEe6yzk+EpE4eFtJeK61GBTZrnxp7V+tnog7kpmKw+NR3A4Br/mhja6HWaxawU6tsWrrHlWjPyyQVMeersmck5MXViGhD3YyIakRMnA2+BN3vY07zpDowlrPodVsrkJachPE5mv19LG+j1GuiWa9eJ7YmptqTmli6G16E2mrAw00lV8xHVzLWh7tWIVv+7wTm9GvXcmLhC5lUkKKDTSe5FB8pD6Qwz3EQBnwKtnutL9</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>");
		return rSACryptoServiceProvider.VerifyData(Encoding.UTF8.GetBytes(stringBuilder.ToString()), "SHA256", signature);
	}

	private static byte[] Baixar(Uri uri, int timeoutMs)
	{
		HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
		httpWebRequest.Timeout = timeoutMs;
		httpWebRequest.ReadWriteTimeout = timeoutMs;
		httpWebRequest.UserAgent = "WonderLauncher/" + VersaoAtual;
		httpWebRequest.AllowAutoRedirect = true;
		httpWebRequest.CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore);
		using WebResponse webResponse = httpWebRequest.GetResponse();
		using Stream stream = webResponse.GetResponseStream();
		MemoryStream memoryStream = new MemoryStream();
		byte[] array = new byte[65536];
		int count;
		while ((count = stream.Read(array, 0, array.Length)) > 0)
		{
			memoryStream.Write(array, 0, count);
			if (memoryStream.Length > 67108864)
			{
				throw new InvalidDataException("arquivo grande demais");
			}
		}
		return memoryStream.ToArray();
	}

	private static string Sha256(byte[] dados)
	{
		using SHA256 sHA = SHA256.Create();
		return Hex(sHA.ComputeHash(dados));
	}

	private static string Hex(byte[] b)
	{
		StringBuilder stringBuilder = new StringBuilder(b.Length * 2);
		foreach (byte b2 in b)
		{
			stringBuilder.Append(b2.ToString("x2"));
		}
		return stringBuilder.ToString();
	}
}
