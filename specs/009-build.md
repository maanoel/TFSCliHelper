# 009 — Build

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
O build atual usa caminho fixo do MSBuild do VS 2022 Professional, mata o `rm.host.exe` sem perguntar e não reporta falhas.

## Comando
```
pep build [--version <versao>]... | [--all] [--project <alias>]... [--configuration <cfg>] [--continue-on-failure] [--dry-run] [--json]
pep build all [--project <alias>]... [...]
pep build version <versao> [--project <alias>]... [...]
```

## Regras
- Ferramenta real: **MSBuild** do Visual Studio (legado usava `msbuild <sln>`). Não substituir por `dotnet build`.
- Soluções: `Sau-PEP\RM.Pep.sln` (PEP, `back`) e `Sau-Saude\Sau-Saude.sln` (Saúde, `sau`).
- **Decisão do usuário (2026-09-14):** o usuário seleciona versões e projetos; o PEP pode ser compilado separado do Saúde; o projeto **principal** (PEP) compila sempre primeiro. Substitui a ordem legada "sau depois back".
- Ordem: versões atual → legadas (catálogo); em cada versão, o principal primeiro e os demais selecionados na ordem da config (`BuildOrder`, Core).
- Principal: campo `principal` do projeto (no máximo um). Sem nenhum marcado, `back` é o principal (único default, em `VersionCatalog.FromConfig`); a config não é reescrita.
- `pep build` sem `--version`/`--all`: interativo pergunta versões (atual marcada) e projetos (todos marcados, exceto Sau-Saúde, desmarcado); nada marcado = cancelado (130). Não interativo ⇒ exit 2. `--all` com `--version` ⇒ exit 2.
- **Sem confirmação (decisão do usuário, 2026-09-14):** após a seleção, mostra o plano e compila direto, também em modo não interativo (build não altera TFVC nem descarta nada; Ctrl+C interrompe). Nenhum `--yes` é necessário.
- Menu "Build (MSBuild)" despacha `pep build` / `pep build --dry-run`.
- Configuração: default do projeto; `--configuration` repassa `/p:Configuration=<cfg>`.
- Argumentos: `<sln> /nologo /verbosity:minimal`. Sem `/m` por padrão (saídas compartilhadas em `Bin`).
- Sequencial sempre; nunca paralelo entre versões.
- **Build não executa get.**
- Pré-verificação: solução existe; MSBuild localizado; `RM.Host.exe` rodando a partir da versão ⇒ aviso no plano. **Decisão do usuário (2026-09-14):** antes do build, encerra esses hosts com a rotina de `pep kill host` (graciosa, sem confirmação, só hosts com caminho dentro da versão compilada). Se não encerrar, a versão fica bloqueada e orienta `pep kill host --pid <n> --force` (nunca força sozinho). `--dry-run` não encerra nada.
- Para no primeiro build com falha, salvo `--continue-on-failure`.
- Mostra etapa atual e última linha de saída; duração por solução.
- Exit: 0 · 3 bloqueio · 4 falha · 5 parcial · 130 cancelado.

## Cenários de aceite
- **Dado** host da versão em execução **Quando** `build version atual` **Então** encerra o host de forma graciosa e compila; se o host não encerrar, bloqueia a versão e orienta `--force`, sem forçar.
- **Dado** primeira solução falha **Quando** build all **Então** demais não iniciadas, exit 4.
- **Dado** MSBuild ausente **Quando** build **Então** erro de ferramenta ausente, exit 3.
- **Dado** config legada com `sau` antes de `back` **Quando** build de todos os projetos **Então** `RM.Pep.sln` antes de `Sau-Saude.sln` em cada versão.
- **Dado** seleção só de `sau` **Quando** build **Então** compila apenas `Sau-Saude.sln`.

## Fora de escopo
- Restore NuGet explícito; build de front.

## Decisões em aberto
Nenhuma.

## Plano
- [x] Localizador MSBuild
- [x] BuildUseCase
- [x] Chains `build all` / `build version`
- [x] `pep build` com seleção de versões/projetos e projeto principal primeiro (2026-09-14)

## Verificação
Ver `docs/HOMOLOGACAO.md`.
