# Testes de integração TFVC (opt-in)

Executam `MergePlanner`/`MergeExecutor` com `TfExeClient` e `ProcessCommandExecutor` **reais** contra uma
coleção/branches **exclusivos de teste**. Automatizam parte do roteiro de `docs/HOMOLOGACAO.md` (H3–H10, H14).

- **Nunca rodam por padrão:** sem `PEPCLI_IT_CONFIG` apontando para um arquivo existente, todos são ignorados (exit 0).
- **Nunca fazem check-in, undo, shelve, baseless, `/force` ou alteração de workspace:** o `RecordingCommandExecutor`
  recusa esses argumentos antes de chamar o tf.exe.
- **Podem deixar pending changes e conflitos no workspace de teste.** A limpeza é manual pelo operador
  (Visual Studio ou `tf undo` executado por você, fora dos testes) antes de rodar de novo.

## Como executar

```powershell
$env:PEPCLI_IT_CONFIG = 'C:\TestePEP\pepcli-it.json'
dotnet test tests\PEPCliHelper.IntegrationTests
# um cenário: dotnet test tests\PEPCliHelper.IntegrationTests --filter "FullyQualifiedName~H7_"
Remove-Item Env:PEPCLI_IT_CONFIG
```

## Esquema do arquivo

```jsonc
{
  "colecaoDeTeste": true,                       // obrigatório: true confirma que tudo abaixo é de TESTE
  "collection": "https://servidor/ColecaoTeste",
  "tfExe": null,                                // opcional; null = localizar via vswhere
  "sourceServer": "$/Teste-PEP/atual/release",
  "sourceLocal": "C:\\TestePEP\\atual\\release",
  "targets": [                                  // ao menos 2, criados por branch da origem
    { "serverPath": "$/Teste-PEP/Legado/A", "localPath": "C:\\TestePEP\\Legado\\A" },
    { "serverPath": "$/Teste-PEP/Legado/B", "localPath": "C:\\TestePEP\\Legado\\B" }
  ],
  "noRelationTarget": { "serverPath": "$/Teste-PEP/Legado/SemRelacao", "localPath": "C:\\TestePEP\\Legado\\SemRelacao" },
  "unmappedTarget":   { "serverPath": "$/Teste-PEP/Legado/A", "localPath": "C:\\TestePEP\\SemMapeamento" },
  "changesets": {
    "edit": 101,
    "add": null, "delete": null, "rename": null,
    "singleChangeset": null,
    "alreadyIntegrated": null,
    "conflict": null,
    "pendingInScope": null
  }
}
```

Travas (o teste **falha** com mensagem clara, sem executar nada): `colecaoDeTeste` diferente de `true`,
`collection` vazia, menos de 2 destinos, caminho de servidor que não começa com `$/` ou começa com `$/Linha-RM`,
caminho de servidor com `.`/`..`, caminho local sem letra de unidade (relativo, UNC, `\\?\`), com `~` (nome 8.3)
ou dentro de `C:\Linha-RM`. Essas travas têm testes comuns (`IntegrationConfigSafetyTests`) que sempre rodam.

Entradas nulas fazem o cenário correspondente ser **ignorado**.

## Cenários e preparação manual

Os changesets de fixture são feitos fora do CLI e devem tocar **arquivos distintos** para não interferirem entre si.
Destino padrão: `targets[0]` (A), salvo indicação.

| Teste | Entradas | Preparação | Esperado |
|---|---|---|---|
| H3 | `edit` | changeset de edição na origem, não integrado em A e B; A e B sem pending changes | A e B *AppliedWithPendingChanges*, só comandos `merge` alteram, nenhum `checkin` |
| H4 | `add` / `delete` / `rename` | changesets de adição, exclusão e renomeação na origem | *AppliedWithPendingChanges* em A |
| H5 | `singleChangeset` | changeset N cujo anterior (N-1) na origem não foi integrado | só `/version:CN~CN`; novas pendências apenas do escopo de N |
| H6 | `alreadyIntegrated` | changeset já mesclado em A **com check-in manual** | *AlreadyIntegrated*, nenhum comando de alteração |
| H7 | `conflict` | editar em A, com check-in manual, o mesmo trecho alterado pelo changeset | *AppliedWithConflicts*, exit 5, conflito não resolvido |
| H8 | `edit`, `noRelationTarget` | branch criada por cópia (sem relação de merge), mapeada | *Blocked*, sem `/baseless` |
| H9 | `edit`, `unmappedTarget` | pasta local existente **sem** mapeamento no workspace | *Blocked* |
| H10 | `pendingInScope` | em A, checkout/edição de um arquivo do changeset (não integrado) | *Blocked*, `tf status` inalterado |
| H14 | `edit` | nenhuma | planejamento não executa comando de alteração; `tf status` de A e B igual antes/depois |

Observações:

- Executar H3 (ou H4/H5/H7) deixa pending changes/conflitos: uma segunda execução sem limpeza bloqueia e falha.
- H14 continua válido mesmo após H3 (compara apenas o estado antes/depois do plano).
- A confirmação de "nenhum changeset novo no servidor" (H19, `tf history`) continua manual: `ITfvcClient` não expõe histórico.
