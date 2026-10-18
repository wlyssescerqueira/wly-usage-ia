# WLY Usage

Protótipo local para Windows, feito em C# / WPF (.NET 9).

## Experimentar

Use o atalho `Abrir WLY Usage.lnk` na raiz, que aponta para `%LOCALAPPDATA%/WlyUsage/app/WlyUsage.exe`. A instalação inicia automaticamente ao entrar no Windows. Esta é a versão horizontal sem cabeçalho (680 × aproximadamente 76 pixels), com leitura de dados locais reais a cada cinco segundos.

- Ao aproximar o mouse, o modo **posicionamento inteligente (experimental)** procura uma região visualmente uniforme no mesmo monitor, preferindo as bordas. Analisa uma captura em memória somente ao precisar desviar: sem IA, sem rede e sem salvar imagens. Usa densidade de contornos; não entende o conteúdo nem sabe onde você vai ler. Pode ser desligado no menu.
- Se não encontrar espaço adequado ou a captura falhar, alterna para a borda oposta: topo ↔ rodapé, direita ↔ esquerda. Alternativas: topo, direita, esquerda e rodapé. Segure **Ctrl** para ela ficar parada e permitir consulta, cliques e arraste.
- Quando não há agentes trabalhando, após quatro segundos fora dela a faixa esmaece. Nesse estado, os cliques atravessam a janela; aproximar o mouse restaura os controles.
- Segure **Ctrl** e arraste pelos ícones ou nomes das IAs para mudar de posição. A posição é salva localmente.
- Fixar e ocultar aparecem na lateral direita ao passar o mouse; o pin permanece visível quando fixado. O espaço reservado mantém as barras estáveis.
- Use `Fixar` para manter os detalhes abertos.
- Use `Ocultar` para sumir até clicar no ícone da bandeja. Novas atividades não reabrem o painel.
- O modo apresentação, no menu e na bandeja, mantém o painel oculto inclusive ao reiniciar. Clique na bandeja para voltar e encerrar esse modo.
- A exclusão de captura do Windows é solicitada automaticamente. O tooltip da bandeja informa se a chamada foi aceita. A compatibilidade precisa ser conferida no compartilhamento real de Teams, Meet e Zoom; isso não detecta chamadas nem o começo de um compartilhamento.
- Segure **Ctrl** e use o botão direito para escolher posição, monitor ou desativar o desvio do mouse. Fixar mantém os detalhes abertos; o desvio continua ativo até ser desativado ou você segurar Ctrl.
- A bandeja permite reabrir o painel, atualizar os dados ou sair.
- As cotas do Codex vêm dos eventos `rate_limits` das sessões em `CODEX_HOME` ou `~/.codex/sessions`. As do Claude vêm da coleta local já configurada em `~/.habblaud/usage/.claude.json`; o Habblaud não precisa estar aberto.
- Coletas com mais de 30 minutos são marcadas como desatualizadas. Janelas expiradas ou sem coleta aparecem com traço, nunca com um percentual inventado. Passe o mouse no bloco para consultar fonte e horário.
- A atividade é inferida dos eventos recentes das sessões locais, com limite de três minutos sem eventos. Não equivale à detecção exata de todos os agentes, contas ou sessões em WSL.

Preferências: `%LOCALAPPDATA%/WlyUsage/preferences.json`.

## Desenvolvimento

```powershell
dotnet build
dotnet run
dotnet format whitespace --verify-no-changes --no-restore
dotnet publish -c Release -r win-x64 --self-contained true -o artifacts/app
./scripts/Publish.ps1
```

`--preview` renderiza a janela expandida em `artifacts/preview.png` e encerra.
`--privacy-check` verifica ocultação após atividade, modo apresentação, restauração e o sinalizador de exclusão de captura, gerando `artifacts/privacy-check.json`.
`--live-check` verifica as fontes locais e gera `artifacts/live-check.json`, sem conteúdo das conversas nem credenciais.
`--live-preview` renderiza uma leitura real em `artifacts/preview-live.png`.
`--dodge-check` verifica posições livres, limites do monitor, coordenadas negativas e tela menor que o painel.
`--smart-check` verifica regiões uniformes e densas e executa uma captura real de teste, gravando apenas um resultado numérico em `artifacts/smart-check.json` (nenhuma imagem).

`scripts/Publish.ps1` publica, atualiza a instalação do usuário, cria o atalho na pasta Inicializar e reinicia o app. Para desativar o startup, remova `WLY Usage.lnk` da pasta aberta por `shell:startup`.

## Escopo e validação

Não lê credenciais nem chama APIs dos provedores. Lê eventos e cotas das sessões locais, sem exportar conteúdo das conversas.
A interface mantém a estrutura WLYTECH com tema escuro solicitado pelo usuário: superfícies grafite, texto claro e acento lilás adaptado ao contraste. Segoe UI é uma adaptação temporária à fonte disponível no Windows.
As barras segmentadas seguem a referência enviada, com verde, âmbar e rosa conforme o consumo (exceção de cor solicitada). Os percentuais continuam explícitos.
Ícones vetoriais incorporados ao executável: Claude via [Simple Icons](https://github.com/simple-icons/simple-icons/blob/develop/icons/claude.svg) e OpenAI, identificando Codex, via [Lobe Icons](https://github.com/lobehub/lobe-icons/blob/master/packages/static-svg/icons/openai.svg). As marcas pertencem aos respectivos titulares.
Build e renderização estática verificados. Hover, passagem de cliques, bandeja e monitores com escalas diferentes precisam de validação interativa no desktop.
