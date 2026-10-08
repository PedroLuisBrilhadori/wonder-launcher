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
