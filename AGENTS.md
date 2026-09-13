# AGENTS.md — PEP CLI

CLI em C# usado diariamente pela equipe PEP RM para get, build, merge (TFVC) e utilitários do RM.
Leia este arquivo antes de alterar o código.

## Regras inegociáveis

- **Nunca executar check-in.** Merge termina em Pending Changes.
- Nunca usar `/baseless`, `/force`, `undo` ou resolução de conflito sem decisão explícita do usuário.
- Nunca desfazer, sobrescrever ou apagar alterações locais silenciosamente.
- Nunca encerrar processos apenas pelo nome sem seleção ou confirmação.
- Não presumir que pasta existente está mapeada no TFVC; não criar/alterar workspaces ou mapeamentos.
- Falha parcial não é sucesso: reporte o resultado por destino.
- Na dúvida sobre comportamento do `tf.exe`, verifique — não invente flags ou saídas.

## Fluxo de features (spec lean)

Toda feature ou mudança de comportamento passa por uma spec curta em `specs/NNN-slug.md` (modelo: `specs/_template.md`).
Correções triviais (typo, mensagem, bug óbvio com teste) dispensam spec.

```
rascunho ──(humano aprova)──> aprovada ──(implementa + verifica)──> implementada
```

- Não implemente spec que não esteja `aprovada` ou que tenha decisões em aberto.
- A spec é a fonte da verdade: divergência durante a implementação vira pergunta, não improviso.

Skills de apoio (`.claude/skills/`; em outros agentes, leia o `SKILL.md` como instrução):

| Etapa | Skill |
|---|---|
| Especificar | `spec-feature` |
| Implementar | `implement-spec` |
| Revisar antes de commit/PR | `review-change` |

## Estrutura

```
src/PEPCliHelper.Core    regras e casos de uso (merge, get, build, catálogo, TFVC, processos, histórico). Sem Spectre.Console, sem Console.
src/PEPCliHelper         executável `pep`: CommandChain/, CommandsBuilders/, Menus/, Presentation/
tests/PEPCliHelper.Tests xUnit com fakes (Fakes/) — Core/ e Cli/
specs/ · docs/ · scripts/
```

Plataforma: SDK **9.0.306** fixo em `global.json`, `net9.0`. Não altere sem alinhamento.

## Padrão de commands (manter)

Todo comando segue o mesmo fluxo. Não introduza outro mecanismo de parsing ou execução.

```
Argumentos/menu → ChainOfCommands → ICommandChain (caminho + ajuda) → ICommandBuilder (valida, chama o Core, apresenta) → ICommandExecutor (processos)
```

Para adicionar um comando:

1. Chain em `CommandChain/` (um arquivo por comando ou grupo, ex.: `GetBuildChains.cs`) — declara `Path`, `Summary`, opções e exemplos; só cria o builder.
2. Builder em `CommandsBuilders/` herdando `CommandBuilderBase` — valida argumentos, chama o caso de uso do Core e renderiza (humano e `--json`).
3. Regra de negócio nova vai para o Core, com teste.
4. Registrar em `CommandChain/ChainCatalog.cs`.
5. Documentar em `docs/COMANDOS.md` (a ajuda do terminal vem do próprio chain).

Regras do padrão:

- `ChainOfCommands` casa o caminho mais longo e executa **um** comando.
- Chain não tem lógica; builder não lê `Console` diretamente — use `Ui` (respeita `--json`, `--no-color`, `--ascii`, não interativo).
- Prompts só via `Ui`; sem `CanPrompt`, entrada ausente é `UsageException` (exit 2).
- Erros conhecidos: `UsageException` (2), `PreconditionException` (3), `PepCliException` com o código adequado. Mensagem responde o que falhou, onde, impacto, o que permanece alterado e próximo passo.
- Caminhos e versões vêm da configuração/catálogo; nada fixo no código.
- Processos sempre via `ICommandExecutor` com argumentos estruturados (`Command`), nunca shell.
- TFVC só via `ITfvcClient`. Não adicione check-in, undo, shelve, baseless, `/force`, `/overwrite`, resolução automática ou alteração de workspace (há teste que falha se aparecer).
- Saída do tf.exe não reconhecida ⇒ bloqueado/indeterminado, nunca sucesso.

## Convenções

- Namespaces `PEPCliHelper.*`; indentação de 2 espaços; `PascalCase` para tipos/métodos, `_camelCase` para campos privados.
- Identificadores em inglês; mensagens ao usuário e chaves JSON em português.
- Métodos pequenos com nome que descreve a etapa.
- Commits: `[FEAT]`, `[FIX]`, `[REFACTOR]`, `[ROLLBACK]` + descrição curta.

## Princípios de qualidade

- **Segurança antes de conveniência:** operação destrutiva exige plano visível e confirmação.
- **Responsabilidade única:** um builder por comando; integração TFVC isolada do restante.
- **Falhe explicitamente:** mensagem diz o que falhou, onde e o próximo passo seguro. Nada de `catch` vazio.
- **Simplicidade (KISS/YAGNI):** não crie camadas, interfaces ou comandos sem necessidade concreta.
- **Sem duplicação relevante:** lógica repetida entre builders vai para um ponto único.
- **Não preserve defeitos só por serem legados:** corrija ao tocar, com teste.

## Testes — F.I.R.S.T

- **Fast:** unitários rodam em milissegundos; sem `tf.exe`, rede, MSBuild ou processos reais.
- **Independent:** cada teste monta seu próprio cenário; sem ordem ou estado compartilhado.
- **Repeatable:** mesmo resultado em qualquer máquina — use executor/filesystem falsos, sem depender de `C:\Linha-RM`.
- **Self-validating:** asserts objetivos; nada de conferir saída manualmente.
- **Timely:** escreva o teste junto com o builder ou a correção de bug.

O que testar primeiro: regras do Core com `FakeTfvcClient`/`InMemoryFileSystem`, argumentos gerados para ferramentas, resolução de versão/alias, rejeição de entradas inválidas e o comando pela CLI não interativa (`CliHarness`).
Nome do teste: `Metodo_Cenario_ResultadoEsperado`.

Integração com TFVC real só em branches/workspaces de teste, nunca nos da equipe, e nunca com check-in.

## Antes de concluir uma alteração

- [ ] Build sem erros e testes passando.
- [ ] Nenhum caminho de check-in, baseless ou sobrescrita introduzido.
- [ ] Ajuda do chain e `docs/COMANDOS.md` atualizados se o comando mudou.
- [ ] Mudança em interação com tf.exe registrada em `docs/HOMOLOGACAO.md`.
- [ ] Informe o que não foi testado e por quê.
