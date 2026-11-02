# Screen Care

Utilitário de bandeja para Windows que acompanha a inatividade e desliga monitores ou inicia um slideshow local.

Projeto de código aberto para Windows, distribuído sob a licença MIT.

## Download

Baixe o [instalador ou a versão portátil na página de releases](https://github.com/lucasmas16/screen-care/releases/latest). Os pacotes incluem a licença e o manual; a lista `SHA256SUMS.txt` permite conferir a integridade dos arquivos. O aplicativo ainda não possui assinatura digital de editor.

## Novidades da versão 1.4.1

- Novo nome: **Screen Care**, com executáveis e atalhos atualizados. As configurações e a identidade interna da instalação anterior são mantidas para permitir atualização.
- Proteção adicional para carregamento de DLLs do Windows, destinos do instalador e configurações XML inválidas.

- Corrigido o ciclo de desligar/reacender causado por mudanças de conexão das telas durante o descanso. Um único pedido de desligamento é enviado por período de inatividade.
- Atividade do DualSense (Bluetooth simples/aprimorado e USB), controles XInput e joysticks WinMM reinicia a contagem, incluindo comandos mantidos pressionados e filtro para pequenas oscilações dos analógicos.
- Alerta visual no centro inferior do monitor principal, com desenho de monitor e símbolos de ativado/pausado. Novos alertas substituem imediatamente o anterior e reiniciam sua duração, sem roubar foco.

## Funcionalidades

- Modo desligar monitores ou apresentar imagens.
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

Resultado e limites da revisão: [segurança da versão 1.4.1](docs/REVISAO-SEGURANCA-1.4.1.md).

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
