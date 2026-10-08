# Build da fonte v35 (recuperada)

Alvo: **Windows** (o autor tem a fonte original e o ambiente — esta pasta é a
recuperada por ILSpy; confirme/substitua pelo original).

## Trocas já aplicadas aqui

- `Atualizador.cs`: chave pública RSA do canal **re-keyada** (a antiga estava
  perdida com o repo 404) — nos 2 lugares (const `ChavePublica` e o
  `FromXmlString` do `LerManifesto`). A privada nova fica com o dono do release
  (custódia planejada: OpenBao `wk/`).
- `WonderLauncher.csproj`: `EnableWindowsTargeting` + pacote de reference
  assemblies (tentativa de build no Linux).

## Erros pendentes (artefatos de decompilação — build no Linux)

O `dotnet build` no Linux para em erros DECOMPILADOS (não existem na fonte
original do autor):

1. `MainForm.cs` — chamadas a membros protegidos com qualificador base
   (`Control.SetStyle`, `OnMouseMove`, `OnMouseDown`, `OnMouseUp`, `OnKeyDown`,
   `OnResizeEnd`, `OnMouseLeave`): remover o qualificador (chamada direta, é a
   própria classe) — CS1540, ~8 ocorrências.
2. `MainForm.cs(1810)` — `Dispose(true)` chamado com qualificador: usar
   `Dispose(bool)` override padrão do designer — CS1501.
3. `Atualizador.cs(65)` — `SecurityProtocolType.Tls11/Tls12` não existem no
   targeting pack do **net40**: ou subir o alvo para net48 (recomendado — o
   runtime 4.x do Windows atende) ou usar os valores numéricos
   `(SecurityProtocolType)768 | (SecurityProtocolType)3072`.
