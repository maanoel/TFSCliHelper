# Migração do CLI antigo (PEPCliHelper) para o PEP CLI

O antigo executável abria um prompt onde se digitavam comandos. O novo `pep` recebe o comando por argumento ou abre um menu.

| Antes | Agora | Mudança de segurança |
|---|---|---|
| `get all` | `pep get all` | Só versões **ativas** do catálogo; valida mapeamentos; plano e confirmação; sem FrameHTML |
| `get version 2209` | `pep get version 2606` | Versões vêm do catálogo configurável |
| `build all` / `build version X` | `pep build all` / `pep build version X` | Não mata mais o RM.Host: bloqueia e orienta `pep kill host` |
| `merge back atual 861799` | `pep merge back atual 861799` (aceito) ou `pep merge --project back --source atual --target … --changeset 861799` | A versão é a **origem**; destinos precisam ser escolhidos (antes: todas as outras, com **/baseless**). Nunca baseless. Aviso de depreciação exibido |
| `merge front …` | — | **Removido**: front migrou para Git |
| `delete broker X` | `pep delete broker X` | Confirmação, backup e verificação de processos/arquivo bloqueado |
| `open host X` | `pep open host X` | Avisa se já existe instância (`--new-instance`) |
| `open rm X`, `open alias X`, `open hostconfig X` | iguais com `pep` | Arquivos resolvidos pela configuração |
| `kill host` | `pep kill host` | Antes: `taskkill /f /im rm.host.exe` (todos, forçado). Agora: seleção explícita, gracioso por padrão, `--force` com confirmação própria |
| `kill all` | — | **Removido**: encerrava `rm.*` pelo nome |
| `cmd <comando>` | — | **Removido**: executava shell arbitrário |
| `open front X`, `open podoc` | — | **Removidos**: front fora do escopo |
| `clear`, `cls`, `exit` | menu (`pep`) → Sair | Prompt digitado substituído pelo menu |
| Versões `32`, `33`, `34`, `2205`, `2209` fixas no código | `pep env configure` | Catálogo configurável, máximo 4 legadas |

Comandos removidos retornam **código 2** com o motivo e a alternativa; nenhum script antigo passa a executar outra coisa silenciosamente.

## Scripts

- Para não ter prompts, adicione `--non-interactive` (ou `--json`).
- Operações que alteram algo exigem `--yes` em modo não interativo.
- Use os códigos de saída estáveis (ver README).
