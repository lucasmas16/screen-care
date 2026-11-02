# ADR 0001 — Repositório independente e material privado externo

Status: adotada nesta organização.

## Contexto

O desenvolvimento acumulou código, documentos de trabalho e resultados locais. A publicação precisa ser reproduzível e excluir dados de uso.

## Decisão

Um repositório por aplicativo, iniciado futuramente dentro de `github/`. Manter a estrutura funcional do código e registrar a arquitetura atual. Material pessoal vive na pasta irmã `privado/`, fora da raiz Git. Histórico anterior permanece privado.

Dependências e compilações são reproduzidas a partir dos manifestos/scripts; recursos necessários de `build/` são fontes e continuam aqui. Testes públicos usam dados sintéticos.

## Alternativas

Um monorepositório reunindo todos os aplicativos uniria ciclos de vida e tecnologias sem benefício imediato. Reorganizar integralmente em camadas novas aumentaria o risco sem ser necessário para a publicação.

## Consequências

Documentação e exclusões são semelhantes entre projetos, mas cada um conserva seus comandos e decisões. Prompts e backups não acompanham clones. A cópia preservada permite consultar o estado original, sem reaproveitar seu histórico no futuro repositório público.
