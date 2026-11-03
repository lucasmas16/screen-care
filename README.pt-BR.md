# Screen Care

[English](README.md) | **Português (Brasil)**

O Screen Care foi desenvolvido para ajudar a proteger monitores contra burn-in (marcas permanentes), retenção de imagem e outros efeitos da exposição prolongada a uma imagem parada. Ele reduz o tempo de tela estática desligando os monitores ou apresentando imagens locais após um período sem atividade real do usuário.

O aplicativo envia seu próprio pedido de desligamento, independentemente do tempo de espera do plano de energia. Isso permite agir mesmo quando um aplicativo em segundo plano mantém a tela ligada e o desligamento automático normal do Windows não acontece. Se a tela reacender sem atividade, o Screen Care tenta desligá-la novamente com intervalos progressivos. Entrada do usuário, suspensão e mudanças de sessão interrompem as tentativas. O comportamento físico depende do Windows, dos drivers e dos monitores: não há garantia de impedir todo reacendimento nem de evitar todo burn-in.

Projeto de código aberto para Windows, distribuído sob a licença MIT.

## Download

Baixe o [instalador ou a versão portátil na página de releases](https://github.com/lucasmas16/screen-care/releases/latest). Os pacotes incluem a licença e o manual; a lista `SHA256SUMS.txt` permite conferir a integridade dos arquivos. O aplicativo ainda não possui assinatura digital de editor.

## Novidades da versão 1.5.4

A legenda abaixo de “Iniciar com o Windows” agora ajusta sua altura ao texto, evitando cortes. O painel pode crescer e continua acessível por rolagem em janelas menores. Verificado nos dois idiomas, temas e com texto ampliado. Consulte a [validação da 1.5.4](docs/VALIDACAO-1.5.4.md).

## Histórico da versão 1.5.3

Menu simplificado: removido “Iniciar agora”. A ação manual permanece em “Desligar telas em 2 segundos”, com o atalho Ctrl+Alt+F11.

Inclui as melhorias de alerta e recuperação descritas abaixo. Consulte a [validação e os limites da 1.5.3](docs/VALIDACAO-1.5.3.md).

## Histórico da versão 1.5.2

- O alerta de ativado/desativado reforça sua posição sobre outras janelas durante os três segundos de exibição, sem ativar a janela, capturar cliques ou exigir novo clique para retornar ao aplicativo. Validado com uma janela sem bordas em tela cheia em outro processo.
- Tela cheia exclusiva real pode impedir sobreposições de janelas comuns. Para jogos nesse modo, use tela cheia sem bordas se precisar do alerta. Não há injeção no jogo nem mudança automática de suas configurações.
- Erros operacionais repetidos passam a pausar a automação mantendo o programa aberto. Falhas ao criar avisos são isoladas; a recuperação remove janelas com falha antes de atualizar o ícone. Falhas fatais do processo e encerramentos pelo Windows continuam possíveis.
- [Validação e revisão de encerramentos da 1.5.2](docs/VALIDACAO-1.5.2.md).

## Histórico da versão 1.5.1

O desligamento manual por Ctrl+Alt+F11 mantém somente o computador acordado para permitir reprodução em segundo plano, até uma entrada ou cancelamento. O desligamento automático libera o pedido de manter o sistema acordado. Suspensão manual e ações críticas do Windows continuam tendo prioridade.

- Interface, instalador, menu e avisos em inglês e português. O seletor de idioma aplica e salva a escolha imediatamente, preservando opções em edição. O primeiro uso acompanha o idioma do Windows: português em sistemas lusófonos, inglês nos demais.
- **Ctrl+Alt+F11** desliga as telas após dois segundos, inclusive com o slideshow selecionado ou a automação pausada. A ação também está no menu do ícone. Ctrl+Alt+F12 continua ativando/pausando a automação e cancela uma contagem manual pendente.
- Recuperação do desligamento após reacendimento sem atividade: espera de três segundos para processar entrada pendente e tentativas com intervalos de 5, 15, 30 e até 60 segundos. A confirmação de tela desligada interrompe as tentativas.
- Erros recuperáveis da interface pausam a automação e mantêm o ícone disponível. Falhas são registradas antes da limpeza de recursos; eventos locais de execução ajudam a investigar saídas inesperadas.
- A causa exata do desaparecimento de ícone relatado não foi confirmada. Consulte a [investigação e validação](docs/VALIDACAO-1.5.1.md).

## Histórico da versão 1.4.1

- Novo nome: **Screen Care**, com executáveis e atalhos atualizados. As configurações e a identidade interna da instalação anterior são mantidas para permitir atualização.
- Proteção adicional para carregamento de DLLs do Windows, destinos do instalador e configurações XML inválidas.

- Corrigido o ciclo de desligar/reacender causado por mudanças de conexão das telas durante o descanso. A versão 1.5.1 também acompanha o estado das telas e repete o pedido quando necessário, com intervalos progressivos.
- Atividade do DualSense (Bluetooth simples/aprimorado e USB), controles XInput e joysticks WinMM reinicia a contagem, incluindo comandos mantidos pressionados e filtro para pequenas oscilações dos analógicos.
- Alerta visual no centro inferior do monitor principal, com desenho de monitor e símbolos de ativado/pausado. Novos alertas substituem imediatamente o anterior e reiniciam sua duração, sem roubar foco.

## Funcionalidades

- Modo desligar monitores ou apresentar imagens.
- Atalho global `Ctrl+Alt+F11` para desligar as telas em dois segundos.
- Atalho global `Ctrl+Alt+F12` para ativar ou desativar o monitoramento, com confirmação visual.
- Tratamento de bloqueio, desbloqueio, suspensão e mudança de monitores.
- Configurações persistentes, temas e instalador por usuário.

## Tecnologias

C#, Windows Forms, .NET Framework 4.8, Raw Input e APIs de energia/sessão.

## Executar

Requer Windows 10/11 x64 e .NET Framework 4.8. Não depende de NuGet.

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\dist\ScreenCare.exe
```

O instalador é gerado em `dist/ScreenCare-Setup.exe`. O parâmetro `-OutputDirectory` permite outra pasta de saída. Consulte [o manual](LEIA-ME.txt).

## Verificar

`powershell -ExecutionPolicy Bypass -File .\build.ps1 -RunTests`. A suíte verifica máquina de estados, persistência, imagens e renderização. O comportamento de energia em drivers, docks e telas deve ser validado manualmente.

Antes de preparar um commit: `node tools/check-privacy.cjs` (Node.js 24).

Depois de compilar com testes: `powershell -ExecutionPolicy Bypass -File .\tools\test-security.ps1`. Esse teste verifica redirecionamento de pastas e carregamento seguro de DLLs usando apenas arquivos sintéticos. Testes de interface exigem uma sessão interativa do Windows.

## Organização

- `src/IdleController.cs`: máquina de estados de inatividade.
- `src/Native.cs` e `src/SessionState.cs`: entrada, energia e sessão.
- `src/Images.cs` e `src/Slideshow.cs`: catálogo e apresentação.
- `src/Settings.cs`: persistência; `SettingsForm.cs`: edição.
- `src/TrayApplication.cs`: composição do aplicativo.
- `setup/Setup.cs`: instalador; `tests/Tests.cs`: verificações.

Detalhes: [arquitetura](docs/ARQUITETURA.md), [decisão inicial](docs/decisions/0001-organizacao.md), [privacidade](docs/PRIVACIDADE.md) e [contribuição](CONTRIBUTING.md).

Resultado e limites: [validação da versão 1.5.1](docs/VALIDACAO-1.5.1.md) e [revisão de segurança anterior](docs/REVISAO-SEGURANCA-1.4.1.md).

## Limitações

O Windows e os drivers podem reagir de maneiras distintas ao pedido de desligamento. Na sessão bloqueada, os dois modos apagam a tela. A distribuição local não inclui assinatura comercial nem atualização automática.

## Próximos passos

- Manter a máquina de estados independente da interface.
- Ampliar a matriz de validação manual para múltiplos monitores e suspensão.

## Licença

Licença [MIT](LICENSE). Permite usar, modificar e redistribuir, inclusive comercialmente, preservando o aviso de direitos autorais e a licença. Os componentes do Windows e do .NET mantêm seus próprios termos.

## Referências de organização

- [Referência para este tipo de aplicação](https://github.com/dotnet/samples/tree/main/windowsforms).
- [Modelos de exclusão do GitHub](https://github.com/github/gitignore).

As referências orientaram a organização; a estrutura descrita corresponde ao código existente.
