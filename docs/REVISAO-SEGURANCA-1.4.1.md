# Revisão de segurança e privacidade — Screen Care 1.4.1

Data: 2026-11-01. Revisão local do código, scripts, arquivos destinados ao repositório e pacotes de distribuição. Não é certificação nem garantia de ausência absoluta de vulnerabilidades.

## Correções e reforços

1. **Configuração XML nula:** um documento com `xsi:nil` causava `NullReferenceException` na abertura. A falha foi reproduzida antes da correção. Agora retorna aviso e desabilita a automação. Testes também cobrem XML inválido, entidades externas e documento acima do limite de 65.536 caracteres.
2. **Busca de bibliotecas nativas:** as chamadas nativas da aplicação e do instalador passaram a usar somente `System32`. Arquivos inertes com os nomes das DLLs foram colocados ao lado do executável de teste; as bibliotecas efetivamente carregadas continuaram vindo do Windows.
3. **Destinos do instalador:** a verificação passou a inspecionar também os ancestrais, atalhos e arquivos conhecidos. Junctions no destino e em um ancestral foram rejeitadas em testes reais, preservando o conteúdo apontado. Instalação, remoção e recuperação verificam os caminhos; não há exclusão recursiva da pasta do usuário.
4. **Metadados de imagens:** a leitura da orientação tolera metadado sem bytes, evitando indexação inválida.

## Privacidade e fronteiras

- Não foram encontrados clientes HTTP, servidores, telemetria, envio de dados, credenciais embutidas ou registro de teclas no código revisado. Raw Input detecta atividade; não grava o texto digitado.
- Preferências e caminhos de imagens ficam no perfil local. O log de erro pode conter caminhos locais. Esses arquivos não integram o fonte nem os pacotes.
- Resultados de teste, capturas, binários de diagnóstico, arquivos privados e versões anteriores ficam fora da seleção pública. Capturas da interface do instalador podem mostrar o nome do usuário e não devem ser publicadas sem revisão.
- Não há dependências NuGet ou JavaScript de produção. A execução depende do .NET Framework, Windows, APIs de controle e decodificadores de imagem do sistema.
- O manifesto usa `asInvoker`; a instalação e a inicialização automática usam o perfil e o registro do usuário atual.

## Verificação reproduzível

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -RunTests
powershell -ExecutionPolicy Bypass -File .\tools\test-security.ps1
node tools/check-privacy.cjs
powershell -ExecutionPolicy Bypass -File .\tools\package-release.ps1
```

Na sessão interativa de validação: 361 verificações funcionais aprovadas, incluindo aquisição/liberação do atalho, XML, atualização de executáveis e recuperação de gravações parciais. Testes adicionais verificaram junctions e carregamento de DLLs. O número pode ser menor quando outra instância ocupa o atalho; essa condição é registrada explicitamente.

O instalador permite extrair seu conteúdo com `--verify-payload` para uma pasta vazia. Os arquivos extraídos devem coincidir por SHA-256 com a compilação testada. O empacotador inclui no ZIP apenas executável, configuração de runtime, manual e licença, verifica os hashes internos e gera `SHA256SUMS.txt`.

## Limites e pendências

- A revisão e a busca por dados privados são heurísticas; não provam inexistência de todas as falhas ou dados sensíveis. O repositório público começa com o conjunto revisado, sem importar histórico privado; alterações futuras exigem nova revisão do índice.
- As verificações de caminhos não são uma barreira contra um processo malicioso já executando como o mesmo usuário e capaz de trocar arquivos entre verificações. Não executar o instalador elevado. Perfis ou pastas redirecionadas por links serão rejeitados.
- Imagens são decodificadas pelo sistema operacional. Manter Windows/.NET atualizados e usar imagens de origem confiável. Pastas de rede escolhidas pelo usuário podem usar a autenticação de arquivos do Windows.
- A distribuição ainda não tem assinatura Authenticode. SHA-256 verifica integridade quando comparado a uma origem confiável, mas não substitui assinatura do editor.
- Não foi executada instalação/desinstalação real nesta revisão. A troca e a recuperação dos arquivos foram testadas em diretórios sintéticos; o desligamento físico das telas precisa ser validado no equipamento.
- O canal privado de relatos de vulnerabilidades foi ativado na publicação. Consulte [SECURITY.md](../SECURITY.md).

## Referências

- [Microsoft: DefaultDllImportSearchPaths](https://learn.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.defaultdllimportsearchpathsattribute?view=netframework-4.8.1).
- [Microsoft: segurança no carregamento de DLLs](https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-security).
