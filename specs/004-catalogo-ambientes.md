# 004 — Catálogo e ambientes

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
Novas versões surgem e antigas saem de uso. A máquina de referência tem 10 pastas em `Legado`, mas a equipe opera no máximo 4 legadas. O CLI precisa de um catálogo ativo explícito e de rotação sem mover arquivos.

## Comando
```
pep env list [--json]
pep env discover [--json]          somente leitura: propõe candidatos
pep env validate [--json]          valida pastas e mapeamentos TFVC do catálogo ativo
```

## Regras
- Catálogo ativo: exatamente **uma atual** e **no máximo 4 legadas**; menos de 4 é válido e informado.
- Resolução de versão: id exato, alias exato ou último segmento numérico exato (`2606` → `12.1.2606`). Sem `EndsWith`, sem aproximação. Ambíguo ⇒ erro listando candidatos. Versão inativa ⇒ erro informando que está desativada.
- Descoberta (somente leitura):
  - candidatos em `<raiz>\Atual\*` e `<raiz>\Legado\*` que contenham pasta de algum projeto;
  - consulta `tf workfold` para obter caminho de servidor efetivo; sem resposta, aplica a convenção `<raizServidor>/<pasta relativa>` (ajuste do usuário em 2026-09-13), confirmável com `env validate`;
  - distingue pasta, versão sugerida (nome da pasta é pista), mapeamento e caminho de servidor;
  - aponta ambiguidades (sem mapeamento, mapeamento divergente, cloaked).
  - **nunca** executa get, merge, exclusão, encerramento de processo nem altera workspace.
- Mais de 4 candidatas legadas: exige seleção; não ordena lexicograficamente para escolher.
- Rotação via `pep config auto` (pelas pastas). Só altera o catálogo; nunca move/exclui pastas nem altera branches.
- Gravação com backup do arquivo anterior.
- Versões desativadas ficam no arquivo (`ativa: false`) e deixam de participar de operações `all`.

## Decisão do usuário — 2026-09-14: configuração automática
- O setup inicial tinha passos demais. **Atualização (2026-09-14):** na primeira execução (`pep` ou qualquer comando, exceto ajuda e `config ...`), sem configuração ou sem versão atual, a configuração automática é **aplicada sem interação** e o usuário é avisado de que o PEP CLI está pronto. Também disponível por argumento como `pep config auto [--yes] [--force]`. O submenu "Ambientes e versões" não tem opções de configuração (só Listar catálogo, Descobrir versões e Validar mapeamentos).
- Convenção: atual sempre em `<raiz>\Atual\Release` (id `atual`); legadas são pastas numeradas em `<raiz>\Legado`.
- **No modo automático**, as 4 legadas mais novas por **ordem numérica de versão** ficam ativas e as mais antigas são cadastradas desativadas. Isso substitui, apenas para esse modo, a regra "não ordena para escolher" acima.
- Caminhos TFVC pela convenção, sem consultar o servidor; validação posterior com `env validate`. Configuração inválida nunca é sobrescrita.
- **Não existe configuração manual (decisão do usuário, 2026-09-14):** `env configure` e `config init` foram removidos (exit 2, orientam `pep config auto`). Configuração inválida só é recriada com `pep config auto --force` (backup).

## Cenários de aceite
- **Dado** 10 pastas legadas **Quando** `env discover` **Então** lista todas como candidatas sem alterar nada.
- **Dado** nenhuma configuração e `Atual\Release` existente **Quando** abrir `pep` **Então** grava a configuração sem perguntas e avisa que está pronto.
- **Dado** nenhuma configuração e sem `Atual\Release` **Quando** abrir `pep` **Então** nada é gravado e o aviso orienta `pep config auto`.
- **Dado** `pep env configure` **Quando** executado **Então** exit 2 orientando `pep config auto`.
- **Dado** token `06` **Quando** resolvido **Então** erro de versão desconhecida.
- **Dado** aliases iguais em duas versões **Quando** carregar **Então** configuração inválida.

## Fora de escopo
- Criar workspaces ou mapeamentos.

## Decisões em aberto
Nenhuma.

## Plano
- [x] VersionCatalog e resolução
- [x] Descoberta somente leitura
- [x] env list/discover/validate (configure removido em 2026-09-14)
- [x] Primeira execução automática sem interação (2026-09-14)

## Verificação
Ver `docs/HOMOLOGACAO.md`.
