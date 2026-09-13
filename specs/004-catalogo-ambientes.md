# 004 — Catálogo e ambientes

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
Novas versões surgem e antigas saem de uso. A máquina de referência tem 10 pastas em `Legado`, mas a equipe opera no máximo 4 legadas. O CLI precisa de um catálogo ativo explícito e de rotação sem mover arquivos.

## Comando
```
pep env list [--json]
pep env discover [--json]          somente leitura: propõe candidatos
pep env configure                  seleção interativa da atual e até 4 legadas; grava com confirmação
pep env configure --atual <id> --legado <id>... --yes   forma não interativa
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
- Rotação via `env configure`: cadastrar nova versão, trocar a atual, manter a antiga atual como legada se desejado, desativar legadas. Só altera o catálogo; nunca move/exclui pastas nem altera branches.
- Gravação exige revisão da tabela final e confirmação; backup do arquivo anterior.
- Versões desativadas ficam no arquivo (`ativa: false`) e deixam de participar de operações `all`.

## Cenários de aceite
- **Dado** 10 pastas legadas **Quando** `env discover` **Então** lista todas como candidatas sem alterar nada.
- **Dado** `env configure` com 5 legadas marcadas **Quando** confirmar **Então** é recusado com mensagem do limite.
- **Dado** nova versão cadastrada como atual **Quando** salvar **Então** a anterior pode virar legada; nenhum diretório é excluído.
- **Dado** token `06` **Quando** resolvido **Então** erro de versão desconhecida.
- **Dado** aliases iguais em duas versões **Quando** carregar **Então** configuração inválida.

## Fora de escopo
- Criar workspaces ou mapeamentos.

## Decisões em aberto
Nenhuma.

## Plano
- [x] VersionCatalog e resolução
- [x] Descoberta somente leitura
- [x] env list/discover/configure/validate

## Verificação
Ver `docs/HOMOLOGACAO.md`.
