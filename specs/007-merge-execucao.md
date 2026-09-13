# 007 — Merge: execução e resultado

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
Não há atomicidade entre versões. O usuário precisa saber exatamente o que foi aplicado, onde há conflito e o que não foi executado — sem que falha parcial pareça sucesso.

## Comando
Continuação de `pep merge` após confirmação. Opções: `--stop-on-failure`; `--continue-on-failure` (padrão, mantido por compatibilidade; os dois juntos ⇒ exit 2).

## Decisão do usuário (2026-09-13)
Substitui "interromper após falha ou conflito por padrão":
- O merge roda em **todos** os destinos selecionados por padrão; `--stop-on-failure` restaura a interrupção após falha, conflito ou bloqueio. Destinos bloqueados no plano (não interativo) são ignorados e listados, salvo `--stop-on-failure` (exit 3).
- Revalidação leve se o plano tem menos de 10 minutos: só pending changes no escopo e graváveis sem checkout (etapas 4–5 do 006); o mesmo `tf status` é o snapshot "antes". Plano mais antigo: revalidação completa (mapeamento, candidatos, pending, graváveis, conflitos), também reaproveitando o status.
- Pré-verificação dos destinos em paralelo (spec 006); execução continua sequencial. Após o merge, conflitos e status "depois" são consultados em paralelo.
- Exit 1 sem conflitos, com novas pendências, sem código `TF\d{5,6}` na saída e com a lista de conflitos legível ⇒ *Aplicado com pending changes* (AutoMerge, evidência real tf.exe 17.14 pt-BR).

## Regras
**Por destino, em sequência**
1. Revalidar (leve ou completa, ver decisão acima).
2. Registrar snapshot de pending changes (antes) — reaproveitado da revalidação.
3. `tf merge /recursive /noimplicitbaseless /version:C<id>~C<id>` com diretório de trabalho no destino.
4. Inspecionar exit code; listar conflitos; ler pending changes (depois).
5. Novas pendências = depois − antes (não atribuir pendências preexistentes ao CLI).
6. Registrar no histórico e orientar revisão.

**Estados**
| Estado | Regra |
|---|---|
| Aplicado com pending changes | exit 0 e novas pendências; ou exit 1 AutoMerge (sem conflitos, sem código TF) |
| Sem alterações aplicáveis | exit 0 e nenhuma nova pendência |
| Já integrado | não candidato na pré-verificação |
| Aplicado com conflitos | conflitos detectados após merge |
| Bloqueado | pré-verificação ou revalidação |
| Falhou | exit ≠ 0/1 sem novas pendências |
| Cancelado | Ctrl+C durante o destino (estado pode ser parcial) |
| Não iniciado | interrompido antes |
| Indeterminado | exit 1 sem conflitos e sem novas pendências (ou com código TF, ou conflitos ilegíveis), exit de falha com novas pendências, ou saída não reconhecida |

**Interrupção**
- Padrão (2026-09-13): após falha, conflito ou bloqueio na revalidação, segue para os demais destinos.
- `--stop-on-failure`: após falha, conflito ou bloqueio, destinos seguintes ficam **não iniciados**.
- Rede, autenticação, indeterminado e cancelamento sempre interrompem.
- **Sem rollback automático.** Cancelamento não é rollback.

**Reexecução**
- Pré-verificação consulta candidatos, pending changes e histórico local; não repete cegamente.

**Resumo final**
- Linha "Merge aplicado em X de Y destinos selecionados" (+ já integrados/sem alterações); JSON `aplicados`/`selecionados`.
- Tabela por versão: estado (ícone + texto), novas pendências, conflitos, duração.
- Destinos bloqueados, não iniciados, com falha ou indeterminados listados com o motivo.
- Lista de conflitos e destinos não executados.
- Instruções de revisão (`pep pending list <versao>`, Source Control Explorer, `tf resolve`).
- Destaque: *"Nenhum check-in foi realizado."*
- ID da execução para `pep history show <id>`.

**Códigos de saída**: 0 todos aplicados/sem alterações/já integrados · 3 nada executado por bloqueio · 4 falha sem nada aplicado · 5 conflito ou resultado parcial · 130 cancelado.

## Cenários de aceite
- **Dado** 2 destinos válidos **Quando** confirmado **Então** ambos aplicados, pending changes preservadas, nenhum check-in, exit 0.
- **Dado** conflito no merge **Quando** termina **Então** lista conflitos, mantém estado, não resolve, exit 5.
- **Dado** 1º destino aplicado e 2º falha com `--stop-on-failure` **Quando** termina **Então** resultado parcial, 1º preservado, 3º não iniciado, sem rollback, exit 5.
- **Dado** 3 destinos e conflito no 1º (padrão) **Quando** termina **Então** os 3 executados, exit 5.
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
