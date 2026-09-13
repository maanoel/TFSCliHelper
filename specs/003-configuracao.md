# 003 — Configuração

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
Versões, caminhos locais, caminhos TFVC, coleção e ferramentas estão fixos no código. Cada rotação de versão exige recompilar.

## Comando
```
pep config init [--force]     cria o arquivo com defaults seguros (backup se já existir e --force)
pep config show [--json]      exibe configuração efetiva e caminho do arquivo
pep config validate [--json]  valida estrutura e regras
```

## Regras
- Local: `%APPDATA%\PepCli\config.json`.
- **Precedência única:** 1) argumentos explícitos (`--config`, opções do comando) → 2) variável `PEPCLI_CONFIG` / arquivo do usuário → 3) defaults seguros.
- Modelo: `raizLocal`, `colecao`, `ferramentas` (`tfExe`, `msBuild`, `editor`), `versoes[]` (`id`, `atual`, `ativa`, `caminhoLocal`, `caminhoServidor`, `aliases`, `workspace`), `projetos[]` (`alias`, `nome`, `pastaLocal`, `pastaServidor`, `solucao`), `arquivosLocais` (`broker`, `host`, `rm`, `alias`, `hostConfig`), `apresentacao` (`semCor`, `ascii`).
- Defaults seguros: raiz `C:\Linha-RM`; projetos `back` (Sau-PEP, `RM.Pep.sln`) e `sau` (Sau-Saude, `Sau-Saude.sln`); arquivos em `Bin\`; nenhuma versão (catálogo vem de `env discover/configure`).
- Senhas ou tokens **não** são aceitos na configuração; autenticação é a do tf.exe/Visual Studio. `config show` mascara qualquer campo com nome de segredo.
- Gravação atômica (arquivo temporário + substituição) com backup `.bak`.
- Config ausente: comandos dependentes param com erro e orientam `pep config init`. Nada é criado silenciosamente.
- JSON inválido ou regra violada: exit 2 com lista de erros.

## Cenários de aceite
- **Dado** nenhum arquivo **Quando** `config init` **Então** cria o arquivo com defaults e informa o caminho.
- **Dado** arquivo existente **Quando** `config init` sem `--force` **Então** não altera nada e orienta.
- **Dado** `--config D:\x\cfg.json` **Quando** qualquer comando **Então** usa esse arquivo, ignorando o do usuário.
- **Dado** config com caminho local em `caminhoServidor` **Quando** `config validate` **Então** erro apontando o campo.

## Fora de escopo
- Configuração compartilhada em rede; criptografia de segredos (não há segredos).

## Decisões em aberto
Nenhuma.

## Plano
- [x] Modelo, defaults e validação
- [x] Store com precedência, gravação atômica e backup
- [x] Comandos init/show/validate

## Verificação
Ver `docs/HOMOLOGACAO.md`.
