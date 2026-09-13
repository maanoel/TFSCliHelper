# 005 — Execução de processos e adapter TFVC

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
O legado escreve texto no stdin de um `cmd.exe` com `runas`: não lê saída, não conhece exit code e concatena argumentos. Sem resultado confiável não há merge seguro.

## Comando
Fundação para os demais. Sem comando próprio.

## Regras
**Executor**
- Executa o processo diretamente, com `ArgumentList` (sem shell, sem concatenar string).
- Retorna exit code, stdout, stderr, duração, cancelado.
- Lê saída na code page OEM do console (tf.exe em pt-BR).
- Cancelamento (Ctrl+C) encerra a árvore do processo e é reportado como cancelado.

**Localização de ferramentas**
- tf.exe: config `ferramentas.tfExe` → `vswhere` (Team Explorer) → erro claro. Nunca presumir PATH.
- MSBuild: config `ferramentas.msBuild` → `vswhere -find MSBuild\**\Bin\MSBuild.exe` → erro claro.
- Versão da ferramenta lida do arquivo (`FileVersionInfo`).

**Adapter TFVC (`ITfvcClient`)** — somente operações permitidas:
| Operação | tf.exe |
|---|---|
| Mapeamento efetivo | `workfold <pastaLocal>` |
| Workspaces | `workspaces /collection:<url>` |
| Changeset | `changeset <id> /collection:<url> /noprompt` |
| Pending changes | `status <pasta> /recursive` |
| Conflitos | `resolve <pasta> /recursive /preview` |
| Candidatos | `merge /candidate /recursive /version:C<id>~C<id> <origem> <destino>` |
| Preview de merge | `merge /preview /recursive /noimplicitbaseless /version:C<id>~C<id> <origem> <destino>` |
| Merge | `merge /recursive /noimplicitbaseless /version:C<id>~C<id> <origem> <destino>` |
| Preview de get / get | `get <pasta> /recursive [/preview]` |

- Todos com `/noprompt` quando a opção existir.
- **Não expõe** checkin, undo, shelve, baseless, `/force`, `/overwrite`, `/auto`, criação/alteração de workspace ou workfold.
- `C<id>~C<id>` representa **um changeset isolado**, nunca intervalo acumulado.
- Exit codes do tf.exe: `0` sucesso, `1` sucesso parcial, `2` comando não reconhecido, `100` nada executado.
- Classificação de erro: `TF400324` ⇒ servidor indisponível/rede; `TF30063` ⇒ autenticação/permissão; demais ⇒ falha com a mensagem original.
- Parsing tolerante a idioma, ancorado em caminhos (`$/…`, `C:\…`) e números. Saída não reconhecida ⇒ **indeterminado**, nunca sucesso.
- Resolução de mapeamento (lógica pura): mapeamento mais específico que cobre a pasta (inclusive ancestral), caminho de servidor efetivo, cloaking e divergência com o configurado. Diferencia pasta inexistente, não mapeada, cloaked, divergente, rede, autenticação.

## Cenários de aceite
- **Dado** tf.exe inexistente **Quando** operação TFVC **Então** erro "ferramenta ausente" com caminho e como configurar.
- **Dado** argumento com espaços **Quando** executado **Então** chega como um único argumento.
- **Dado** mapeamento em `C:\Linha-RM\Atual\Release` **Quando** validar `…\Release\Sau-PEP` **Então** é válido pelo ancestral.
- **Dado** `(cloaked) $/…/Sau-PEP` **Quando** validar **Então** estado cloaked.
- **Dado** saída com `TF400324` **Quando** classificada **Então** erro de rede.
- **Dado** a interface do adapter **Quando** inspecionada **Então** não há método de check-in.

## Fora de escopo
- API .NET do TFVC; login/PAT.

## Decisões em aberto
Nenhuma. Formatos reais de saída entram na homologação (spec 014).

## Plano
- [x] Executor e localizadores
- [x] Adapter e classificação
- [x] MappingResolver e parsers com testes

## Verificação
Ver `docs/HOMOLOGACAO.md`.
