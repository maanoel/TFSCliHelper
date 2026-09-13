# 006 — Merge: plano, pré-verificação e dry-run

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
Propagar um changeset da atual para as legadas exige abrir o Visual Studio versão a versão. O legado automatizou isso com `/baseless` para todas as versões, sem validar nada.

## Comando
```
pep merge                                                    interativo
pep merge --project back --source atual --target 2606 --changeset 861799
pep merge --project back --source atual --target 2606 --target 2602 --changeset 861799
pep merge --project sau --source atual --all-legacy --changeset 861799
pep merge ... --dry-run [--json]
```

## Regras
**Entrada**
- Projeto, origem, um ou mais destinos e changeset são obrigatórios. Interativo pergunta; não interativo falha com exit 2.
- Origem começa selecionada em `atual` no menu, mas sempre aparece no resumo.
- `--all-legacy` = legadas **ativas** do catálogo, exceto a origem.
- Nunca usar automaticamente o changeset mais recente.
- Origem diferente de `atual` é permitida apenas se a validação de topologia (candidatos) aprovar; exibe aviso.

**Changeset**
- Existe? Não ⇒ bloqueio global.
- Itens classificados em: no escopo (`<servidor origem>/<projeto>`), outros projetos da origem, fora da origem.
- Nenhum item no escopo ⇒ bloqueio global. Itens externos são listados como **excluídos**; o merge por caminho de projeto não os propaga.
- Dependências de changesets anteriores não são resolvidas automaticamente; o preview e os conflitos são o sinal exibido.

**Por destino** (resultado: pronto, já integrado ou bloqueado com motivos)
1. Pasta local do projeto existe.
2. Mapeamento TFVC efetivo cobre a pasta, não é cloaked e aponta para o caminho de servidor configurado.
3. Candidatos: changeset é candidato de `origem → destino`. Não listado ⇒ **já integrado**. Erro ⇒ **bloqueado: sem relação de merge suportada** (nunca baseless, nunca cópia).
4. Pending changes preexistentes: as que atingem arquivos do changeset no destino **bloqueiam**; as demais são informadas.
5. Alterações não reconciliadas: arquivo do changeset no destino gravável e sem pending em workspace de servidor ⇒ bloqueia. Workspace local (`$tf`) ⇒ aviso da limitação.
6. Conflitos não resolvidos no escopo ⇒ bloqueia; fora do escopo ⇒ informa.
7. Atualização local: `get /preview`; itens pendentes de get ⇒ aviso. Interativo oferece: executar get (com confirmação) · continuar sem get · cancelar. Get com conflito/falha ⇒ merge não continua.
8. `merge /preview` real: falha ⇒ bloqueado com a mensagem do tf.
9. Histórico local: execução anterior do mesmo changeset/destino é exibida.

**Plano e confirmação**
- Tabela: origem, projeto, changeset, coleção, e por destino workspace, caminho local, caminho de servidor, pendências, conflitos, atualização, preview, impedimentos.
- Mensagem obrigatória: *"O merge será aplicado aos destinos selecionados e permanecerá em Pending Changes. Nenhum check-in será realizado."*
- Destinos bloqueados: interativo pergunta se segue só com os prontos; não interativo segue com os prontos e lista os bloqueados, salvo `--stop-on-failure` (exit 3) — decisão 2026-09-13, ver spec 007.
- Desempenho (2026-09-13): destinos avaliados em paralelo (até 4, somente consultas), ordem do plano preservada; histórico local lido uma vez por plano. Falha de rede/autenticação bloqueia os destinos ainda não iniciados.
- `--yes` dispensa só a confirmação; não ignora validações nem autoriza nada além do plano.
- **Dry-run:** executa todas as consultas acima e **nenhuma** alteração (sem merge, sem get). Exit 0 se todos prontos/já integrados, 3 se houver bloqueio. Informa que conflitos só são conhecidos na execução real.

## Cenários de aceite
- **Dado** pasta existente sem mapeamento **Quando** merge **Então** destino bloqueado, explicação de como corrigir (`tf workfold`/Source Control Explorer) e nada alterado.
- **Dado** pending change em arquivo do changeset no destino **Quando** pré-verificação **Então** bloqueia e preserva.
- **Dado** changeset com itens de Sau-PEP e Sau-Saude **Quando** `--project back` **Então** mostra incluídos e excluídos.
- **Dado** `--dry-run` **Quando** executado **Então** plano completo e zero comandos de alteração enviados ao tf.exe.
- **Dado** changeset não candidato **Quando** plano **Então** destino "já integrado", sem reaplicar.
- **Dado** `--non-interactive` sem `--changeset` **Quando** executado **Então** exit 2 sem aguardar entrada.

## Fora de escopo
- Merge de vários changesets; busca de changesets por texto; baseless.

## Decisões em aberto
Nenhuma.

## Plano
- [x] MergeRequest e validação de entrada
- [x] Análise de escopo do changeset
- [x] Pré-verificação por destino
- [x] Renderização do plano, confirmação e dry-run

## Verificação
Ver `docs/HOMOLOGACAO.md`.
