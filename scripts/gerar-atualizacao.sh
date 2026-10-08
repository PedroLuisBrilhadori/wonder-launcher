#!/usr/bin/env bash
# gerar-atualizacao.sh — monta e assina o atualizacao.txt do canal do launcher,
# no formato que o Atualizador.LerManifesto espera (recuperado do v3.5):
#
#   WKUPD|1
#   nota|<texto opcional>
#   versao|<x.y.z.w>            (opcional; com launcher| atualiza o próprio exe)
#   launcher|<arquivo>|<sha256> (opcional)
#   arquivo|<nome>|<url-relativa>|<sha256hex>
#   assinatura|<base64 RSA-SHA256 de TUDO acima (cada linha + \n)>
#
# Assinatura: RSA PKCS#1 v1.5 SHA-256 = `openssl dgst -sha256 -sign` — o mesmo
# que RSACryptoServiceProvider.VerifyData(bytes, "SHA256", sig) do launcher.
# A chave privada RSA do canal fica com quem publica (OpenBao depois); NUNCA no git.
#
# Uso:
#   gerar-atualizacao.sh --check <pasta-release> <chave-rsa.pem> [nota]
#   gerar-atualizacao.sh <pasta-release> <chave-rsa.pem> [nota] [--versao x.y.z.w --launcher arquivo]
#
# Exit codes: 0 ok | 2 pré-requisito | 4 falha de assinatura
set -euo pipefail

log() { echo "[$(date '+%F %T')] $*"; }
req_pre() { log "ERRO (pré-requisito): $*"; exit 2; }

CHECK=0
ARGS=()
while [[ $# -gt 0 ]]; do
  case "$1" in
    --check) CHECK=1 ;;
    --versao) VERSAO=$2; shift ;;
    --launcher) LAUNCHER=$2; shift ;;
    *) ARGS+=("$1") ;;
  esac
  shift
done
PASTA=${ARGS[0]:-}
CHAVE=${ARGS[1]:-}
NOTA=${ARGS[2]:-}

[[ -n $PASTA && -d $PASTA ]] || req_pre "pasta do release: $PASTA"
if [[ $CHECK -eq 0 ]]; then
  [[ -n $CHAVE && -r $CHAVE ]] || req_pre "chave RSA privada ausente: $CHAVE"
  command -v openssl >/dev/null || req_pre "openssl não encontrado"
fi

[[ -z ${LAUNCHER:-} ]] || [[ -f $PASTA/$LAUNCHER ]] || req_pre "--launcher: arquivo não está na pasta: $LAUNCHER"

# [2/4] linhas do manifesto (na ordem; só \n; hash minúsculo)
ARQS=$(find "$PASTA" -maxdepth 1 -type f ! -name 'atualizacao.txt' ! -name 'SHA256SUMS*' ! -name '*.sig' -printf '%f\n' | sort)
[[ -n $ARQS ]] || req_pre "pasta sem arquivos"

BODY=$(mktemp /tmp/atualizacao.XXXXXX)
trap 'rm -f "$BODY"' EXIT
{
  echo "WKUPD|1"
  [[ -n $NOTA ]] && echo "nota|$NOTA"
  if [[ -n ${LAUNCHER:-} ]]; then
    [[ -n ${VERSAO:-} ]] || req_pre "--launcher exige --versao"
    H=$(sha256sum "$PASTA/$LAUNCHER" | cut -d' ' -f1)
    echo "versao|$VERSAO"
    echo "launcher|$LAUNCHER|$H"
  fi
  while IFS= read -r f; do
    [[ $f == "${LAUNCHER:-}" ]] && continue
    H=$(sha256sum "$PASTA/$f" | cut -d' ' -f1)
    echo "arquivo|$f|$f|$H"
  done <<< "$ARQS"
} > "$BODY"

echo "PLANO:"
echo " [1/4] manifesto:"
sed 's/^/       | /' "$BODY"
echo " [2/4] assinar corpo (RSA-SHA256 PKCS#1) -> linha assinatura|"
echo " [3/4] gravar $PASTA/atualizacao.txt"
echo " [4/4] auto-verificação com openssl + conferência do formato (WKUPD|1)"
[[ $CHECK -eq 1 ]] && { echo "Check: nada foi escrito."; exit 0; }

log "[2/4] assinando..."
ASSINATURA=$(openssl dgst -sha256 -sign "$CHAVE" "$BODY" | base64 -w0)

log "[3/4] gravando..."
{
  cat "$BODY"
  echo "assinatura|$ASSINATURA"
} > "$PASTA/atualizacao.txt"

log "[4/4] auto-verificação..."
# reconstrói o corpo a partir do ARQUIVO final (tudo antes de assinatura|) e confere
head -n -1 "$PASTA/atualizacao.txt" > "$BODY.final"
tail -n 1 "$PASTA/atualizacao.txt" | grep -q '^assinatura|' || { log "ERRO: linha de assinatura ausente"; exit 4; }
diff -q "$BODY" <(sed -e '$d' "$PASTA/atualizacao.txt" 2>/dev/null || cat "$BODY.final") >/dev/null \
  || diff -q "$BODY" "$BODY.final" >/dev/null || { log "ERRO: corpo não bate"; exit 4; }
grep -q '^WKUPD|1$' "$PASTA/atualizacao.txt" || { log "ERRO: WKUPD|1 ausente"; exit 4; }
openssl dgst -sha256 -verify <(openssl pkey -in "$CHAVE" -pubout 2>/dev/null) \
  -signature <(tail -n 1 "$PASTA/atualizacao.txt" | sed 's/^assinatura|//' | base64 -d) \
  "$BODY.final" >/dev/null || { log "ERRO: assinatura não confere"; exit 4; }

log "OK — atualizacao.txt pronto em $PASTA (anexe ao release, junto dos arquivos)"
