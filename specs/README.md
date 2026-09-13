# Specs — Novo PEP CLI

Fonte: *SPEC KIT — NOVO PEP CLI* (documento completo da equipe). Estas specs quebram o documento em entregas implementáveis.

> **O PEP CLI realiza o merge e mantém as alterações em Pending Changes. A revisão, resolução de conflitos e o check-in são responsabilidades do usuário.**

## Índice

| # | Spec | Etapa | Status |
|---|---|---|---|
| 001 | [Plataforma e estrutura](001-plataforma-estrutura.md) | Base | implementada |
| 002 | [Identidade visual e terminal](002-identidade-terminal.md) | Experiência | implementada |
| 003 | [Configuração](003-configuracao.md) | Base | implementada |
| 004 | [Catálogo e ambientes](004-catalogo-ambientes.md) | Base | implementada |
| 005 | [Execução de processos e adapter TFVC](005-execucao-tfvc.md) | Base | implementada |
| 006 | [Merge — plano, pré-verificação e dry-run](006-merge-plano.md) | Merge seguro | implementada |
| 007 | [Merge — execução e resultado](007-merge-execucao.md) | Merge seguro | implementada |
| 008 | [Get](008-get.md) | Preservados | implementada |
| 009 | [Build](009-build.md) | Preservados | implementada |
| 010 | [Ferramentas locais](010-ferramentas-locais.md) | Preservados | implementada |
| 011 | [Diagnóstico e consultas TFVC](011-diagnostico-consultas.md) | Suporte | implementada |
| 012 | [Histórico, logs e códigos de saída](012-historico-saida.md) | Suporte | implementada |
| 013 | [Compatibilidade com comandos antigos](013-compatibilidade.md) | Preservados | implementada |
| 014 | [Documentação, distribuição e homologação](014-documentacao-distribuicao.md) | Validação | implementada |

Aprovadas pelo usuário e implementadas em 2026-09-12. **Homologação TFVC pendente** — ver [docs/HOMOLOGACAO.md](../docs/HOMOLOGACAO.md).

## Decisões registradas

| Decisão | Motivo |
|---|---|
| Somente backend: `back` = Sau-PEP, `sau` = Sau-Saude. Front **não implementado**. | Front do PEP migrou para o Git (`_git/pep`); decisão do usuário. |
| SDK .NET **9.0.306** fixo em `global.json`, TFM `net9.0`. | Determinação obrigatória da spec. Risco registrado: .NET 9 encerra suporte em 10/11/2026. |
| Integração TFVC via **tf.exe** (não API .NET). | A API com workspace/merge (`ExtendedClient`) é só .NET Framework; REST não faz merge local. |
| **Spectre.Console** só para apresentação; parsing próprio no padrão Chain/Builder. | Evita parser concorrente e preserva o padrão de commands da equipe. |
| **xUnit** para testes. | Framework já presente no cache NuGet da máquina, padrão de mercado em .NET, sem dependência extra. |
| Executável `pep`, marca **PEP CLI**. | Nome curto para digitar; banner de identidade no estilo Angular CLI. |
| Sem container de DI; composição manual em `AppServices`. | CLI pequeno; evita arquitetura excessiva. |
| Parsers de tf.exe tolerantes a idioma; saída não reconhecida ⇒ **indeterminado**, nunca sucesso. | tf.exe da equipe responde em pt-BR; sem validação real ainda. |
| Pendência: migrar para .NET 10 LTS antes de 10/11/2026. | Fim de suporte do .NET 9. |
