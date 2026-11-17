# Privacidade no desenvolvimento

Fotos selecionadas continuam nas pastas escolhidas pelo usuário. Preferências e caminhos de fotos são privados; nenhuma foto de uso pessoal acompanha este código.

O aplicativo não tem telemetria, cliente HTTP, servidor ou envio de teclas. Detecta atividade de entrada em memória. Configurações XML incluem o caminho da pasta de imagens e ficam no perfil local do Windows. O log opcional `last-error.txt` pode conter caminhos em mensagens de erro; não deve ser publicado sem revisão. Pastas de rede escolhidas pelo próprio usuário são acessadas pelo sistema de arquivos do Windows e podem provocar autenticação de rede do sistema operacional.

Desde a versão 1.5.0, `diagnostics.log` registra horários UTC e códigos fixos de início, desligamento, estado da tela e erro. A versão 1.5.1 identifica também a categoria de entrada que encerrou uma ação ativa (teclado, movimento/clique do mouse, controle ou sessão bloqueada) e mudanças nos pedidos de energia do próprio aplicativo. O arquivo é limitado a aproximadamente 64 KB; desde a 1.5.8, ao atingir o limite a metade mais recente é preservada. A 1.5.8 acrescenta as categorias `keyboard-no-device` e `mouse-no-device` (entrada sem dispositivo, gerada por software ou por alguns touchpads). Ele não registra cada entrada do usuário, teclas específicas, posições do ponteiro, nomes ou identificadores de dispositivos, nomes de imagens nem títulos de janelas e não é enviado automaticamente. Os registros ficam no perfil local e fora dos pacotes públicos.

Na 1.5.9, cada aviso de tela ligada registra a idade da última entrada da sessão, contagens agregadas de mensagens Raw Input (total, teclado, mouse, HID, sem dispositivo e ignoradas) desde o aviso anterior e a validade do registro. O log inclui contagens de retentativas, saída por proteção e código de término do auxiliar. Hooks temporários de teclado e mouse existem somente enquanto a cortina preta estiver aberta, não bloqueiam entrada e não guardam teclas, texto ou trajetórias. Pontos de referência do ponteiro ficam apenas em memória para aplicar o limiar de movimento e são descartados ao remover a cortina.

## O que pode entrar

Código, migrações de esquema, configuração sem segredos, documentação genérica, ícones revisados e fixtures sintéticas.

## O que fica fora

Faturas, planilhas de uso, documentos, fotos pessoais, prontuários, cofres, bancos preenchidos, cookies, sessões, logs, capturas com dados reais, prompts e históricos de assistentes. Guarde-os na pasta irmã privada, nunca em `public/` ou assets da aplicação.

## Verificação

Execute `node tools/check-privacy.cjs`. A verificação mostra caminho, linha e categoria, sem imprimir o valor suspeito. Binários e exceções de fixtures exigem hash exato no catálogo `tools/privacy-reviewed.json`; alterá-los exige nova revisão. O catálogo não deve ser atualizado apenas para silenciar um alerta.

A verificação é heurística: não identifica todos os nomes ou dados possíveis e não inspeciona um histórico Git antigo. Revise o diff e os arquivos efetivamente selecionados antes de publicar. `.gitignore` não remove arquivos já rastreados. Não restaure o histórico privado dentro deste diretório.

Os arquivos de teste numéricos convencionais são exemplos sem associação a pessoas; alterações futuras devem manter essa condição.
