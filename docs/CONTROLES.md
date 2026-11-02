# Entrada de controles e energia — versão 1.4

O DualSense conectado por Bluetooth pode não aparecer corretamente nas APIs de joystick antigas. O app registra Raw Input para gamepads/joysticks em segundo plano e identifica Sony 054C:0CE6 (DualSense) e 054C:0DF2 (Edge). Lê os campos de controles dos relatórios USB, Bluetooth simples e Bluetooth aprimorado. Não abre o dispositivo de forma exclusiva, não envia comandos, não altera LEDs/vibração e não registra os comandos em arquivos. Contadores, giroscópio, acelerômetro e bateria são ignorados.

Formatos consultados: [driver Sony no Linux](https://github.com/torvalds/linux/blob/master/drivers/hid/hid-playstation.c) e [driver PS5 do SDL](https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps5.c). Implementação local de detecção de atividade, sem incorporar código desses drivers. Os relatórios são limitados por tamanho; buffers Raw Input são dimensionados dinamicamente. IDs de dispositivo ficam em memória e são invalidados quando a conexão muda.

XInput é consultado para os quatro slots; WinMM cobre joysticks legados, com leitura a cada 100 ms e redetecção a cada 2 segundos para slots desconectados. A calibração inicial dos eixos opcionais de dispositivos WinMM genéricos usa a posição ao conectar. Em aparelhos genéricos, deixe os eixos em repouso ao conectá-los. O DualSense direto usa centros/limiares do protocolo e independe dessa calibração.

Um jogo aberto, por si só, não bloqueia a automação: o uso dos comandos mantém a contagem reiniciada. Jogos em pausa e controles abandonados continuam sujeitos ao tempo configurado. Se um driver ocultar o HID (por exemplo, modo exclusivo de um remapeador), a saída XInput precisa estar disponível ao aplicativo.

A solicitação de manter as telas ligadas é liberada antes do desligamento. Alterações de topologia enquanto a ação de desligar está ativa não reiniciam o controlador: isso evita que a desconexão de uma tela em economia DisplayPort provoque ES_DISPLAY_REQUIRED e a reacenda. Não há repetição periódica do comando de desligamento. O comando enfileirado revalida a ação antes de executá-la. Entrada real, mudanças de sessão e comandos explícitos rearmam o período.

Referências das APIs: [XINPUT_GAMEPAD](https://learn.microsoft.com/en-us/windows/win32/api/xinput/ns-xinput-xinput_gamepad), [JOYINFOEX](https://learn.microsoft.com/en-us/windows/win32/api/joystickapi/ns-joystickapi-joyinfoex) e [SetThreadExecutionState](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate).

Os testes de regressão simulam inatividade e mudanças de topologia sem desligar telas. Os testes de alertas conferem posição, foco, substituição, duração e imagens renderizadas. A confirmação física do descanso depende do conjunto monitor/driver e deve ser feita com a versão instalada.
