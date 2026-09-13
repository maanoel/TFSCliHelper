# 010 — Ferramentas locais

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
`delete broker` apaga sem confirmar, `kill host` encerra todos os `rm.host.exe` à força e `open host` abre instâncias sem avaliar se já existe uma.

## Comando
```
pep delete broker <versao> [--no-backup] [--yes]
pep open host <versao> [--new-instance]
pep open rm <versao>
pep open alias <versao>
pep open hostconfig <versao>
pep kill host [--pid <n>]... [--version <versao>] [--force] [--yes] [--json]
```

## Regras
**delete broker**
- Arquivo: `<versao>\Bin\_Broker.dat` (config `arquivosLocais.broker`). Nunca diretório, nunca recursivo.
- Valida que o caminho resolvido está dentro da pasta da versão.
- Ausente ⇒ "nenhuma ação necessária", exit 0.
- Host/RM da versão em execução ou arquivo bloqueado ⇒ bloqueia (exit 3) com orientação.
- Backup `_Broker.dat.bak-<data>` por padrão; confirmação antes.

**open host / rm / alias / hostconfig**
- Executável/arquivo resolvido pela config (`Bin\RM.Host.exe`, `Bin\RM.exe`, `Bin\Alias.dat`, `Bin\RM.Host.exe.config`); diretório de trabalho = pasta do executável.
- Já existe `RM.Host.exe` rodando do mesmo caminho ⇒ interativo pergunta; não interativo exige `--new-instance`.
- Editor: `ferramentas.editor` (default `notepad++`); não encontrado ⇒ erro com orientação.

**kill host**
- Lista candidatos `RM.Host`: PID, caminho (quando acessível), início, versão inferida e confiança (alta: caminho dentro da versão; baixa: dentro da raiz; desconhecida: caminho inacessível).
- Exige alvo: seleção interativa, `--pid` ou `--version`. A seleção na lista (Espaço + Enter) já é a confirmação do encerramento gracioso (ajuste do usuário em 2026-09-13). Não interativo com vários candidatos e sem alvo ⇒ exit 2.
- Gracioso primeiro (fechar janela principal, aguarda); se não encerrar, informa.
- Forçado só com `--force` + aviso próprio + confirmação própria (`--yes` só em não interativo junto de `--force`).
- Nunca `taskkill /im` por nome.

## Cenários de aceite
- **Dado** 2 processos RM.Host **Quando** `kill host --non-interactive` sem alvo **Então** exit 2 e nada encerrado.
- **Dado** broker ausente **Quando** `delete broker 2606` **Então** informa nenhuma ação, exit 0.
- **Dado** broker presente **Quando** confirmado **Então** cria backup e remove apenas o arquivo.
- **Dado** host já rodando **Quando** `open host atual --non-interactive` **Então** exit 3 orientando `--new-instance`.

## Fora de escopo
- `kill all` e `cmd` (removidos — spec 013).

## Decisões em aberto
Nenhuma.

## Plano
- [x] ProcessInspector e FileSystem
- [x] Casos de uso e chains

## Verificação
Ver `docs/HOMOLOGACAO.md`.
