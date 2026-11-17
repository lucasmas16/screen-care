# 0002 — Limitar a cortina e devolver as telas ao usuário

Data: 2026-11-17. Estado: implementada na 1.5.9.

## Contexto

O registro local confirmou um período longo de reacendimentos e mudanças de monitores sem entrada reconhecida. A UI continuava processando mensagens. O código permitia retentativas ilimitadas e a cortina passiva não recebia teclado por foco. O motivo da ausência de Raw Input não foi determinado. Corrigir a recuperação não depende de atribuir a falha a USB, driver ou dispositivo virtual.

## Decisão

- Permitir no máximo três retentativas por ação de inatividade, com os intervalos progressivos existentes. Após a terceira, aguardar a janela de confirmação antes de desistir se a tela continuar ligada ou não houver confirmação.
- Limitar a ação a 120 segundos desde o primeiro aviso inesperado de tela ligada, mesmo que depois apareça um aviso de tela desligada. Sem esse limite, um aviso desatualizado ainda poderia deixar a cortina indefinidamente. Desligamento normal confirmado, sem reacendimento, continua sem limite de duração.
- Ao desistir, remover a cortina, desarmar a recuperação, cancelar o desligamento manual, pedir tela ligada e liberar os pedidos de manter tela/sistema acordados. Não reativar automaticamente a ação enquanto não houver nova atividade ou comando explícito. O timeout do Windows continua livre para desligar a tela.
- Instalar `WH_KEYBOARD_LL` e `WH_MOUSE_LL` apenas durante a cortina. Encaminhar todos os eventos com `CallNextHookEx`; adiar o encerramento para a fila da UI, sem escrita em disco nos callbacks. Qualquer tecla ou movimento de quatro pixels encerra a cortina. Uma mensagem local de mouse também pode encerrá-la.
- Consultar `GetLastInputInfo` na sessão desbloqueada somente enquanto a cortina existir. Comparar com a amostra obtida ao criar a cortina, não com a ativação do desligamento: o próprio sinal de presença que causou o reacendimento não deve desfazer imediatamente a proteção. Entradas posteriores têm prioridade sobre evitar reacendimentos de um controle parado. Não é possível distinguir a origem pelo timestamp.
- Com `IgnoreSimulatedInput=true`, desabilitar o fallback de timestamp e as mensagens locais de ponteiro, cuja origem é ambígua; os hooks ignoram eventos marcados como injetados. Entrada física nos hooks, o atalho e o limite de tempo continuam disponíveis.
- O atalho Ctrl+Alt+F12 remove apresentações e cancela pedidos antes de tentar salvar configurações. Um erro de persistência não pode impedir a saída.
- Registrar as quatro usages no `OnHandleCreated`, liberar recursos no `OnHandleDestroyed` e conferir o registro a cada minuto com ação ativa, ao reacender e em mudanças de sessão, resume e monitores. A consulta confirma HWND e `INPUTSINK`; neste Windows ela omite `DEVNOTIFY`, embora o registro o solicite.
- Registrar contagens agregadas em cada aviso de tela ligada e resumos do ciclo a cada 30 segundos. Não suprimir os avisos de energia.

## Auxiliar de controles

As estruturas nativas correspondem a `JOYINFOEX` (52 bytes), `JOYCAPSW` (728 bytes) e `XINPUT_STATE` (16 bytes). Não foi encontrado erro de tamanho nas assinaturas revisadas. A corrupção de heap observada não prova qual componente a provocou; o relatório WER disponível não contém um dump para identificar a pilha.

Após `0xC0000374`, desabilitar WinMM no próximo auxiliar e preservar XInput e os controles atendidos pelo Raw Input. Se o auxiliar sem WinMM também sofrer corrupção de heap, interrompê-lo até reiniciar o app. Isso contém o ciclo de falhas; não é uma correção comprovada do driver. Controles somente WinMM deixam de contar como atividade nesse modo. A coleta de dump é opcional e local.

## Consequências e limites

A tela pode voltar a mostrar o aplicativo anterior após o prazo, mesmo sem ninguém presente. Preferimos esse resultado a uma cortina sem saída. O app não substitui a tela de bloqueio. Os hooks podem ser removidos pelo Windows se não responderem a tempo; por isso não substituem o fallback e o limite da máquina de estados. Uma falha completa do Windows ou da thread de UI fica fora dessa garantia.

Referências: [registro Raw Input](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getregisteredrawinputdevices), [última entrada da sessão](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getlastinputinfo), [callback de teclado](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc), [callback de mouse](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc).
