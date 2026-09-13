# 007 — Merge: execução e resultado

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
Não há atomicidade entre versões. O usuário precisa saber exatamente o que foi aplicado, onde há conflito e o que não foi executado — sem que falha parcial pareça sucesso.

## Comando
Continuação de `pep merge` após confirmação. Opção: `--continue-on-failure`.

## Regras
**Por destino, em sequência**
1. Revalidar mapeamento, pending changes no escopo e candidatos.
2. Registrar snapshot de pending changes (antes).
3. `tf merge /recursive /noimplicitbaseless /version:C<id>~C<id>` com diretório de trabalho no destino.
4. Inspecionar exit code; listar conflitos; ler pending changes (depois).
5. Novas pendências = depois − antes (não atribuir pendências preexistentes ao CLI).
6. Registrar no histórico e orientar revisão.

**Estados**
| Estado | Regra |
|---|---|
| Aplicado com pending changes | exit 0 e novas pendências |
| Sem alterações aplicáveis | exit 0 e nenhuma nova pendência |
| Já integrado | não candidato na pré-verificação |
| Aplicado com conflitos | conflitos detectados após merge |
| Bloqueado | pré-verificação ou revalidação |
| Falhou | exit ≠ 0/1 sem novas pendências |
| Cancelado | Ctrl+C durante o destino (estado pode ser parcial) |
| Não iniciado | interrompido antes |
| Indeterminado | exit 1 sem conflitos, exit de falha com novas pendências, ou saída não reconhecida |

**Interrupção**
- Padrão: após falha, conflito, indeterminado ou cancelamento, destinos seguintes ficam **não iniciados**.
- `--continue-on-failure` segue apenas após *falha* ou *conflito* em destino independente.
- Rede, autenticação, indeterminado e cancelamento sempre interrompem.
- **Sem rollback automático.** Cancelamento não é rollback.

**Reexecução**
- Pré-verificação consulta candidatos, pending changes e histórico local; não repete cegamente.

**Resumo final**
- Tabela por versão: estado (ícone + texto), novas pendências, conflitos, duração.
- Lista de conflitos e destinos não executados.
- Instruções de revisão (`pep pending list <versao>`, Source Control Explorer, `tf resolve`).
- Destaque: *"Nenhum check-in foi realizado."*
- ID da execução para `pep history show <id>`.

**Códigos de saída**: 0 todos aplicados/sem alterações/já integrados · 3 nada executado por bloqueio · 4 falha sem nada aplicado · 5 conflito ou resultado parcial · 130 cancelado.

## Cenários de aceite
- **Dado** 2 destinos válidos **Quando** confirmado **Então** ambos aplicados, pending changes preservadas, nenhum check-in, exit 0.
- **Dado** conflito no merge **Quando** termina **Então** lista conflitos, mantém estado, não resolve, exit 5.
- **Dado** 1º destino aplicado e 2º falha **Quando** termina **Então** resultado parcial, 1º preservado, 3º não iniciado, sem rollback, exit 5.
- **Dado** repetição do mesmo merge **Quando** plano **Então** "já integrado" e nada reaplicado.
- **Dado** pendência preexistente fora do escopo **Quando** merge aplicado **Então** não é contada como nova.

## Fora de escopo
- Resolução de conflitos; check-in; undo.

## Decisões em aberto
Nenhuma.

## Plano
- [x] Executor sequencial com snapshot antes/depois
- [x] Classificador de estados e política de interrupção
- [x] Resumo, JSON e códigos de saída

## Verificação
Ver `docs/HOMOLOGACAO.md`.
