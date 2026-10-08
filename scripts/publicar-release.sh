#!/usr/bin/env bash
# publicar-release.sh — monta o SHA256SUMS do release do launcher e assina com Ed25519.
#
# O pacote do release precisa conter, além dos arquivos do launcher:
#   SHA256SUMS      — "hash  arquivo" (formato sha256sum, duas casas, ordem alfabética)
#   SHA256SUMS.sig  — Ed25519 sobre os bytes exatos do SHA256SUMS
# O launcher confere a assinatura com a chave pública EMBUTIDA e depois hash por hash;
# qualquer divergência = atualização não aplicada.
#
# Uso:
#   publicar-release.sh --check <pasta-do-release>            # read-only: plano + arquivos
#   publicar-release.sh <pasta-do-release> <chave-privada.pem>
#
# Exit codes: 0 ok | 2 pré-requisito ausente | 4 falha de assinatura
set -euo pipefail

log() { echo "[$(date '+%F %T')] $*"; }
req_pre() { log "ERRO (pré-requisito): $*"; exit 2; }

CHECK=0
if [[ ${1:-} == "--check" ]]; then CHECK=1; shift; fi
[[ $# -eq 2 || ($CHECK -eq 1 && $# -eq 1) ]] || { sed -n '2,13p' "$0" | sed 's/^# \{0,1\}//'; exit 2; }

PASTA=$1
CHAVE=${2:-}
[[ -d $PASTA ]] || req_pre "pasta do release não existe: $PASTA"
if [[ $CHECK -eq 0 ]]; then
  [[ -n $CHAVE && -r $CHAVE ]] || req_pre "chave privada ausente/ilegível: $CHAVE"
  command -v openssl >/dev/null || req_pre "openssl não encontrado"
fi

ARQUIVOS=$(find "$PASTA" -maxdepth 1 -type f ! -name 'SHA256SUMS*' -printf '%f\n' | sort)
[[ -n $ARQUIVOS ]] || req_pre "pasta sem arquivos para publicar"

echo "PLANO:"
echo " [1/4] arquivos do release:"
echo "$ARQUIVOS" | sed 's/^/       - /'
echo " [2/4] gerar SHA256SUMS (formato sha256sum, ordem alfabética)"
echo " [3/4] assinar com Ed25519 -> SHA256SUMS.sig"
echo " [4/4] auto-verificação (openssl confere a assinatura que acabou de criar)"
[[ $CHECK -eq 1 ]] && { echo "Check: nada foi escrito."; exit 0; }

log "[2/4] SHA256SUMS..."
( cd "$PASTA" && echo "$ARQUIVOS" | xargs sha256sum | sed 's|\(  \)\./\?|\1|' > SHA256SUMS )
# sha256sum imprime "<hash>  <caminho>"; garantimos o separador de duas casas sem "./"
cat "$PASTA/SHA256SUMS"

log "[3/4] assinando..."
openssl dgst -sha256 -sign "$CHAVE" -out "$PASTA/SHA256SUMS.sig" "$PASTA/SHA256SUMS"

log "[4/4] auto-verificação..."
openssl dgst -sha256 -verify <(openssl pkey -in "$CHAVE" -pubout 2>/dev/null) \
  -signature "$PASTA/SHA256SUMS.sig" "$PASTA/SHA256SUMS" >/dev/null \
  || { log "ERRO: assinatura não bateu com a própria chave — abortado."; exit 4; }

log "OK — anexe ao release do GitHub: os arquivos + SHA256SUMS + SHA256SUMS.sig"
log "Publique também a chave pública (chave-publica.pem/.der) — quem embute no launcher usa o .der"
