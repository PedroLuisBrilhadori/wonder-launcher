# Assinatura Authenticode sem gastar muito

VT do zip v3.5.2 (2026-10-08): **4/75** (Elastic moderate, Ikarus genérico,
Google, MaxSecure `susgen`) — mesmo perfil de ML/heurística do exe cru (6/75),
nenhuma família real. O que muda esse jogo é assinatura **Authenticode**
(embutida no binário), não o zip nem o SHA256SUMS.

## O que assinar

Só os binários **nossos** de cada release: `WonderLauncher.exe`, `dinput8.dll`,
`wkhdmod.dll` (a blocklist de conformidade garante que jogo não entra; assinar
só o nosso). Comando (quando houver certificado):

```
signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com \
  /n "Nome no certificado" arquivo.exe
```

## Caminhos (do mais barato pro mais caro)

| Caminho | Custo | Requisitos | Observação |
|---|---|---|---|
| **SignPath Foundation** | **grátis** | projeto **open source** (repo público + licença OSS, ex.: MIT) e aprovação da fundação | certificado OV de código + integração **GitHub Action** (assina no release sozinho). Aplicação: signpath.org/foundation. O repo já é público; falta escolher a licença |
| **Certum Open Source** | ~€70–90/ano (confirmar) | projeto open source | cert + assinatura em nuvem (Simplysign) — cumpre a regra de hardware/HSM dos certificados desde 2023 |
| **Certum/A SSL.com padrão (OV)** | ~US$100–250/ano | identidade validada | sem exigência de código aberto — se o launcher for ficar proprietário fechado |
| **Azure Trusted Signing** | US$9,99/mês | pessoa jurídica OU dev individual com 3+ anos de histórico verificável | melhor custo/benefício técnico; provavelmente não elegíveis ainda (projeto novo) |
| **EV** | US$300–500+/ano | empresa | reputação SmartScreen instantânea — fora do orçamento atual |

## Recomendação

1. **Agora (grátis, sem certificado):** nada muda nos binários — o que já
   fizemos (repo público, release limpa, hash na página, LEIA-ME explicando o
   aviso do Windows) é o máximo sem assinar.
2. **Curto prazo:** decidir licença (MIT?) e aplicar no **SignPath Foundation**
   — de graça, automático via GitHub Actions, resolve a maior parte dos
   alertas de ML.
3. **Se quiserem manter o código fechado:** Certum padrão (~US$100+/ano) é o
   mínimo viável; EV só com empresa.

Realismo: mesmo assinado (OV), o SmartScreen mantém reputação por volume de
downloads — melhora bastante, mas os primeiros dias ainda podem mostrar aviso.
EV resolveria na hora, mas é o caro.

## Checklist quando o certificado chegar

- [ ] Assinar os 3 binários ANTES de montar o zip (hash do release muda —
      atualizar o SHA-256 das notes)
- [ ] Revalidar no VT (subir o zip assinado) e registrar o delta
- [ ] Integrar no fluxo: docs/release.md §Release player-facing

## Azure Trusted Signing (renomeado Azure Artifact Signing) — individual

**US$ 9,99/mês** (Basic: 5.000 assinaturas/mês; Premium US$ 99,99; excedente
US$ 0,005/assinatura). Individual aberto desde 11/2024 (preview público).

Como funciona: você **não tem certificado** — valida sua identidade uma vez
(documento + comprovante fiscal + ~3 anos de experiência profissional
verificável; GitHub/LinkedIn ajudam) e o serviço assina por API com cadeia
**gerenciada pela Microsoft**, mostrando seu nome validado como publisher.
Integrações: cliente oficial + `signtool`, e **GitHub Action** (sign no
release pipeline). Timestamp incluído. Revalidação periódica da identidade é
exigida (aviso de renovação desde 10/2025).

Riscos/fricção conhecidos:
- lista de países suportados no formulário individual é LIMITADA (relatos de
  devs da UE fora da lista); Brasil aparece como suportado em relatos, mas
  confirmar no form. Criar conta + submeter validação **não custa nada** — dá
  pra checar elegibilidade antes de pagar.
- abuse de 2025 (malware assinado) apertou a vetting — aprovação pode demorar.
- assinar em lote e cancelar a assinatura esbarra na revalidação — tratar como
  custo recorrente enquanto em uso.

**A conta que decide** (Azure é mês a mês, sem fidelidade — mas a validação de
identidade antecede o uso e a revalidação periódica pune o ciclo
assina-1-mês-cancela): contínuo = US$ 120/ano vs Certum ~€70–90; empate em
~8 meses de uso/ano.

**Ordem recomendada pra este projeto** (projeto ATIVO, release todo mês):
(1) **Certum Open Source** (~€70–90/ano — a MIT no repo já satisfaz o
requisito; compra determinística, assina quando quiser sem dança mensal);
(2) **SignPath Foundation aplicado em paralelo** — quando aprovar, vira grátis
e o Certum morre no vencimento; (3) **Azure só se o uso virar esporádico**
(2–3 surtos de release/ano: ~US$ 20–30 ganha de todo mundo).

## Ponte de 2 meses no Azure + escopo "amplo" (fazaboa)

**A ponte funciona por causa do timestamp**: assinatura com carimbo de tempo é
válida PARA SEMPRE, mesmo após cancelar a assinatura/certificado. Dois meses de
Basic (~US$ 20) assinando cada release da janela = builds assinadas
permanentemente; só builds NOVAS após o cancelamento ficam sem assinatura.
Começar a validação de identidade (grátis) já, em paralelo ao pedido do
SignPath, maximiza a janela útil.

**Certificado "amplo" — um pra todas as apps da fazaboa:** certificado de
código valida uma IDENTIDADE, não um app — um certificado só assina binários
ilimitados de quem o detém. Dois regimes:

| Regime | Como | Escopo |
|---|---|---|
| **Pessoa física** (Azure individual / Certum OS) | valida o dev (docs + ~3 anos experiência) | assina TUDO que ELE buildar — launcher, voicecast-Windows, ferramentas; publisher aparece o nome do dev |
| **Pessoa jurídica** (org OV) | exige empresa registrada com **3+ anos** (critério Azure; CAs tradicionais verificam empresa) | publisher "fazaboa", cobre qualquer app da empresa — o "amplo" de verdade |

Se a fazaboa tiver CNPJ com 3+ anos, o org OV é a resposta ampla (Azure org
US$ 9,99/mês ou CA tradicional US$ 200–400/ano). Se a empresa é mais nova:
certificado pessoal agora, org quando a empresa completar idade.

**Limites do "um certificado pra tudo"**: Authenticode cobre **binários
Windows** (launcher WkEmu, voicecast-Windows, tools). Fora dele: extensão de
browser é assinada pela loja (CWS/AMO), app macOS exige Apple Developer
(US$ 99/ano) — certificados separados por ecossistema.

**SignPath é por PROJETO, não por org**: cada projeto open source aplica o
seu (wonder-launcher hoje; outro repo OSS = outro pedido).
