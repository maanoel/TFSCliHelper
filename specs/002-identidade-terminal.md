# 002 — Identidade visual e terminal

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
O CLI atual é um prompt de texto sem identidade, sem menus, sem tabelas e sem distinção visual de estado. A equipe quer um CLI final bonito, com marca própria no estilo do Angular CLI, sem perder clareza e compatibilidade.

## Comando
```
pep                 menu interativo com banner
pep version         banner + tabela de ambiente (como `ng version`)
pep --version       só o número da versão
pep help [comando]  ajuda geral ou contextual
pep <comando> --help
```
Opções globais: `--no-color`, `--ascii`, `--json`, `--non-interactive`, `--yes|-y`, `--config <arquivo>`, `--verbose`.

Menu principal: Merge · Get · Build · Ambientes e versões · Diagnóstico · Pending changes · Ferramentas locais · Histórico · Ajuda · Sair.

## Regras
- Banner "PEP CLI" em letras grandes com gradiente; versão ASCII pura quando `--ascii`, saída redirecionada ou terminal sem Unicode.
- Estado nunca só por cor: sempre ícone + texto (`✔ OK` / `[OK]`, `✖ FALHA`, `▲ AVISO`, `● INFO`, `⊘ BLOQUEADO`).
- `--no-color` e variável `NO_COLOR` desativam cores; saída redirecionada desativa ANSI, animações e prompts.
- `--json`: apenas JSON no stdout, sem banner, ANSI, spinner ou prompt; implica não interativo.
- Não interativo (`--non-interactive`, `--json`, stdin/stdout redirecionados): entrada ausente ⇒ erro de uso com orientação, nunca prompt oculto.
- Menus sempre oferecem **Voltar**; Ctrl+C cancela a operação com segurança e informa que cancelamento não é rollback.
- Spinner só quando não há progresso mensurável; nunca percentual inventado.
- Operações longas mostram operação, versão/projeto, etapa atual, resultado e duração.
- Menu monta a mesma linha de comando dos argumentos e executa o mesmo builder.

## Cenários de aceite
- **Dado** terminal interativo **Quando** `pep` **Então** exibe banner e menu navegável por teclado.
- **Dado** saída redirecionada **Quando** `pep version` **Então** sai sem sequências ANSI e com banner ASCII.
- **Dado** `--json` **Quando** qualquer comando com suporte **Então** stdout é JSON válido e nada mais.
- **Dado** `--non-interactive` e parâmetro faltando **Quando** executado **Então** exit 2 e mensagem de uso, sem aguardar entrada.
- **Dado** `pep merge --help` **Quando** executado **Então** mostra descrição, sintaxe, opções e exemplos.

## Fora de escopo
- Temas configuráveis além de cor/sem cor/ASCII.

## Decisões em aberto
Nenhuma.

## Plano
- [x] Tema, ícones de estado, banner Unicode/ASCII
- [x] Detecção de capacidades e opções globais
- [x] Help geral e contextual, `version`
- [x] Menu interativo com Voltar

## Verificação
Ver `docs/HOMOLOGACAO.md`.
