#!/usr/bin/env bash
# gerar-chaves.sh — gera o par Ed25519 do canal de atualização do launcher.
#
# A chave PRIVADA é de quem publica releases: fica FORA do git (este script
# recusa sobrescrever) e deve morar no cofre/máquina do mantenedor. A pública
# vai para o release e é embutida no launcher (formato cru de 32 bytes
# = chaves/teste/chave-publica-teste.der de referência).
#
# Uso:
#   gerar-chaves.sh --check [pasta]    # read-only: plano + o que já existe
#   gerar-chaves.sh [pasta]            # gera chave-privada.pem, chave-publica.pem e chave-publica.der
#
# Pré-requisito: openssl 3.x (EC P-256 com -rawin).
# Exit codes: 0 ok | 2 pré-requisito ausente | 3 já existe (não sobrescreve)
set -euo pipefail

CHECK=0
PASTA=.
if [[ ${1:-} == "--check" ]]; then CHECK=1; PASTA=${2:-.}; else PASTA=${1:-.}; fi

log() { echo "[$(date '+%F %T')] $*"; }
req_pre() { log "ERRO (pré-requisito): $*"; exit 2; }

command -v openssl >/dev/null || req_pre "openssl não encontrado no PATH"
openssl version | grep -qE 'OpenSSL 3\.' || req_pre "openssl 3.x necessário (EC P-256 -rawin): $(openssl version)"

PRIV="$PASTA/chave-privada.pem"
PUB="$PASTA/chave-publica.pem"
DER="$PASTA/chave-publica.der"

echo "PLANO:"
echo " [1/4] validar destino: $PASTA (nada sobrescrito)"
echo " [2/4] gerar chave privada EC P-256 -> $PRIV (600, FORA do git)"
echo " [3/4] derivar pública -> $PUB (PEM) e $DER (DER SPKI, para embutir no launcher)"
echo " [4/4] auto-teste: assinar e verificar com o openssl"

if [[ $CHECK -eq 1 ]]; then
  for f in "$PRIV" "$PUB" "$DER"; do
    [[ -e $f ]] && echo "  existe: $f ($(stat -c%A "$f"))" || echo "  ausente: $f"
  done
  echo "Check: nada foi alterado."
  exit 0
fi

for f in "$PRIV" "$PUB" "$DER"; do
  [[ -e $f ]] && { log "ERRO: $f já existe — não sobrescrevo (guarde/mova antes)."; exit 3; }
done

mkdir -p "$PASTA"
log "[2/4] gerando chave privada..."
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out "$PRIV"
chmod 600 "$PRIV"
log "[3/4] derivando pública..."
openssl pkey -in "$PRIV" -pubout -out "$PUB"
openssl pkey -pubin -in "$PUB" -outform DER -out "$DER"
[[ -s "$DER" ]] || { log "ERRO: DER da pública vazio"; exit 2; }
log "[4/4] auto-teste..."
echo "teste-de-assinatura" > "$PASTA/.autoteste"
openssl dgst -sha256 -sign "$PRIV" -out "$PASTA/.autoteste.sig" "$PASTA/.autoteste"
openssl dgst -sha256 -verify "$PUB" -signature "$PASTA/.autoteste.sig" "$PASTA/.autoteste" >/dev/null
rm -f "$PASTA/.autoteste" "$PASTA/.autoteste.sig"
log "OK — privada em $PRIV (600). PUBLIQUE: $PUB e $RAW junto do release; EMBUTA o .der no launcher."
