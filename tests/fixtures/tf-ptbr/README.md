# Fixtures reais do tf.exe (pt-BR)

Saídas capturadas por `scripts/capture-tf-fixtures.ps1` (somente comandos de leitura/preview).

| Arquivo | Comando | Usado por |
|---|---|---|
| `workspaces.txt` | `tf workspaces /collection` | doctor, workspace list |
| `workfold.txt` | `tf workfold <pasta>` | WorkfoldParser / MappingResolver |
| `status-detailed.txt` | `tf status /recursive /format:detailed` | pending changes |
| `resolve-preview.txt` | `tf resolve /recursive /preview` | conflitos |
| `changeset.txt` | `tf changeset /noprompt` | escopo do changeset |
| `merge-candidate.txt` | `tf merge /candidate` | elegibilidade |
| `merge-preview.txt` | `tf merge /preview /noimplicitbaseless` | preview |
| `get-preview.txt` | `tf get /recursive /preview` | atualização local |

Cada arquivo começa com um cabeçalho `# tf ...`, `# exitCode`, `# codePage`. Enquanto a pasta não tiver `.txt`, os testes de fixture são ignorados.

Revise e anonimize antes de versionar.
