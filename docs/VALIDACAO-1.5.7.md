# Validação 1.5.7

- Equipamento real: DualSense 054C:0CE6 por Bluetooth, dois monitores 2560×1440. Com o controle parado, o Screen Care não detectou atividade: 0/89 consultas XInput/WinMM ativas e 6.133 relatórios Raw Input, nenhum ativo.
- Mesmo assim, o Windows reacendia as telas cerca de 20 ms após cada pedido de desligamento. `GetLastInputInfo` mudava a cada consulta (~10 vezes por segundo) sem nenhum evento de teclado ou mouse, inclusive com o Screen Care encerrado e com o `GameInputRawInputProxy` encerrado. A origem está em um driver; o responsável não foi isolado porque isso exige desativar drivers como administrador. Detalhes em [CONTROLES.md](CONTROLES.md).
- Teste ao vivo com a compilação 1.5.7: desligamento pedido após 120 s, tela reacesa em 25 ms, `display-woke-without-input` registrado e duas janelas pretas cobrindo exatamente os dois monitores. A janela em primeiro plano não mudou; as novas tentativas de desligamento continuaram nos intervalos previstos.
- Comandos presos: XInput, WinMM, DualSense e Switch Pro calculam o conjunto de comandos acionados; o mesmo conjunto por 5 minutos deixa de contar. Testes cobrem limite, mudança, soltura, volta do contador de ticks e um controle travado que deixa de impedir o desligamento.
- Leitura WinMM duplicada do DualSense/Edge ignorada (Raw Input é a fonte): antes, 135/136 consultas legadas ativas com o controle parado; depois, 0/137.
- DDC/CI testado como alternativa para a tela de bloqueio e descartado: o monitor entrou em standby, desconectou-se do Windows em cerca de 1 s e só voltou pelo botão físico.
- 1.611 verificações automatizadas aprovadas. Instalação conferida por SHA-256 contra a compilação, com configurações preservadas.

Limites: a cobertura não aparece na tela de bloqueio (lá, o sinal do driver reinicia a contagem e a tela permanece acesa) nem sobre jogos em tela cheia exclusiva. Em LCD a luz de fundo continua acesa. Um comando realmente mantido sem nenhuma outra alteração por mais de 5 minutos passa a contar como inatividade.
