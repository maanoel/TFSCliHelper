# Referência de comandos

> O PEP CLI realiza o merge e mantém as alterações em Pending Changes. A revisão, resolução de conflitos e o check-in são responsabilidades do usuário.

Sintaxe geral: `pep <comando> [argumentos] [opções]`. Sem argumentos abre o menu interativo. `pep <comando> --help` mostra a ajuda do comando.

## Opções globais

| Opção | Efeito |
|---|---|
| `--help`, `-h` | Ajuda do comando |
| `--version` | Número da versão (só antes de qualquer comando; depois do comando é opção dele, ex.: `pep build --version 2606`) |
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

Sem credencial do tf.exe (`TF30063`), o login do TFS abre automaticamente em terminal interativo e o merge continua (ver *Login automático do TFS* em Diagnóstico). Um `tf merge` que falhar por autenticação só é repetido se a saída não citar nenhum item, ou seja, se nada foi processado; o mesmo vale para `tf get`.

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

### `pep build` · `pep build all` · `pep build version <versao>`

MSBuild sequencial. Não faz get. **Antes de compilar, encerra o RM.Host das versões do build** com a mesma rotina de `pep kill host` (graciosa, sem pergunta); se algum host não encerrar, só aquela versão fica bloqueada com orientação para `pep kill host --pid <n> --force`. Hosts de outras versões não são tocados. Não pede confirmação: mostra o plano e compila direto (Ctrl+C interrompe; `--dry-run` só mostra o plano).

**Ordem:** versões atual → legadas (ordem do catálogo); em cada versão o projeto **principal** (PEP, `RM.Pep.sln`) compila primeiro e depois os demais selecionados na ordem da configuração (Saúde, `Sau-Saude.sln`). A coluna *Ordem* do plano mostra a sequência.

`pep build` sem `--version`/`--all`, em terminal interativo, pergunta as versões (atual já marcada) e os projetos (todos marcados, exceto o Sau-Saúde, que vem desmarcado; Espaço marca/desmarca). Nada marcado cancela (130). Em modo não interativo, `--version` ou `--all` é obrigatório (exit 2).

| Opção | Descrição |
|---|---|
| `--version <versao>` | Somente `pep build`; repetível (ex.: `--version atual --version 2606`) |
| `--all` | Somente `pep build`; todas as versões ativas (não combina com `--version`) |
| `--project, -p <alias>` | Repetível: `back` (PEP) e/ou `sau` (Saúde). Padrão: todos |
| `--configuration <cfg>` | `/p:Configuration=<cfg>` |
| `--continue-on-failure` | Continua após falha |
| `--dry-run` | Só o plano |

```powershell
pep build                                            # seleção interativa
pep build --version 2606 --project back --dry-run    # só o PEP da 2606
pep build --all --project sau                        # só o Saúde, todas as versões
```

## Ambientes e configuração

| Comando | Descrição |
|---|---|
| `pep env list` | Catálogo: atual, legadas ativas e desativadas |
| `pep env discover [--offline]` | Pastas candidatas em `<raiz>\Atual` e `<raiz>\Legado` + mapeamento. Somente leitura |
| `pep env validate` | Pastas e mapeamentos das versões ativas |
| `pep config auto [--yes] [--force]` | Configuração automática: `<raiz>\Atual\Release` atual e as 4 legadas mais novas de `<raiz>\Legado` ativas (ordem numérica), mais antigas desativadas. Sem consulta ao TFVC; confirmação com Enter; backup. `--force` recria a partir de um arquivo inválido. **Na primeira execução é aplicada sozinha, sem perguntas** |

Não existe configuração manual: `pep env configure` e `pep config init` foram removidos (exit 2, orientam `pep config auto`).
| `pep config show` | Configuração efetiva e arquivo usado |
| `pep config validate` | 0 válida, 2 inválida |

## Diagnóstico e TFVC (somente leitura)

| Comando | Descrição |
|---|---|
| `pep doctor` | Configuração, ferramentas, conexão/autenticação, catálogo, mapeamentos, soluções, disco, histórico. Não corrige nada |
| `pep workspace list` | Workspaces da coleção |
| `pep workspace inspect <versao> [--project]` | Mapeamento efetivo, cloaking, divergência e como corrigir |
| `pep pending list [versao] [--project]` | Pending changes |
| `pep changeset show <id> [--project --source]` | Itens do changeset; incluídos/excluídos do escopo |

**Login automático do TFS:** quando o tf.exe responde sem credencial (`TF30063`), o PEP CLI — em terminal interativo — roda `tf workspaces /collection:<coleção>` sem `/noprompt` no seu terminal, para a janela de login do tf.exe aparecer, confirma o acesso e repete a consulta. Uma tentativa por execução (mesmo com consultas em paralelo); a credencial fica no cache do tf.exe, o PEP CLI não recebe nem grava senha/token. Com `--json`/`--non-interactive` não há login: o comando falha com a orientação e basta rodá-lo uma vez em um terminal interativo.

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

## Prompt de comandos (menu)

No menu `pep`, **Prompt de comandos (digitar comandos pep)** abre um prompt `pep>` dentro do CLI:

```text
pep> merge --project back --source atual --target 2606 --changeset 669997 --dry-run
pep> pep build --version 2606
pep> sair
```

- Aceita qualquer comando do PEP CLI, com ou sem o prefixo `pep`, incluindo opções globais (`--yes`, `--json`, `--help`). Aspas duplas agrupam argumentos com espaço.
- Usa o mesmo despacho do modo por argumentos: mesmas validações, planos e confirmações.
- **Não executa comandos do Windows/shell** (ex.: `dir`, `del`): retornam "Comando desconhecido".
- `sair`, `voltar`, `exit` ou Ctrl+C voltam ao menu.

## Removidos

`merge front`, `open front`, `open podoc`, `kill all`, `cmd`, `clear`, `cls`, `exit`, `login` (o login do TFS agora é automático) — ver [MIGRACAO.md](MIGRACAO.md).
