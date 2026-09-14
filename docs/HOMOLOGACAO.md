# Homologação e situação dos testes

> **O PEP CLI não está pronto para produção** enquanto os roteiros TFVC abaixo não forem executados e aprovados.

## 1. Testes automatizados (executados)

`dotnet test PEPCliHelper.sln` — xUnit, sem tf.exe, rede, MSBuild ou pastas reais (F.I.R.S.T).

| Área | Cobertura |
|---|---|
| Catálogo | resolução por id/alias/segmento, token parecido rejeitado, ambiguidade, inativa, limite de 4 legadas, ordem ativa |
| Configuração | validações de caminho local × servidor, aliases duplicados, arquivos fora da versão, segredos rejeitados, backup, precedência |
| Rotação | nova atual, antiga vira legada, desativadas preservadas, recusa de 5 legadas |
| tf.exe | parser de workfold (inglês e pt-BR), mapeamento ancestral/cloaked/divergente/não mapeado/rede, classificação de exit codes e TF400324/TF30063, extração de caminhos, candidatos, argumentos de merge (`C<id>~C<id>`, sem baseless/force), get sem force, resolve só preview, interface sem check-in |
| Merge | 2 destinos aplicados sem check-in, sem mapeamento, pending no escopo × fora, gravável sem checkout, escopo com outro projeto, changeset sem itens do projeto, dry-run sem merge/get, já integrado, sem relação de merge, rede, conflito interrompe, falha parcial sem rollback, continue-on-failure, pendência preexistente não contada, cancelamento, histórico anterior |
| Get/Build | alvos do catálogo, bloqueio e continuação, parcial, rede interrompe, host aberto bloqueia build, falha interrompe, configuração repassada |
| Ferramentas | broker ausente/backup/processo aberto/caminho fora, inferência de host e confiança, término gracioso sem kill |
| CLI | parsing (repetição, opção desconhecida, sem valor, não numérico), modo não interativo sem prompt, sintaxe antiga, front removido, dry-run JSON válido sem ANSI, `--yes`, destino bloqueado, kill host sem alvo/PID, removidos, get sem `--yes`, config ausente, help, banner ASCII, sugestão de comando, histórico |

Resultado atual: **154 aprovados, 7 ignorados (fixtures reais ainda não capturadas), 0 falhas**; projeto de integração: 32 aprovados (travas de `IntegrationConfig` e `RecordingCommandExecutor`, sem tf.exe) e 11 ignorados sem `PEPCLI_IT_CONFIG` (ver seção 4).

Inclui 9 testes de terminal interativo (Spectre.Console.Testing): merge guiado, confirmação negada, multisseleção vazia, Cancelar (merge e env configure), kill host por seleção e menu Voltar/Sair.

### Revisão independente de segurança

Um revisor (agente separado, somente leitura) verificou as 10 regras inegociáveis: **nenhuma violação bloqueante**. Achados corrigidos antes da entrega, com teste:

- pending changes com caminho não reconhecido (subst/junction/encoding) agora bloqueiam como estado indeterminado;
- Ctrl+C entre destinos retorna 130 (merge, get e build);
- merge com exit 0 sem novas pendências, mas com itens no preview, é *indeterminado* (não sucesso);
- `kill host` confere nome/início/caminho do processo no mesmo handle antes de encerrar (PID reutilizado);
- `delete broker` revalida processos e bloqueio depois da confirmação;
- leitura de saída com timeout e serialização; MSBuild com `/nodeReuse:false`; code page ANSI do Windows (ver evidência de encoding);
- tf nunca executa com pasta de trabalho inexistente; itens do changeset lidos só de linhas de item.

## 2. Smoke test local (executado, máquina da equipe)

Executado com o binário real, somente comandos de leitura ou bloqueados antes de alterar:

- `pep version` — localizou tf.exe 17.14 e MSBuild 17.14 via vswhere.
- `pep config init` e `pep env discover` em arquivo de configuração temporário — encontrou Atual\Release e 10 pastas legadas, avisou o limite de 4, tratou o servidor indisponível (TF400324) sem falhar.
- `pep merge ... --dry-run` — bloqueou por servidor indisponível, exit 3, nenhum comando de alteração.
- `pep merge back atual 861799 --non-interactive` — exit 2 exigindo destinos.
- `pep delete broker 2606 --non-interactive` — exit 2 exigindo `--yes`; arquivo preservado.
- `pep kill all` — exit 2, comando removido.
- `scripts/capture-tf-fixtures.ps1` (2026-09-13) — 8 comandos de leitura/preview executados; servidor indisponível (exit 100), nenhum comando de alteração.

### Evidência de encoding (2026-09-13)

Saída real do tf.exe 17.14 capturada com stdout redirecionado e `CreateNoWindow`:

| Decodificada como | Resultado |
|---|---|
| OEM 850 (`GetOEMCP`) | `o Azure DevOps services nÒo estß disponÝvel` ✖ |
| ANSI 1252 (`GetACP`) | `o Azure DevOps services não está disponível` ✔ |

