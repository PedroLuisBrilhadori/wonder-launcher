# Canal de releases assinados — como funciona

O launcher só aplica uma atualização se ela vier com **`SHA256SUMS` + `SHA256SUMS.sig`**
válidos. A assinatura é **ECDSA P-256 (SHA-256)** feita com `openssl dgst`; o launcher
verifica com a classe `VerificacaoAtualizacao` (C#, `ECDsa` nativo — zero dependências)
usando a chave pública **embutida** nele. Sem assinatura válida, nada é executado nem
substituído.

## Custódia das chaves (uma vez)

```bash
scripts/gerar-chaves.sh /caminho/seguro/das-chaves
```

- `chave-privada.pem` — **NUNCA vai para o git nem para o release.** Fica com quem
  publica (cofre do mantenedor; arquivo 600).
- `chave-publica.pem` / `chave-publica.der` — públicas: vão no release e o **.der é
  embutido no launcher** (é ele que o `Verificar()` usa).
- As chaves em `chaves/teste/` são de **teste** (a privada de teste está commitada de
  propósito — não vale nada; os testes usam a fixture assinada com ela).

## Publicar um release

1. Build do launcher + junte numa pasta só os arquivos do release.
2. `scripts/publicar-release.sh <pasta> <chave-privada.pem>` — gera `SHA256SUMS`
   (formato `sha256sum`), assina e **auto-verifica** antes de declarar OK.
3. Anexe ao release do GitHub: os arquivos + `SHA256SUMS` + `SHA256SUMS.sig` +
   `chave-publica.der` (para conferência pública).
4. Faça o launcher baixar tudo para uma pasta temporária, chamar
   `VerificacaoAtualizacao.Verificar(pastaTemp, chavePublicaEmbutida)` e SÓ depois
   aplicar (substituir arquivos/executar). `Falha` = descartar e avisar o jogador.

## Testes

```bash
dotnet test tests/Verificacao.Tests.csproj
```

A fixture (`chaves/teste/fixture/`) é assinada pela chave de teste com o mesmo script
de publicação — o round-trip openssl → .NET fica travado pelos testes (assinatura
válida passa; adulteração de 1 byte, chave errada e falta de assinatura reprovam).

> ⚠️ **Ambiente**: o container `mcr.microsoft.com/dotnet/sdk:10.0` (Debian + OpenSSL
> 3.5) está **falhando verificação ECDSA até para o vetor oficial do RFC 6979** (bug
> do interop .NET↔OpenSSL 3.5 daquela imagem — comprovado 2026-10-07). O alvo do
> launcher é **Windows**, onde o .NET usa CNG (outro backend, sem esse problema):
> rode os testes no Windows antes de abrir mão deles. O lado release usa só
> `openssl dgst`, que python/openssl/Windows concordam entre si.

## Lição registrada (OpenSSL 3.x)

`openssl pkeyutl -sign` **sem** `-rawin` no OpenSSL 3.x não faz mais "a entrada É o
digest" como no 1.1 — ele fez hashing por conta própria e o *verify* dele se
auto-concordava (dava "Verified Successfully" para uma assinatura que python e .NET
rejeitavam). Use **`openssl dgst -sha256 -sign/-verify`** — interface clássica, todos
os ecossistemas concordam.

## Migração (launchers já distribuídos)

Os binários v3.5 apontavam para `guisq1515/wonder-launcher` (repo **404** hoje — o
auto-update deles falha em silêncio, por design do LEIA-ME "sem internet ele pula").
A nova home é este repo; jogadores atualizam baixando o pacote novo uma última vez
(quando existir release aqui, o launcher novo já nasce apontando para cá e com a
chave embutida).
