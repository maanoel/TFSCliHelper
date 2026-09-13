---
name: implement-spec
description: Implementa uma spec aprovada de specs/ seguindo o padrão Chain/Builder/Executor e testes F.I.R.S.T. Use quando o usuário pedir para implementar uma spec ou um comando já especificado.
---

# Implementar spec

## Pré-condição

- A spec existe e está com status `aprovada`, sem decisões em aberto. Se não estiver, pare e informe.

## Passos

1. Leia `AGENTS.md` e a spec inteira.
2. Preencha a seção **Plano** da spec com passos pequenos e mostre ao usuário antes de codar.
3. Implemente no padrão existente (ver AGENTS.md):
   - chain em `CommandChain/` declarando caminho, ajuda e opções;
   - builder em `CommandsBuilders/` herdando `CommandBuilderBase` (valida, chama o Core, renderiza humano e `--json`);
   - regra de negócio no `PEPCliHelper.Core`;
   - registro em `CommandChain/ChainCatalog.cs`.
4. Escreva testes junto (F.I.R.S.T): `FakeTfvcClient`/`InMemoryFileSystem`/`CliHarness`, sem `tf.exe`, rede ou `C:\Linha-RM`.
   Cubra cada cenário de aceite testável em unidade, incluindo o de erro.
5. Atualize `docs/COMANDOS.md` (e `docs/HOMOLOGACAO.md` se mudar interação com tf.exe).
6. Rode build e testes. Marque os itens do **Plano** concluídos.
7. Preencha **Verificação** (testado / não testado e por quê) e mude o status para `implementada`.
8. Execute a skill `review-change` antes de declarar concluído.

## Não fazer

- Não ampliar escopo além da spec; divergências viram pergunta ao usuário.
- Não commitar sem pedido explícito.
