# 012 — Histórico, logs e códigos de saída

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
Não há registro do que o CLI executou, dificultando inspecionar falhas parciais e retomar sem duplicar operações. Scripts não têm códigos de saída estáveis.

## Comando
```
pep history list [--limit <n>] [--json]
pep history show <id> [--json]
```

## Regras
- Cada operação que consulta ou altera TFVC/arquivos/processos grava um registro em `%LOCALAPPDATA%\PepCli\history\<id>.json`.
- Registro: id, comando, parâmetros não sensíveis, versões/projetos, início/fim/duração, etapas (horário, alvo, etapa, resultado, exit code do tf, trecho da saída limitado), resultado por alvo, exit code, orientações de recuperação.
- Não registra credenciais nem conteúdo de arquivos; saída de ferramenta truncada (máx. 4 000 caracteres por etapa).
- Histórico local não substitui o histórico TFVC (dito em `history show`).
- Falha ao gravar histórico gera aviso, não derruba a operação.
- **Erros** respondem: o que falhou · onde · impacto · o que permanece alterado · próximo passo seguro.

**Códigos de saída (estáveis)**
| Código | Significado |
|---|---|
| 0 | Sucesso |
| 1 | Erro inesperado |
| 2 | Uso ou configuração inválida |
| 3 | Pré-condição não atendida (bloqueio) |
| 4 | Falha operacional |
| 5 | Conflito ou resultado parcial |
| 130 | Cancelado |

## Cenários de aceite
- **Dado** um merge concluído **Quando** `history list` **Então** aparece com id, comando, resultado e duração.
- **Dado** `history show <id>` **Quando** executado **Então** mostra etapas e resultado por destino.
- **Dado** id inexistente **Quando** `history show` **Então** exit 2 com orientação.

## Fora de escopo
- Envio de logs para servidor.

## Decisões em aberto
Nenhuma.

## Plano
- [x] ExecutionJournal
- [x] ExitCodes e formato de erro
- [x] Chains history

## Verificação
Ver `docs/HOMOLOGACAO.md`.
