# 013 — Compatibilidade com comandos antigos

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
A equipe e scripts usam as sintaxes antigas. Elas não podem passar a executar algo diferente silenciosamente, e as partes inseguras não podem ser preservadas.

## Comando
| Antigo | Novo comportamento |
|---|---|
| `get all`, `get version <v>` | Mantidos (Sau-PEP e Sau-Saude; sem FrameHTML) com plano e confirmação |
| `build all`, `build version <v>` | Mantidos; não encerram o host automaticamente |
| `merge <projeto> <versao> <changeset>` | Versão = **origem**; destinos por seleção explícita (interativo) ou erro orientando `--target/--all-legacy` (não interativo). Nunca "todas as outras" implícito; nunca baseless |
| `delete broker <v>` | Mantido com confirmação e backup |
| `open host|rm|alias|hostconfig <v>` | Mantidos |
| `kill host` | Mantido com seleção explícita e encerramento gracioso |
| `merge front ...`, `open front`, `open podoc` | **Removidos**: front fora do escopo |
| `kill all` | **Removido**: encerrava processos só pelo nome |
| `cmd <comando>` | **Removido**: executava shell arbitrário |
| `clear`, `cls`, `exit` | Substituídos pelo menu interativo |
| Versões `32`, `33`, `34`, `2205`, `2209` | Substituídas pelo catálogo configurável |

## Regras
- Sintaxe antiga de merge mostra aviso de depreciação com a sintaxe nova equivalente.
- Comando removido mostra motivo e alternativa, exit 2, sem executar nada.
- Nenhuma interpretação de check-in automático.

## Cenários de aceite
- **Dado** `pep merge back atual 861799 --non-interactive` **Quando** executado **Então** exit 2 orientando `--target`/`--all-legacy`, nada executado.
- **Dado** `pep merge back atual 861799` interativo **Quando** executado **Então** aviso de depreciação, origem = atual e seleção de destinos.
- **Dado** `pep kill all` **Quando** executado **Então** mensagem de comando removido, exit 2.

## Fora de escopo
- Modo REPL com comandos digitados (substituído pelo menu).

## Decisões em aberto
Nenhuma.

## Plano
- [x] Detecção da sintaxe antiga de merge
- [x] Chains de comandos removidos

## Verificação
Ver `docs/HOMOLOGACAO.md`.