Decisão: `ProcessCommandExecutor` e o script de captura usam a code page **ANSI** (`GetACP`). Confirmar também com a saída de sucesso na H1.

### Primeira execução real (2026-09-13, changeset 669997, back, atual → 4 legadas)

Correções feitas a partir da saída real do tf.exe 17.14 pt-BR:

| Achado | Correção |
|---|---|
| `tf.exe` de linha de comando sem credencial (TF30063) | Login automático em TF30063 (tf workspaces sem /noprompt no terminal do usuário, uma vez por execução, depois repete a consulta); o antigo comando de login foi removido. Pendente: homologar em terminal real (janela de login e spinner) |
| `Opção /version não pode ser combinada com opção /candidate` | `merge /candidate` sem `/version`; changeset procurado na lista |
| `merge /preview` exit 1 `Conflito (mesclar, editar)` bloqueava | Conflito previsto vira aviso; só falha real bloqueia |
| `resolve /preview` lista conflito com caminho **relativo** (exit 1) | Parser usa exit code (0 = sem conflitos) e completa o caminho |

Validados com saída real: `tf changeset` (Itens/`editar`), `tf workfold` (`Workspace:`/`Coleção  :`), `merge /candidate`, `merge /preview`, `tf status /format:detailed` (`Item local :`), `resolve /preview`.

Resultado: 12.1.2510, 12.1.2602 e 12.1.2606 **Aplicado com conflitos** (1 pending change `mesclar` + 1 conflito cada), sem check-in. 12.1.2506: merge aplicado com conflito na 1ª execução (classificado *indeterminado* antes da correção do parser) e depois encontrado **sem pending changes** e ainda candidato — desfeito fora do CLI.

### Merge em todas as versões e desempenho (2026-09-13)

- Causa da sensação "merge só em algumas versões": (1) interrupção após o primeiro conflito — agora o padrão é **continuar nos demais destinos** (`--stop-on-failure` restaura o comportamento estrito); (2) menu `pep` aberto antes da atualização continuava com o parser antigo — **feche e reabra o `pep` após atualizar**.
- Exit 1 com `Conflito resolvido automaticamente ... como AutoMerge`, sem conflito pendente e sem código `TFnnnnn`, é classificado como aplicado.
- Desempenho medido: dry-run real de 4 legadas **22,6 s** (antes ~60 s) com planejamento em paralelo (máx. 4). Execução: revalidação leve para plano com menos de 10 min (reaproveita o `tf status` como estado anterior) e consultas pós-merge em paralelo; merges continuam **sequenciais**.
- A homologar: `tf.exe` em paralelo no mesmo workspace sob carga real (validado em dry-run real sem erros).

## 3. Homologação TFVC (parcial — ver execução real acima)

**Motivo:** a coleção não estava acessível na máquina de desenvolvimento (TF400324 / SSL) e não há coleção/branches de teste provisionados.

### Pré-requisitos

- Coleção ou projeto **exclusivo de teste** (nunca branches reais da equipe).
- Branch `$/Teste-PEP/atual/release` com filhos `$/Teste-PEP/Legado/A` e `$/Teste-PEP/Legado/B` criados por *branch* (relação TFVC real) e `$/Teste-PEP/Legado/SemRelacao` criada por cópia (sem relação).
- Dois workspaces na máquina: um de **servidor** e um **local**; um mapeamento cloaked; uma pasta existente sem mapeamento.
- Changesets de fixture feitos **fora do CLI** (adição, edição, exclusão, renomeação e um changeset tocando dois projetos).
- Config de teste apontando para esses caminhos (`--config`).

### Roteiro

Itens marcados com **(auto)** têm teste opt-in em `tests/PEPCliHelper.IntegrationTests` (tf.exe real, sem check-in/undo; esquema e preparação no README do projeto). Sem a variável, os testes são ignorados:

```powershell
$env:PEPCLI_IT_CONFIG='C:\TestePEP\pepcli-it.json'; dotnet test tests\PEPCliHelper.IntegrationTests
```

Com fixtures do H1 em `tests/fixtures/tf-ptbr/`, os testes `TfFixtureTests` deixam de ser ignorados em `dotnet test`.

