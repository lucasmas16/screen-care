# Validação 1.5.9

## Diagnóstico conferido

O log local confirma um desligamento prolongado seguido de minutos com avisos de tela ligada e mudanças de monitores, sem encerramento por entrada reconhecida. Os eventos do Windows confirmam corrupção de heap `0xC0000374` em processos do auxiliar e reinicialização sem desligamento normal. O código confirma as falhas de recuperação descritas na decisão [0002](decisions/0002-fail-safe-cortina.md).

A causa da falta de entrada não foi confirmada: perda de registro, filtragem de eventos e falha anterior à entrega pelo Windows permanecem hipóteses. Os relatórios WER consultados não conservam um dump. O código revisado não contém chamadas diretas a DirectInput: elas podem ocorrer dentro do caminho WinMM/driver. Os tamanhos das estruturas nativas foram conferidos contra a API do Windows e em teste.

## Verificações automatizadas

Compilação 1.5.9 concluída com avisos tratados como erros: **1.684 verificações aprovadas**. Testes de caminhos/atalhos de diretório e carregamento de DLLs aprovados. Verificação de privacidade: 56 arquivos, nenhum alerta. O script de dumps passou pela análise de sintaxe do PowerShell.

A suíte verifica a máquina de estados e os caminhos reais de recuperação:

- três retentativas no máximo, fim do ciclo sem confirmação e prazo máximo mesmo com aviso de tela desligada desatualizado;
- pausa até nova atividade, remoção da cortina e liberação dos pedidos de energia;
- timestamp da sessão, wrap do contador e ausência de amostra inicial;
- remoção deliberada de uma usage Raw Input, reparo das quatro usages e recriação da janela;
- instalação e remoção dos hooks, teclado sem foco, movimento de mouse sem clique, filtro de eventos injetados e limpeza adiada;
- mensagem local de movimento do mouse com limiar, independente de Raw Input e hooks;
- saída pelo fallback com Raw Input desabilitado e cancelamento pelo `WM_HOTKEY` de Ctrl+Alt+F12 quando salvar configurações falha;
- falha de heap simulada em processos filhos reais, passagem para modo restrito e interrupção após a segunda falha;
- regressões existentes de controles, sessões, slideshow, configurações, instalação e avisos.

Os testes bloqueiam o comando físico de desligamento nos cenários de falha e usam dados sintéticos. A preferência real do usuário não é salva pelo teste do atalho.

## Instalação local

Atualizada sobre a 1.5.8, com backup dos arquivos anteriores. Executável, manual, configuração do executável e desinstalador conferidos por SHA-256; XML de preferências idêntico ao anterior. A nova instância e o auxiliar iniciaram normalmente. O log registrou `start-1.5.9` e, no primeiro reacendimento de teste, `display-on` com contagens coerentes de duas entradas de mouse sem dispositivo, nenhuma ignorada e `registered=yes`. O teste enviou dois pequenos movimentos opostos, sem deslocamento final do ponteiro.

Os pacotes do instalador e portátil foram preparados com hashes para distribuição. O ciclo completo de interface do instalador não foi repetido: a instalação existente recebeu os arquivos verificados e a versão no cadastro de desinstalação foi atualizada.

## Matriz manual e limites

| Cenário | Estado |
|---|---|
| Perda de registro, handle recriado, falta de Raw Input, atalho com erro de gravação | Coberto automaticamente |
| Controle parado com sinal de presença que atualiza `GetLastInputInfo` | Pendente de reprodução física; fallback só observa entradas posteriores à criação da cortina |
| Noite inteira, pelo menos oito horas, no conjunto real de USB e controles | Pendente |
| Bloqueio automático durante a noite e retorno da sessão | Pendente de teste prolongado; estados básicos cobertos na suíte |
| Dois monitores físicos e monitor virtual com reconexões durante o descanso | Topologia sintética coberta; reprodução física prolongada pendente |
| Entrada simulada, opção de filtro e remoção dos hooks | Coberto automaticamente |
| Corrupção real do driver e análise da pilha | Pendente de dump; contenção coberta por processos sintéticos |

Para reprodução controlada, iniciar o app com `--test-ignore-raw-input --background`, após encerrar a instância normal. O log registra `test-rawinput-suppressed`. Esse modo desconsidera `WM_INPUT`, mas mantém hooks, fallback e atalho. Encerrar essa instância e iniciar normalmente ao terminar. Não usar esse argumento na inicialização automática.

O script `tools/configure-local-dumps.ps1` permite habilitar dumps locais apenas para `ScreenCare.exe`, com limite de três arquivos e restauração da configuração anterior por `-Restore`. Requer um PowerShell elevado. Não foi aplicado nesta sessão, que não possui privilégios administrativos. Dumps ficam no perfil local, podem conter dados de memória e não devem entrar no repositório.
