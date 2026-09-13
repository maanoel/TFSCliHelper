# Tarefas — PEP CLI

Acompanhamento do que falta até o CLI poder ser declarado pronto para produção.
Legenda: ✅ concluída · 🔄 em andamento · ⏳ a fazer · ⛔ bloqueada · 👤 depende do usuário/equipe

Última atualização: 2026-09-13 (desenvolvimento concluído; restam tarefas do usuário)

## Resumo

| Situação | Qtde |
|---|---|
| ✅ Concluídas | 14 specs implementadas, 186 testes (154 unitários/CLI + 32 de segurança da integração) + 7 de fixture + 11 de integração opt-in, revisão de segurança, publicação, script de captura |
| 🔄 / ⏳ Desenvolvimento (Claude) | ver seção 1 |
| 👤 Usuário / equipe | ver seção 2 |

## 1. Desenvolvimento

| ID | Tarefa | Spec | Responsável | Status | Depende de |
|---|---|---|---|---|---|
| T01 | Testes de terminal interativo com `Spectre.Console.Testing` (seleção, múltipla seleção, confirmação negada, cancelamento, menu Voltar/Sair) | 002, 020 do doc | Claude (subagente) | ✅ 8 testes | — |
| T02 | Opção **Cancelar** explícita nos prompts de merge e `env configure` (hoje só Ctrl+C ou seleção vazia) | 002 | Claude (subagente) | ✅ | — |
| T03 | Script `scripts/capture-tf-fixtures.ps1` que grava saídas reais do tf.exe usando **apenas comandos de leitura/preview** | 005, 014 | Claude | ✅ | — |
| T03b | Corrigir encoding da saída do tf.exe: evidência real mostrou ANSI 1252, não OEM 850 | 005 | Claude | ✅ | T03 |
| T04 | Testes de parser sobre fixtures reais (`tests/fixtures/tf-ptbr/`), ignorados enquanto a pasta estiver vazia | 005 | Claude (subagente) | ✅ 7 testes (ignorados até U02) | T03 |
| T05 | Projeto `tests/PEPCliHelper.IntegrationTests` com cenários H3–H19 automatizáveis, **opt-in** por variável de ambiente e só em coleção de teste | 014 | Claude (subagente) | ✅ 11 testes H3–H10, H14 (ignorados até U03) | T03 |
| T06 | Ajustar parsers com as saídas reais capturadas | 005 | Claude | ⛔ | U02 |
| T07 | Revisão final (`review-change`), atualização de docs e specs — sem bloqueios; 5 correções nas ferramentas de teste | — | Claude (subagente) | ✅ | T01–T05 |
| T08 | (opcional) Recusar na config de integração a URL da coleção da equipe e caminhos via junction/subst | — | Claude | ⏳ baixa prioridade | — |

## 2. Usuário / equipe

| ID | Tarefa | Status | Depende de |
|---|---|---|---|
| U01 | Autorizar commit (sugestão: branch `feature/novo-pep-cli`) — nada foi commitado ainda | 👤 | — |
| U02 | Com VPN ativa, rodar `scripts/capture-tf-fixtures.ps1` (somente leitura) e revisar/anonimizar as saídas. Comando no `docs/HOMOLOGACAO.md` (H1) | 👤 pronto para executar | ✅ T03 |
| U03 | Provisionar coleção/branches/workspaces **de teste** para homologação (ver `docs/HOMOLOGACAO.md` §3) | 👤 | — |
| U04 | Executar o roteiro H1–H19: automáticos com `$env:PEPCLI_IT_CONFIG='...'; dotnet test tests\PEPCliHelper.IntegrationTests`, demais manuais | 👤 | U03 |
| U05 | Validar o menu interativo manualmente em Windows Terminal, PowerShell e cmd (checklist em `docs/HOMOLOGACAO.md`) | 👤 | T01 |
| U06 | Decidir migração para **.NET 10 LTS** (.NET 9 encerra suporte em 10/11/2026) | 👤 | — |
| U07 | Publicar no drive da equipe e comunicar a migração (`docs/MIGRACAO.md`) | 👤 | U04 |

## 3. Concluídas

| ID | Tarefa |
|---|---|
| ✅ C01 | Research do CLI legado e do ambiente |
| ✅ C02 | AGENTS.md, template de spec e skills (`spec-feature`, `implement-spec`, `review-change`) |
| ✅ C03 | 14 spec kits (001–014) |
| ✅ C04 | Plataforma .NET 9.0.306, solução Core + CLI + testes |
| ✅ C05 | Identidade visual PEP CLI, menu, ajuda, `--json`, `--no-color`, `--ascii`, não interativo |
| ✅ C06 | Configuração, catálogo, descoberta e rotação de versões |
| ✅ C07 | Adapter tf.exe e executor de processos |
| ✅ C08 | Merge seguro (plano, dry-run, execução, estados, falha parcial) |
| ✅ C09 | Get, build, broker, host, kill host, doctor, workspace, pending, changeset, histórico |
| ✅ C10 | Compatibilidade com sintaxes antigas e comandos removidos |
| ✅ C11 | Documentação (README, docs/*) e `scripts/publish.ps1` |
| ✅ C12 | 142 testes xUnit |
| ✅ C13 | Revisão de segurança independente e correções |
| ✅ C14 | Publicação `pep.exe` validada |
| ✅ C15 | Instalador `PEPCLI-Setup` (janela única, por usuário, modo silencioso, 34 testes) + ícone CLI `assets/pep-cli.ico` |
