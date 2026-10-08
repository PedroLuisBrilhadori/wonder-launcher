using System.Security.Cryptography;

namespace WonderLauncher;

/// <summary>
/// Verificação da atualização baixada: um SHA256SUMS (formato sha256sum: "hash  arquivo",
/// duas casas, ordem alfabética) assinado com ECDSA P-256 (SHA-256) usando a chave pública
/// EMBUTIDA no launcher. Nada é substituído ou executado antes de: assinatura válida E
/// todos os hashes batendo. A chave nasce de scripts/gerar-chaves.sh — quem tem a privada
/// é só quem publica; a pública (DER SPKI) vai no release e é embutida no launcher.
/// Zero dependências: ECDsa é do próprio .NET, e o openssl assina do lado do release.
/// </summary>
public static class VerificacaoAtualizacao
{
    public const string ArquivoSums = "SHA256SUMS";
    public const string ArquivoAssinatura = "SHA256SUMS.sig";

    /// <summary>Falha de verificação: o launcher NUNCA deve aplicar a atualização neste caso.</summary>
    public sealed class Falha(string motivo) : Exception(motivo) { }

    /// <summary>Verifica assinatura + hashes de todos os arquivos listados no SHA256SUMS da pasta.</summary>
    public static void Verificar(string pasta, byte[] chavePublicaDer)
    {
        var caminhoSums = Path.Combine(pasta, ArquivoSums);
        var caminhoSig = Path.Combine(pasta, ArquivoAssinatura);
        if (!File.Exists(caminhoSums) || !File.Exists(caminhoSig))
            throw new Falha("SHA256SUMS ou SHA256SUMS.sig ausentes no download.");

        var sums = File.ReadAllBytes(caminhoSums);
        var assinatura = File.ReadAllBytes(caminhoSig);
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(chavePublicaDer, out _);
        if (!ecdsa.VerifyData(sums, assinatura, HashAlgorithmName.SHA256))
            throw new Falha("Assinatura do SHA256SUMS inválida — o download não veio da equipe.");

        foreach (var linha in File.ReadAllLines(caminhoSums))
        {
            if (linha.Length == 0) continue;
            if (linha.Length < 67 || linha[64] != ' ' || linha[65] != ' ')
                throw new Falha($"Linha malformada no SHA256SUMS: '{linha[..Math.Min(24, linha.Length)]}…'");
            var hashEsperado = linha[..64];
            var nome = linha[66..].Trim();
            var caminho = Path.Combine(pasta, nome);
            if (!File.Exists(caminho))
                throw new Falha($"Arquivo listado no SHA256SUMS ausente: {nome}");
            var hashReal = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(caminho))).ToLowerInvariant();
            if (hashReal != hashEsperado)
                throw new Falha($"Hash divergente: {nome} (download corrompido ou adulterado).");
        }
    }

    /// <summary>Confere um arquivo isolado contra o hash esperado (hex minúsculo).</summary>
    public static void VerificarArquivo(string caminho, string hashEsperado)
    {
        var hashReal = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(caminho))).ToLowerInvariant();
        if (hashReal != hashEsperado.ToLowerInvariant())
            throw new Falha($"Hash divergente: {Path.GetFileName(caminho)}.");
    }
}
