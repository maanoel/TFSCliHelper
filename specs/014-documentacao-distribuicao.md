# 014 — Documentação, distribuição e homologação

- **Status:** implementada (homologação TFVC pendente)
- **Autor / data:** equipe PEP RM / 2026-09-12

## Problema
O README atual cobre versões antigas e .NET 5. Não há instrução de instalação, migração, troubleshooting nem roteiro para validar a integração TFVC real.

## Comando
```
./scripts/publish.ps1    gera pacote em artifacts/pep
```

## Regras
- `README.md`: visão geral, destaque permanente de Pending Changes, pré-requisitos (.NET 9 runtime, VS 2022 com Team Explorer), instalação, primeiros passos, exemplos interativos e por argumentos, códigos de saída, limitações.
- `docs/COMANDOS.md`: referência completa.
- `docs/CONFIGURACAO.md`: modelo, precedência, catálogo, cadastrar nova versão, desativar legada.
- `docs/OPERACAO.md`: corrigir mapeamento, revisar pending changes, inspecionar falha parcial, retomar sem duplicar, localizar logs sem expor credenciais, troubleshooting.
- `docs/MIGRACAO.md`: sintaxes mantidas, alteradas e removidas.
- `docs/DISTRIBUICAO.md`: build, publicação, instalação e atualização.
- `docs/HOMOLOGACAO.md`: roteiro de validação em coleção/branches/workspaces **de teste** (adicionar, alterar, excluir, renomear, conflito, já integrado, workspace distinto, ausência de check-in), formatos de saída do tf.exe a confirmar e relatório de testes executados/não executados.
- Distribuição: `dotnet publish` framework-dependent `win-x64`, executável único `pep.exe`; script de publicação; plano de atualização (substituir pasta; config do usuário preservada em `%APPDATA%`).
- **Não declarar pronto para produção** antes da homologação TFVC.

## Cenários de aceite
- **Dado** um novo membro da equipe **Quando** segue o README **Então** instala, configura o catálogo e roda `pep doctor`.
- **Dado** `scripts/publish.ps1` **Quando** executado **Então** gera `artifacts/pep/pep.exe`.

## Fora de escopo
- Instalador MSI; atualização automática.

## Decisões em aberto
Nenhuma.

## Plano
- [x] README e docs
- [x] Script de publicação
- [x] Roteiro de homologação

## Verificação
Ver `docs/HOMOLOGACAO.md`.
