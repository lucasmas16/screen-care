# Arquitetura — Screen Care

## Contexto e decisão

Aplicação nativa com máquina de estados independente da ação física. O controlador decide quando ativar; a integração com Windows produz eventos; a camada de apresentação executa a ação escolhida. Essa fronteira permite testar o tempo e as transições sem desligar os monitores.

A interface e o instalador permanecem em um único código-fonte pequeno. Não foi feita migração de framework nem alteração de regras de inatividade durante a organização.

## Fluxo principal

```text
Entrada / sessão → controlador de inatividade → desligamento ou slideshow
Interface ↔ configurações XML
```

## Mapa do código

- `src/IdleController.cs`: máquina de estados de inatividade.
- `src/Native.cs` e `src/SessionState.cs`: entrada, energia e sessão.
- `src/Images.cs` e `src/Slideshow.cs`: catálogo e apresentação.
- `src/Settings.cs`: persistência; `SettingsForm.cs`: edição.
- `src/TrayApplication.cs`: composição do aplicativo.
- `setup/Setup.cs`: instalador; `tests/Tests.cs`: verificações.

## Estado e arquivos

Fotos selecionadas continuam nas pastas escolhidas pelo usuário. Preferências e caminhos de fotos são privados; nenhuma foto de uso pessoal acompanha este código.

## Fronteiras de manutenção

Mudanças de interface devem preservar os contratos da camada de dados e das integrações nativas. Novos formatos externos pertencem aos adaptadores; dados reais nunca pertencem às fixtures. Alterações estruturais futuras devem registrar contexto, alternativas, decisão e consequência em `docs/decisions/`.

## Evolução planejada

- Manter a máquina de estados independente da interface.
- Ampliar a matriz de validação manual para múltiplos monitores e suspensão.

Não há proposta de microserviços: o tamanho atual e a execução local favorecem um único aplicativo com responsabilidades delimitadas.
