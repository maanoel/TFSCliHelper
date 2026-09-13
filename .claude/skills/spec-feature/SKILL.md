---
name: spec-feature
description: Cria ou refina a spec lean de uma nova feature ou comando do PEP CLI em specs/. Use quando o usuário pedir uma feature nova, um comando novo ou mudança de comportamento, antes de qualquer código.
---

# Spec de feature

Objetivo: transformar um pedido em `specs/NNN-slug.md` a partir de `specs/_template.md`. **Não escreva código nesta etapa.**

## Passos

1. Leia `AGENTS.md` e as specs existentes em `specs/` (evite duplicar ou conflitar).
2. Leia o código relacionado (chains e builders parecidos) para ancorar a spec no que existe.
3. Numere com o próximo `NNN` livre; slug curto em kebab-case.
4. Preencha o template com o que é conhecido. O que não for, vai para **Decisões em aberto** — não suponha.
5. Na seção **Regras**, avalie impacto de segurança contra as regras inegociáveis do `AGENTS.md`.
6. Escreva cenários de aceite verificáveis, incluindo ao menos um de erro/bloqueio.
7. Deixe **Plano** e **Verificação** vazios. Status `rascunho`.
8. Apresente ao usuário: resumo, decisões em aberto e perguntas objetivas.

## Critérios

- Spec cabe em uma tela ou pouco mais; corte o que não muda a implementação.
- Só o usuário muda o status para `aprovada`.
