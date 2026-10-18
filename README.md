# WLY Usage

Protótipo local para Windows, feito em C# / WPF (.NET 9).

## Experimentar

Use o atalho `Abrir WLY Usage.lnk` na raiz, que aponta para `artifacts/app/WlyUsage.exe`. Esta é a versão horizontal sem cabeçalho (680 × aproximadamente 76 pixels). Os dados de Claude e Codex são simulados.

- Passe o mouse sobre a barra para abrir os detalhes.
- Quando não há agentes trabalhando, após quatro segundos fora dela a faixa esmaece. Nesse estado, os cliques atravessam a janela; aproximar o mouse restaura os controles.
- Arraste pelos ícones ou nomes das IAs para mudar de posição. A posição é salva localmente.
- Fixar e ocultar aparecem na lateral direita ao passar o mouse; o pin permanece visível quando fixado. O espaço reservado mantém as barras estáveis.
- Use `Fixar` para manter os detalhes abertos.
- Use `Ocultar` para sumir até clicar no ícone da bandeja. Novas atividades não reabrem o painel.
- O modo apresentação, no menu e na bandeja, mantém o painel oculto inclusive ao reiniciar. Clique na bandeja para voltar e encerrar esse modo.
- A exclusão de captura do Windows é solicitada automaticamente. O tooltip da bandeja informa se a chamada foi aceita. A compatibilidade precisa ser conferida no compartilhamento real de Teams, Meet e Zoom; isso não detecta chamadas nem o começo de um compartilhamento.
- Use o botão direito para escolher topo esquerdo, centro, direito ou outro monitor.
- A bandeja permite reabrir o painel, simular atividade ou sair.
- A demonstração muda de estado a cada 18 segundos; pode ser pausada no menu.

Preferências: `%LOCALAPPDATA%/WlyUsage/preferences.json`.

## Desenvolvimento

```powershell
dotnet build
dotnet run
dotnet format whitespace --verify-no-changes --no-restore
dotnet publish -c Release -r win-x64 --self-contained true -o artifacts/app
```

`--preview` renderiza a janela expandida em `artifacts/preview.png` e encerra.
`--privacy-check` verifica ocultação após atividade, modo apresentação, restauração e o sinalizador de exclusão de captura, gerando `artifacts/privacy-check.json`.

## Escopo e validação

Não lê credenciais, cotas ou sessões reais. Não configura início automático.
A interface mantém a estrutura WLYTECH com tema escuro solicitado pelo usuário: superfícies grafite, texto claro e acento lilás adaptado ao contraste. Segoe UI é uma adaptação temporária à fonte disponível no Windows.
As barras segmentadas seguem a referência enviada, com verde, âmbar e rosa conforme o consumo (exceção de cor solicitada). Os percentuais continuam explícitos.
Ícones vetoriais incorporados ao executável: Claude via [Simple Icons](https://github.com/simple-icons/simple-icons/blob/develop/icons/claude.svg) e OpenAI, identificando Codex, via [Lobe Icons](https://github.com/lobehub/lobe-icons/blob/master/packages/static-svg/icons/openai.svg). As marcas pertencem aos respectivos titulares.
Build e renderização estática verificados. Hover, passagem de cliques, bandeja e monitores com escalas diferentes precisam de validação interativa no desktop.
