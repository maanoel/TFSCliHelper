# 009 — Build

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
O build atual usa caminho fixo do MSBuild do VS 2022 Professional, mata o `rm.host.exe` sem perguntar e não reporta falhas.

## Comando
```
pep build all [--project back|sau] [--configuration <cfg>] [--continue-on-failure] [--dry-run] [--yes] [--json]
pep build version <versao> [...]
```

## Regras
- Ferramenta real: **MSBuild** do Visual Studio (legado usava `msbuild <sln>`). Não substituir por `dotnet build`.
- Soluções: `Sau-Saude\Sau-Saude.sln` e `Sau-PEP\RM.Pep.sln`. Ordem = ordem dos projetos na config (default: sau, depois back — ordem do legado).
- Configuração: default do projeto; `--configuration` repassa `/p:Configuration=<cfg>`.
- Argumentos: `<sln> /nologo /verbosity:minimal`. Sem `/m` por padrão (saídas compartilhadas em `Bin`).
- Sequencial sempre; nunca paralelo entre versões.
- **Build não executa get.**
- Pré-verificação: solução existe; MSBuild localizado; `RM.Host.exe` rodando a partir da versão ⇒ bloqueia e orienta `pep kill host` (não encerra sozinho).
- Para no primeiro build com falha, salvo `--continue-on-failure`.
- Mostra etapa atual e última linha de saída; duração por solução.
- Exit: 0 · 3 bloqueio · 4 falha · 5 parcial · 130 cancelado.

## Cenários de aceite
- **Dado** host da versão em execução **Quando** `build version atual` **Então** bloqueia e orienta, sem encerrar processo.
- **Dado** primeira solução falha **Quando** build all **Então** demais não iniciadas, exit 4.
- **Dado** MSBuild ausente **Quando** build **Então** erro de ferramenta ausente, exit 3.

## Fora de escopo
- Restore NuGet explícito; build de front.

## Decisões em aberto
Nenhuma.

## Plano
- [x] Localizador MSBuild
- [x] BuildUseCase
- [x] Chains `build all` / `build version`

## Verificação
Ver `docs/HOMOLOGACAO.md`.