| # | Cenário | Comando | Esperado | OK |
|---|---|---|---|---|
| H1 | Formatos pt-BR | `.\scripts\capture-tf-fixtures.ps1 -Changeset <id> -SourceServer <$/...> -TargetLocal <C:\...> -TargetServer <$/...>` (somente leitura) | Saídas reais em `tests/fixtures/tf-ptbr/`; testes de fixture passam | ☐ |
| H2 | Opções aceitas | idem | Nenhum exit 2 (confirmar `/noimplicitbaseless`, `/format:detailed`, `/preview` do resolve) | ☐ |
| H3 | Merge 2 destinos **(auto)** | `pep merge --source atual --target A --target B --changeset <edit>` | Aplicado com pending changes nos dois, **nenhum changeset novo no servidor** | ☐ |
| H4 | Add / delete / rename **(auto)** | changesets de fixture | Pending changes corretas (add, delete, rename) | ☐ |
| H5 | Um changeset específico **(auto)** | changeset N com N-1 não integrado | Somente N aplicado (checar `tf merges`) | ☐ |
| H6 | Já integrado **(auto)** | repetir H3 após check-in manual | Já integrado, nada reaplicado | ☐ |
| H7 | Conflito **(auto)** | editar o mesmo trecho em A e fazer check-in | Aplicado com conflitos, exit 5, conflito listado, não resolvido | ☐ |
| H8 | Sem relação **(auto)** | `--target SemRelacao` | Bloqueado, sem baseless | ☐ |
| H9 | Sem mapeamento / cloaked **(auto: sem mapeamento)** | pasta sem mapeamento; subpasta cloaked | Bloqueado com orientação | ☐ |
| H10 | Pending no escopo **(auto)** | editar com checkout arquivo do changeset em A | Bloqueado, alteração preservada | ☐ |
| H11 | Gravável sem checkout | tirar read-only e editar em workspace de servidor | Bloqueado | ☐ |
| H12 | Workspace local | repetir H3 e H10 no workspace local | Comportamento correto; aviso de limitação | ☐ |
| H13 | Falha parcial | tornar B inválido após o plano (ex.: remover mapeamento) | A aplicado, B bloqueado na revalidação, exit 5 | ☐ |
| H14 | Dry-run **(auto)** | `--dry-run` | Nenhuma pending change criada (`tf status` antes/depois igual) | ☐ |
| H15 | Cancelamento | Ctrl+C durante merge grande | Cancelado, estado inspecionado, sem rollback | ☐ |
| H16 | Get | `pep get version A` com arquivo editado localmente | Não sobrescreve; parcial quando aplicável | ☐ |
| H17 | Build | `pep build version A` com host aberto e fechado | Bloqueia / compila | ☐ |
| H18 | Kill host | dois RM.Host de versões diferentes | Só o escolhido encerra | ☐ |
| H19 | Ausência de check-in | após todos os cenários | `tf history` da branch de teste sem changesets do CLI | ☐ |

## 3b. Checklist manual do terminal (U05)

Executar em **Windows Terminal**, **PowerShell 5.1 (conhost)** e **cmd**. Use `--config` com um catálogo de teste.

| # | Verificação | WT | PS | cmd |
|---|---|---|---|---|
| M1 | `pep` mostra banner colorido legível e o menu navega com setas/Enter | ☐ | ☐ | ☐ |
| M2 | Em todos os submenus, "← Voltar" retorna ao menu principal | ☐ | ☐ | ☐ |
| M3 | Merge guiado: "Cancelar" na seleção de projeto encerra sem alterar nada | ☐ | ☐ | ☐ |
| M4 | Multisseleção de destinos: Espaço marca, Enter confirma; nada marcado cancela | ☐ | ☐ | ☐ |
| M5 | Confirmação final responde "n" ⇒ "Merge cancelado… Nada foi alterado" | ☐ | ☐ | ☐ |
| M6 | Spinner aparece em operações longas e não deixa lixo na tela | ☐ | ☐ | ☐ |
| M7 | Ctrl+C durante uma consulta volta ao menu com aviso de cancelamento | ☐ | ☐ | ☐ |
| M8 | `pep --ascii` e `pep --no-color`: sem caracteres quebrados e estados legíveis sem cor | ☐ | ☐ | ☐ |
| M9 | `pep merge ... --dry-run > saida.txt`: arquivo sem sequências ANSI | ☐ | ☐ | ☐ |
| M10 | Tabelas não quebram de forma ilegível em janela de 120 colunas | ☐ | ☐ | ☐ |

## 4. Registro de execução

| Data | Quem | Automatizados | Smoke | Homologação TFVC | Observações |
|---|---|---|---|---|---|
| 2026-09-12 | Implementação inicial + correções da revisão | 142/142 (Debug e Release) | ok (seção 2) + publicação `pep.exe` validada | não executada | Servidor indisponível na máquina de dev; menu interativo (setas/prompts) com validação manual pendente |
| 2026-09-13 | T01–T07: testes interativos e "Cancelar" (130), code page ANSI, script de captura somente leitura, fixtures, integração opt-in + revisão final | unitários 154 ok / 7 ignorados; integração 32 ok / 11 ignorados; 0 avisos | script de captura: 8 comandos de leitura (servidor indisponível, exit 100) | não executada | Revisão final endureceu travas (script por lista de verbos permitidos; integração recusa UNC/`\\?\`/8.3/`..` e resolve sem preview); H1 e M1–M10 pendentes |

## 5. Riscos conhecidos

- Parsers baseados em caminhos e números podem não cobrir alguma saída real do tf.exe em pt-BR (mitigação: saída não reconhecida ⇒ bloqueado/indeterminado; H1).
- `/noimplicitbaseless` e `/format:detailed` precisam ser confirmados na versão 17.14 do tf.exe (H2).
- Detecção de alteração não reconciliada em workspace local é limitada (aviso exibido; H12).
- .NET 9 encerra suporte em 10/11/2026: planejar migração para .NET 10 LTS.
