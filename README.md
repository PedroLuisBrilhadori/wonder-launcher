# wonder-launcher

Launcher do WonderKing Classic (servidor WkEmu).

## Descoberta importante (2026-10-07 — fonte recuperada)

A fonte do launcher v3.5 (2,1 MB) foi **recuperada por descompilação** (ILSpy) e
está em `src/v35/` — o autor deve conferir/substituir pelo original. A auditoria
mostrou que a segurança já era boa:

- **Atualizações já eram assinadas**: `Atualizador.LerManifesto` valida um
  `atualizacao.txt` (formato `WKUPD|1`, campos, `arquivo|nome|url|sha256`) com
  **RSA-2048 SHA-256** contra a chave pública **embutida** no launcher. Hash de
  cada arquivo, guarda contra path traversal, teto de 64 MB, TLS12 forçado.
- **Cadastro já cifrava a senha de verdade**: RSA-**OAEP** com chave pública do
  servidor (`Cadastro.Selar`) — "só o servidor lê" se cumpre.

O que estava quebrado: **o canal** — o repo `guisq1515/wonder-launcher` (URL
embutida nos launchers distribuídos) está **404**, então o auto-update falha em
silêncio. E a fonte não estava em git.

## O ativo crítico

A **chave privada RSA** que assina o `atualizacao.txt`: sem ela, ninguém publica
update que os launchers já distribuídos aceitem (eles só confiam na pública
embutida). Está com o autor do launcher — custódia planejada: espaço `wk/` no
OpenBao da casa. Se estiver perdida: re-keying (nova chave pública embutida =
nova build distribuída manualmente uma última vez).

## Tooling deste branch

| Script | O quê |
|---|---|
| `scripts/gerar-chaves.sh` | par P-256 do esquema novo (SHA256SUMS + .sig) |
| `scripts/publicar-release.sh` | SHA256SUMS + assinatura (defesa em profundidade) |
| `scripts/gerar-atualizacao.sh` | **monta e assina o `atualizacao.txt` no formato exato do launcher** (round-trip provado contra a lógica decompilada) |

```bash
# canal do launcher (formato nativo — o que os launchers atuais consomem):
scripts/gerar-atualizacao.sh <pasta-release> <chave-rsa-privada.pem> "nota do update" \
  [--versao 3.5.2.0 --launcher WonderLauncher.exe]
# camada extra (SHA256SUMS assinado — defesa em profundidade):
scripts/publicar-release.sh <pasta> <chave-p256-privada.pem>
```

| Caminho | O quê |
|---|---|
| `src/v35/` | fonte v3.5 recuperada (net40 WinForms; as DLLs bg/dinput8/wkhdmod são recursos embutidos do projeto) |
| `src/VerificacaoAtualizacao.cs` | verificação ECDSA (esquema novo, drop-in p/ builds futuros) |
| `tests/` | round-trips das duas camadas |
| `docs/release.md` | fluxo, custódia, lições (openssl 3.x, container) |

## Pendências

- [ ] **Autor**: confirmar a fonte recuperada; informar se a **chave privada RSA** do canal existe — define restaurar o canal (mesma chave) ou re-key (nova build).
- [ ] Publicar `atualizacao.txt` assinado num release desta home (o canal volta a respirar; launchers antigos só apontam pra URL nova após 1 update manual).
- [ ] Custódia das chaves no OpenBao (`wk/`) — depois da anomalia de acesso à db01 resolvida.
- [ ] Remover `GameGuard.des` do pacote de distribuição (peso morto, único flag legítimo do VT).
