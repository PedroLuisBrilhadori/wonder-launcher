using Xunit;
using WonderLauncher;
using System.Security.Cryptography;

namespace WonderLauncher.Tests;

/// <summary>
/// Verificação da atualização: assinatura ECDSA P-256 do SHA256SUMS (fixture gerada pelo
/// scripts/publicar-release.sh com a chave de TESTE commitada em chaves/teste/) e os
/// hashes de cada arquivo. O round-trip openssl→.NET prova a interoperabilidade.
/// </summary>
public class VerificacaoTests
{
    static string RepoRoot => EncontraRaiz();
    static string PastaFixture => Path.Combine(RepoRoot, "chaves", "teste", "fixture");
    static byte[] ChavePublicaDer => File.ReadAllBytes(Path.Combine(RepoRoot, "chaves", "teste", "chave-publica-teste.der"));

    static string EncontraRaiz()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && !File.Exists(Path.Combine(d.FullName, "chaves", "teste", "chave-publica-teste.der")))
            d = d.Parent!;
        return d?.FullName ?? throw new InvalidOperationException("raiz do repo não encontrada a partir do binário de teste");
    }

    /// <summary>Cópia da fixture em pasta temporária (os testes adulteram cópias, nunca a fixture).</summary>
    static string CopiaFixture()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "verif-teste-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        foreach (var f in Directory.GetFiles(PastaFixture, "*", SearchOption.AllDirectories))
            File.Copy(f, Path.Combine(tmp, Path.GetRelativePath(PastaFixture, f)), overwrite: true);
        return tmp;
    }

    [Fact]
    public void FixtureAssinadaPeloOpensslPassa()
    {
        var pasta = CopiaFixture();
        try { VerificacaoAtualizacao.Verificar(pasta, ChavePublicaDer); }
        finally { Directory.Delete(pasta, recursive: true); }
    }

    [Fact]
    public void SumsAdulteradoReprova()
    {
        var pasta = CopiaFixture();
        try
        {
            var sums = Path.Combine(pasta, VerificacaoAtualizacao.ArquivoSums);
            File.WriteAllText(sums, File.ReadAllText(sums).Replace("versao", "versaO")); // 1 byte mudou
            Assert.Throws<VerificacaoAtualizacao.Falha>(() => VerificacaoAtualizacao.Verificar(pasta, ChavePublicaDer));
        }
        finally { Directory.Delete(pasta, recursive: true); }
    }

    [Fact]
    public void ArquivoAdulteradoReprova()
    {
        var pasta = CopiaFixture();
        try
        {
            var qualquer = Directory.GetFiles(pasta).First(f => !f.EndsWith(".sig") && !f.EndsWith("SHA256SUMS"));
            File.AppendAllText(qualquer, "inject");
            Assert.Throws<VerificacaoAtualizacao.Falha>(() => VerificacaoAtualizacao.Verificar(pasta, ChavePublicaDer));
        }
        finally { Directory.Delete(pasta, recursive: true); }
    }

    [Fact]
    public void SemAssinaturaReprova()
    {
        var pasta = CopiaFixture();
        try
        {
            File.Delete(Path.Combine(pasta, VerificacaoAtualizacao.ArquivoAssinatura));
            Assert.Throws<VerificacaoAtualizacao.Falha>(() => VerificacaoAtualizacao.Verificar(pasta, ChavePublicaDer));
        }
        finally { Directory.Delete(pasta, recursive: true); }
    }

    [Fact]
    public void ChaveErradaReprova()
    {
        var pasta = CopiaFixture();
        try
        {
            using var outra = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var derOutra = outra.ExportSubjectPublicKeyInfo();
            Assert.Throws<VerificacaoAtualizacao.Falha>(() => VerificacaoAtualizacao.Verificar(pasta, derOutra));
        }
        finally { Directory.Delete(pasta, recursive: true); }
    }

    [Fact]
    public void RoundTripInProcessAssinaEVerifica()
    {
        // a mesma chave privada de teste (PEM PKCS#8 do openssl) assina em processo .NET
        var pem = File.ReadAllText(Path.Combine(RepoRoot, "chaves", "teste", "chave-privada-teste.pem"));
        using var ed = ECDsa.Create();
        ed.ImportFromPem(pem.AsSpan());
        var dados = "linha 1\nlinha 2\n"u8.ToArray();
        var assinatura = ed.SignData(dados, HashAlgorithmName.SHA256);
        Assert.True(ed.VerifyData(dados, assinatura, HashAlgorithmName.SHA256));
        Assert.False(ed.VerifyData("linha 3\n"u8.ToArray(), assinatura, HashAlgorithmName.SHA256));
    }
}
