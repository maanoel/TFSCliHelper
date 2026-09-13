---
name: review-change
description: Revisa as alterações pendentes do PEP CLI contra as regras de segurança, o padrão de commands e os princípios de teste do AGENTS.md. Use antes de commit/PR ou ao final de uma implementação.
---

# Revisar alteração

Escopo: `git diff` e arquivos novos (`git status`). Revise apenas o que mudou.

## 1. Segurança (bloqueante)

Procure no diff e confirme que nenhum fluxo novo:
- executa `checkin`, `/baseless`, `/force`, `undo`, `resolve` ou `taskkill /f` sem confirmação explícita;
- apaga/sobrescreve arquivos sem validar caminho e confirmar;
- concatena entrada do usuário em comando sem validação;
- trata falha parcial ou cancelamento como sucesso.

## 2. Padrão de commands

- Chain só declara caminho/ajuda e cria o builder; regra de negócio está no Core.
- Builder valida entrada, usa `Ui` (sem `Console` direto) e trata `--json`/não interativo.
- Sem novos caminhos ou versões fixos no código.
- Registrado em `ChainCatalog`; `docs/COMANDOS.md` atualizado.

## 3. Testes

- Build e testes executados de fato — informe o resultado real.
- Testes F.I.R.S.T: sem `tf.exe`, rede, disco real ou ordem entre testes.
- Cenários de aceite da spec cobertos ou justificados na seção Verificação.

## Saída

Lista curta por severidade: **bloqueante**, **deve corrigir**, **sugestão** — com `arquivo:linha` e correção proposta.
Sem achados relevantes: diga isso em uma linha. Não corrija automaticamente sem o usuário pedir.
