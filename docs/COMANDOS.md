# Referência de comandos

> O PEP CLI realiza o merge e mantém as alterações em Pending Changes. A revisão, resolução de conflitos e o check-in são responsabilidades do usuário.

Sintaxe geral: `pep <comando> [argumentos] [opções]`. Sem argumentos abre o menu interativo. `pep <comando> --help` mostra a ajuda do comando.

## Opções globais

| Opção | Efeito |
|---|---|
| `--help`, `-h` | Ajuda do comando |
| `--version` | Número da versão |
| `--json` | Saída JSON sem cores, animações ou prompts (implica `--non-interactive`) |
| `--non-interactive` | Nunca pergunta; entrada ausente ⇒ exit 2 |
| `--yes`, `-y` | Confirma um plano completo. Não ignora validações, não resolve conflitos, não cria mapeamentos, não autoriza check-in |
| `--no-color` | Sem cores (também `NO_COLOR`) |
| `--ascii` | Somente ASCII |
| `--config <arquivo>` | Arquivo de configuração explícito |
| `--verbose` | Detalhes das ferramentas |

## Merge

### `pep merge`

Propaga **um** changeset da origem para um ou mais destinos. Resultado em Pending Changes.

| Opção | Descrição |
|---|---|
| `--project, -p <alias>` | `back` (Sau-PEP) ou `sau` (Sau-Saude) |
| `--source, -s <versao>` | Origem: `atual`, `12.1.2606`, `2606` |
| `--target, -t <versao>` | Destino (repetível) |
| `--all-legacy` | Todas as legadas **ativas** (exceto a origem) |
| `--changeset, -c <id>` | Changeset (aceita `861799` ou `C861799`) |
| `--dry-run` | Só consultas e plano |
| `--stop-on-failure` | Interrompe os destinos seguintes após falha, conflito ou bloqueio; com destinos bloqueados no plano (não interativo), aborta com exit 3 |
| `--continue-on-failure` | Padrão; mantido por compatibilidade (não pode ser combinado com `--stop-on-failure`) |

Comportamento: por padrão o merge roda em **todos** os destinos prontos, mesmo após falha ou conflito em um deles; destinos bloqueados no plano são ignorados e listados no resumo (interativo pergunta antes). Rede/autenticação, cancelamento e resultado indeterminado sempre interrompem. A pré-verificação avalia até 4 destinos em paralelo (somente consultas); a aplicação é sempre sequencial. Com plano de menos de 10 minutos, a revalidação antes do merge consulta apenas pending changes e arquivos graváveis sem checkout; plano mais antigo é revalidado por completo. O resumo mostra "Merge aplicado em X de Y destinos selecionados" (JSON: `aplicados`, `selecionados`).

Sintaxe antiga aceita: `pep merge <projeto> <versao> <changeset>` — a versão é a **origem**; destinos são escolhidos no terminal ou exigidos por opção.

Estados por destino: *Aplicado com pending changes* · *Sem alterações aplicáveis* · *Já integrado* · *Aplicado com conflitos* · *Bloqueado* · *Falhou* · *Cancelado* · *Não iniciado* · *Indeterminado — inspecionar*.

Saída: 0 todos aplicados/já integrados · 3 nada executado por bloqueio · 4 falha sem nada aplicado · 5 conflito ou parcial · 130 cancelado.

## Get e build

### `pep get all` · `pep get version <versao>`

`tf get <pasta> /recursive` para Sau-PEP e Sau-Saude das versões ativas (ou da versão informada). Nunca `/force` nem `/overwrite`.

| Opção | Descrição |
|---|---|
| `--project, -p <alias>` | Somente um projeto |
| `--dry-run` | Validação de mapeamentos, pending changes e `get /preview` |

### `pep build all` · `pep build version <versao>`

MSBuild sequencial: `Sau-Saude.sln` e depois `RM.Pep.sln` (ordem dos projetos na configuração). Não faz get e não encerra o RM.Host (bloqueia se o host da versão estiver aberto).

| Opção | Descrição |
|---|---|
| `--project, -p <alias>` | Somente um projeto |
| `--configuration <cfg>` | `/p:Configuration=<cfg>` |
| `--continue-on-failure` | Continua após falha |
| `--dry-run` | Só o plano |

## Ambientes e configuração

| Comando | Descrição |
|---|---|
| `pep env list` | Catálogo: atual, legadas ativas e desativadas |
| `pep env discover [--offline]` | Pastas candidatas em `<raiz>\Atual` e `<raiz>\Legado` + mapeamento. Somente leitura |
| `pep env configure` | Seleção guiada da atual e até 4 legadas; revisão e confirmação antes de gravar |
| `pep env configure --atual <id> --legado <id>... --yes` | Rotação não interativa |
| `pep env validate` | Pastas e mapeamentos das versões ativas |
| `pep config init [--force]` | Cria a configuração com defaults (backup com `--force`) |
| `pep config show` | Configuração efetiva e arquivo usado |
| `pep config validate` | 0 válida, 2 inválida |

## Diagnóstico e TFVC (somente leitura)

| Comando | Descrição |
|---|---|
| `pep doctor` | Configuração, ferramentas, conexão/autenticação, catálogo, mapeamentos, soluções, disco, histórico. Não corrige nada |
| `pep login` | Autentica o tf.exe na coleção: roda `tf workspaces /collection` no seu terminal, sem `/noprompt`, para o login aparecer. Uma vez por máquina; resolve TF30063. Não grava senha/token |
| `pep workspace list` | Workspaces da coleção |
| `pep workspace inspect <versao> [--project]` | Mapeamento efetivo, cloaking, divergência e como corrigir |
| `pep pending list [versao] [--project]` | Pending changes |
| `pep changeset show <id> [--project --source]` | Itens do changeset; incluídos/excluídos do escopo |

## Ferramentas locais

| Comando | Descrição |
|---|---|
| `pep delete broker <versao> [--no-backup]` | Remove somente `Bin\_Broker.dat`, com confirmação e backup. Ausente ⇒ nenhuma ação |
| `pep open host <versao> [--new-instance]` | Abre `Bin\RM.Host.exe`; avisa se já está aberto |
| `pep open rm <versao>` | Abre `Bin\RM.exe` |
| `pep open alias <versao>` | Abre `Bin\Alias.dat` no editor |
| `pep open hostconfig <versao>` | Abre `Bin\RM.Host.exe.config` no editor |
| `pep kill host [--pid <n>]... [--version <v>] [--force]` | Lista RM.Host (PID, caminho, versão e confiança) e encerra só os escolhidos; gracioso por padrão. Na lista, Espaço marca e **Enter encerra** (sem pergunta extra); `--pid`/`--version` pedem confirmação ou `--yes`; `--force` sempre confirma |

## Histórico e ajuda

| Comando | Descrição |
|---|---|
| `pep history list [--limit <n>]` | Últimas execuções |
| `pep history show <id>` | Etapas, resultado por alvo e orientações |
| `pep help [comando]` | Ajuda |
| `pep version` | Versão, runtime, ferramentas, configuração e catálogo |

## Removidos

`merge front`, `open front`, `open podoc`, `kill all`, `cmd`, `clear`, `cls`, `exit` — ver [MIGRACAO.md](MIGRACAO.md).
