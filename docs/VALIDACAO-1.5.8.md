# Validação 1.5.8

Revisão pedida após as correções de Switch Pro (1.5.6) e DualSense (1.5.7): o que ainda pode impedir o desligamento dos monitores ou encerrar o programa.

## Evidências do equipamento

- Windows Error Reporting: 11 encerramentos, todos `0xc0000374` (corrupção de heap), o último em 06/11 com a 1.5.5. Nenhum desde a 1.5.6. A instância 1.5.7 analisada rodava havia 47 h com o mesmo auxiliar.
- Auxiliar 1.5.7 instalado: 1.474.862 handles do tipo Key após 47 h; 9,66 por segundo. Experimento isolado: 300 consultas a slots vazios, +400 handles; 300 consultas a um slot presente, +0; 2.000 chamadas `XInputGetState`, +0; 5.000 chamadas `GetRawInputDeviceList`, +16 (uma vez).
- Auxiliar 1.5.8, mesmo equipamento e período: 404 → 404 handles em 60 s, com 657 relatórios e nenhum ativo.
- Movimento de mouse injetado por `mouse_event` chegou ao Raw Input sem dispositivo e contou como atividade.
- Em dois dias, as telas reacenderam 11 vezes sem nenhuma entrada detectada antes (uma delas seguida de uma tecla meio segundo depois, outra logo após um desligamento manual); o app pediu o desligamento de novo em poucos segundos. A causa dos demais reacendimentos não foi identificada.

## Testes automatizados

1.651 verificações aprovadas. As novas simulam, contra as classes entregues:

- analógico parado além da zona morta, com ruído em volta do limite, com um pico a cada 90 s e ligando por 2 s a cada 30 s: todos deixam o descanso acontecer em até 5 minutos mais o tempo configurado (antes, os três últimos nunca deixavam);
- lixo que muda a cada leitura em um controle de outro fabricante: ignorado após dois minutos, e o controle volta a contar quando o lixo para;
- uso real que precisa continuar contando: acelerador mantido com direção alternada, a mesma direção repetida com outro comando mantido, um botão por minuto e botões rápidos;
- consulta de slots vazios só na partida, após mudança de dispositivos e a cada 5 minutos; substituição imediata do auxiliar que pede reciclagem; intervalos crescentes de reinício;
- slideshow com pasta inexistente cai para o desligamento protegido, libera o pedido de manter a tela acesa e avisa na volta;
- retomada automática após erro, cobertura refeita quando os monitores mudam, rotação do log preservando as linhas mais recentes, estado de bloqueio na partida e a opção `IgnoreSimulatedInput`.

Testes de segurança e verificação de privacidade aprovados.

## Não verificado fisicamente

- Controle conectado depois da partida do auxiliar: a detecção depende da mudança na lista de dispositivos, coberta por teste, mas não foi repetida com um controle real.
- Leitura direta do DualSense com a 1.5.8: o controle estava desligado durante a validação; a lógica de relatórios é coberta pelos testes.
- Partida com a sessão já bloqueada e mudança física de monitores com a cobertura visível.
- Comportamento do auxiliar antigo ao atingir o limite de handles do Windows.

## Limites

Entrada gerada por software continua contando por padrão. O filtro de controles troca falhas silenciosas por um caso visível: usar só um analógico, sem nenhum botão, por mais de 5 minutos conta como inatividade. Na tela de bloqueio, um driver que sinaliza presença continua mantendo a tela acesa (ver 1.5.7). Travamentos nativos em outros componentes e encerramentos externos continuam possíveis.
