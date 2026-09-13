# 001 — Plataforma e estrutura

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
O CLI atual é net5.0 (sem suporte), um único projeto sem testes, com versões e caminhos fixos em código e comandos enviados como texto a um `cmd.exe`. Não é possível evoluir com segurança.

## Comando
Sem comando novo. Estrutura:
```
global.json                      SDK 9.0.306, rollForward: disable
Directory.Build.props            net9.0, Nullable, ImplicitUsings
PEPCliHelper.sln
src/PEPCliHelper.Core/           domínio, casos de uso, portas, adapters (TFVC, processos, arquivos)
src/PEPCliHelper/                executável `pep`: chains, builders, menus, apresentação
tests/PEPCliHelper.Tests/        xUnit
```

## Regras
- `global.json` fixa 9.0.306 sem roll forward; todos os projetos em `net9.0`.
- `Core` não referencia Spectre.Console nem lê `Console`: lógica de negócio independe de menus, cores e prompts.
- Padrão de commands preservado e corrigido:
  - `ICommandChain` declara o caminho do comando (`["delete","broker"]`), ajuda e cria o builder;
  - `ICommandBuilder` valida argumentos, chama o caso de uso e apresenta o resultado;
  - `ICommandExecutor` executa processos (porta no Core);
  - `ChainOfCommands` para no **primeiro** chain que casa (corrige o legado que executava todos e sempre imprimia "comando não existe").
- Modo interativo e por argumentos usam os mesmos builders e casos de uso.
- Código legado da raiz é removido após a migração (fica no histórico git).
- Sem dependências redundantes: Spectre.Console (apresentação) e xUnit (testes).

## Cenários de aceite
- **Dado** o SDK 9.0.306 instalado **Quando** `dotnet build` e `dotnet test` na raiz **Então** compilam e passam.
- **Dado** um token que casa com dois chains (`open host` e `open`) **Quando** resolvido **Então** o caminho mais longo vence e só um executa.
- **Dado** o projeto Core **Quando** inspecionadas as referências **Então** não há Spectre.Console.

## Fora de escopo
- Container de DI; empacotamento MSI.

## Decisões em aberto
Nenhuma.

## Plano
- [x] global.json, props, solution, projetos
- [x] Contratos Chain/Builder/Executor e ChainOfCommands
- [x] Projeto de testes xUnit
- [x] Remoção do código legado

## Verificação
Ver seção Verificação do relatório de implementação em `docs/HOMOLOGACAO.md`.
