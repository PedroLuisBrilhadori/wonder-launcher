# wonder-launcher

Launcher do WonderKing Classic (servidor WkEmu). Distribuição com **atualização
verificada**: cada release carrega `SHA256SUMS` + `SHA256SUMS.sig` (ECDSA P-256);
o launcher só aplica atualização com assinatura válida usando a chave pública
embutida nele. Fluxo completo: [docs/release.md](docs/release.md).

## Estado (2026-10-07)

- O repo antigo (`guisq1515/wonder-launcher` — URL embutida nos launchers v3.5 já
  distribuídos) está **404**: o auto-update deles falha em silêncio. Esta é a nova
  casa; o launcher novo nasce apontando pra cá e com a chave embutida.
- A fonte do launcher atual (2.1 MB, .NET) está com o autor — **pendente de push
  aqui**. A UI bootstrap e a fonte da `dinput8.dll` já estão em `src/`.

## O que cada pessoa precisa fazer

### 1. Autor do launcher (tem a fonte do 2.1 MB)
- [ ] Fazer push da fonte atual neste repo (branch ou main).
- [ ] Integrar [`src/VerificacaoAtualizacao.cs`](src/VerificacaoAtualizacao.cs):
      depois de baixar a atualização, chamar `Verificar(pastaTemp, chavePublicaEmbutida)`
      — só aplicar se não lançar `Falha`.
- [ ] Trocar a URL de update para `PedroLuisBrilhadori/wonder-launcher` (ou o org
      final `PbssSoftware`) e embutir o `chave-publica.der` do release.
- [ ] Aproveitar e remover o `GameGuard.des` na instalação ("limpa patches antigos").

### 2. Quem publica releases (dono da chave privada)
- [ ] Gerar o par uma única vez: `scripts/gerar-chaves.sh <pasta-segura>` — a
      `chave-privada.pem` **não sai dali** (cofre/arquivo 600, nunca no git).
- [ ] Por release: juntar os arquivos numa pasta → `scripts/publicar-release.sh
      <pasta> <chave-privada.pem>` → anexar ao release do GitHub os arquivos +
      `SHA256SUMS` + `SHA256SUMS.sig` + `chave-publica.der`.
- [ ] Rodar os testes no Windows (`dotnet test tests/Verificacao.Tests.csproj`) —
      o container Linux do .NET 10 está com o interop ECDSA doente (ver docs).

### 3. Jogadores
- Baixar o pacote, conferir nada (o launcher confere sozinho a partir da versão
  com verificação). Quem quiser conferir na mão: `Get-FileHash` contra o
  `SHA256SUMS` do release.

## Branches
| Branch | Conteúdo |
|---|---|
| `main` | fontes existentes + docs |
| `pedro/assinatura-updates` | verificação de atualização + scripts de assinatura + testes |
