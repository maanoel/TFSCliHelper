# 011 — Diagnóstico e consultas TFVC

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
Quando algo falha, o usuário não tem como verificar ambiente, ferramentas e mapeamentos sem abrir o Visual Studio.

## Comando
```
pep doctor [--json]
pep workspace list [--json]
pep workspace inspect <versao> [--project back|sau] [--json]
pep pending list [<versao>] [--project back|sau] [--json]
pep changeset show <id> [--project back|sau --source <versao>] [--json]
```

## Regras
- Todos **somente leitura**. Doctor **não corrige** nada; cada falha traz o próximo passo.
- Doctor verifica, com estado OK / AVISO / FALHA / N/A:
  - configuração carregada e válida;
  - tf.exe e MSBuild localizados e versão;
  - coleção configurada; conexão e autenticação (`tf workspaces`);
  - catálogo: atual + legadas ativas;
  - por versão × projeto: pasta, mapeamento efetivo, cloaking, divergência;
  - soluções para build;
  - espaço livre no disco da raiz (aviso < 5 GB);
  - pasta de histórico gravável.
- `workspace list`: workspaces da coleção (saída do tf apresentada de forma legível).
- `workspace inspect`: mapeamento efetivo, workspace, caminho de servidor esperado × efetivo, diagnóstico.
- `pending list`: pending changes por versão/projeto (todas ativas se versão omitida).
- `changeset show`: cabeçalho e itens; com `--project/--source`, classifica incluídos e excluídos do escopo.
- Exit doctor: 0 sem falhas · 3 com falhas.
- Login automático do TFS (decisão do usuário, 2026-09-14): não há comando `login`. Quando qualquer operação TFVC retorna `TF30063`
  em terminal interativo, o CLI roda `tf workspaces /collection:<coleção>` sem `/noprompt` no terminal do usuário, confirma com
  `tf workspaces` e repete a operação uma vez. No máximo uma tentativa de login por execução (inclusive com destinos em paralelo).
  `tf merge`/`tf get` só são repetidos se a saída não citar itens (nada processado). Sem terminal interativo (`--json`,
  `--non-interactive`) não há login: falha com orientação. O CLI não recebe nem grava senha/token.

## Cenários de aceite
- **Dado** tf.exe sem credencial (TF30063) em terminal interativo **Quando** `doctor` ou `merge --dry-run` **Então** o login do TFS abre uma única vez e a operação continua.
- **Dado** servidor indisponível **Quando** `doctor` **Então** FALHA em conexão com orientação (VPN/proxy) e demais checks locais continuam.
- **Dado** pasta cloaked **Quando** `workspace inspect 2606 --project back` **Então** estado cloaked explicado.
- **Dado** `changeset show 861799 --project back --source atual` **Quando** itens de outros projetos **Então** aparecem como excluídos.

## Fora de escopo
- Correção automática; criação de workspace.

## Decisões em aberto
Nenhuma.

## Plano
- [x] DoctorUseCase
- [x] Chains de consulta

## Verificação
Ver `docs/HOMOLOGACAO.md`.
