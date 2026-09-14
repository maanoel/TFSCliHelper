# PEP CLI

```
██████╗ ███████╗██████╗      ██████╗██╗     ██╗
██╔══██╗██╔════╝██╔══██╗    ██╔════╝██║     ██║
██████╔╝█████╗  ██████╔╝    ██║     ██║     ██║
██╔═══╝ ██╔══╝  ██╔═══╝     ██║     ██║     ██║
██║     ███████╗██║         ╚██████╗███████╗██║
╚═╝     ╚══════╝╚═╝          ╚═════╝╚══════╝╚═╝
```

CLI da equipe PEP RM para **propagar changesets TFVC entre versões em um único comando**, sem abrir o Visual Studio, além de get, build e utilitários do RM.

> **O PEP CLI realiza o merge e mantém as alterações em Pending Changes. A revisão, resolução de conflitos e o check-in são responsabilidades do usuário.**

> ⚠️ **Situação:** implementado e coberto por testes automatizados, **ainda não homologado contra o TFVC real**. Não use em produção antes de concluir [docs/HOMOLOGACAO.md](docs/HOMOLOGACAO.md).

## Visão geral

| Você quer… | Comando |
|---|---|
| Levar o changeset 861799 da atual para todas as legadas | `pep merge --project back --source atual --all-legacy --changeset 861799` |
| Simular antes | o mesmo comando com `--dry-run` |
| Ser guiado passo a passo | `pep` (menu) ou `pep merge` |
| Atualizar todas as versões | `pep get all` |
| Escolher versões e projetos para compilar (PEP sempre primeiro) | `pep build` |
| Compilar só o PEP da 2606 | `pep build --version 2606 --project back` |
| Saber por que algo falhou | `pep doctor` |

Escopo: somente backend — `back` (Sau-PEP) e `sau` (Sau-Saude). O front do PEP está no Git e não é gerenciado por este CLI.

## Pré-requisitos

- Windows 10/11 x64.
- **.NET 9 Runtime** só na instalação manual (o instalador já inclui o runtime). Para desenvolver: **SDK .NET 9.0.306** (fixado em `global.json`).
- **Visual Studio 2022** com Team Explorer (fornece `TF.exe`) e MSBuild. Os caminhos são localizados via `vswhere` ou configurados.
- Workspaces TFVC já mapeados pelo Visual Studio (o CLI **não** cria nem altera mapeamentos).
- Acesso à coleção (VPN quando necessário) e login feito no Visual Studio.

> Ciclo de suporte: o .NET 9 é STS e tem fim de suporte em **10/11/2026**. A migração para o .NET 10 (LTS) está registrada como pendência em [specs/README.md](specs/README.md).

## Instalação

1. Baixe `PEPCLI-Setup-x.y.z.exe` (gerado por `scripts/build-installer.ps1`) e execute — não precisa de administrador nem de .NET.
2. Clique em **Instalar** (ou **Atualizar**, se já houver uma versão).
3. Abra um novo terminal e digite `pep`.

Detalhes, modo silencioso e desinstalação em [docs/DISTRIBUICAO.md](docs/DISTRIBUICAO.md). A instalação manual (`scripts/publish.ps1` + PATH) continua disponível.

## Primeiros passos

1. Abra o terminal e digite `pep`.
2. Na tela **Primeira configuração**, confira a tabela e pressione **Enter** em *Aplicar configuração automática (recomendado)*.
   A atual é `C:\Linha-RM\Atual\Release`; as 4 versões mais novas de `C:\Linha-RM\Legado` ficam ativas e as mais antigas, desativadas.
3. `pep login` para autenticar o tf.exe.
4. Pronto. Opcional: `pep doctor` e `pep env validate` confirmam ferramentas e mapeamentos TFVC.

Por argumentos: `pep config auto` (ou `pep config auto --yes`). Configuração manual continua disponível:

```powershell
pep config init        # cria %APPDATA%\PepCli\config.json com defaults seguros
pep env discover       # lista pastas em C:\Linha-RM\Atual e \Legado e seus mapeamentos (somente leitura)
pep env configure      # escolhe a versão atual e até 4 legadas ativas
pep doctor             # valida ferramentas, conexão, catálogo e mapeamentos
```

## Uso interativo

```powershell
pep
```

Abre o banner e um menu navegável por setas: Merge · Get · Build · Ambientes e versões · Diagnóstico · Pending changes · Ferramentas locais · Histórico · Ajuda · Sair. Cada tela oferece **Voltar**, mostra o plano antes de executar e pede confirmação. `Ctrl+C` cancela a operação atual (cancelamento **não** é rollback).

## Uso por argumentos

```powershell
# Merge para dois destinos
pep merge --project back --source atual --target 2606 --target 2602 --changeset 861799

# Merge para todas as legadas ativas, primeiro simulando
pep merge --project sau --source atual --all-legacy --changeset 861799 --dry-run

# Automação: sem prompts, JSON, confirmando o plano completo
pep merge --project back --source atual --target 2606 --changeset 861799 --yes --json

pep get all
pep get version 2606 --project back
pep build all --configuration Release
pep build --version atual --version 2606 --project sau --dry-run
pep pending list 2606
pep changeset show 861799 --project back --source atual
pep kill host --pid 12345
pep delete broker atual
pep history list
```

Veja a referência completa em [docs/COMANDOS.md](docs/COMANDOS.md).

## Como o merge protege o seu trabalho

Antes de pedir confirmação, para cada destino o CLI verifica:

1. changeset existe e contém itens do projeto na origem (itens de outros projetos aparecem como **excluídos**);
2. pasta existe e está mapeada (inclusive por pasta ancestral), sem cloak e apontando para o caminho TFVC configurado;
3. há relação de merge (`tf merge /candidate`) — **nunca** usa baseless; se já foi integrado, não reaplica;
4. pending changes nos arquivos do changeset **bloqueiam**; as demais são preservadas e informadas;
5. arquivos do changeset graváveis sem checkout (alteração não reconciliada) bloqueiam;
6. conflitos não resolvidos no escopo bloqueiam;
7. destino desatualizado gera aviso e o menu oferece get com confirmação;
8. `tf merge /preview` real;
9. execuções anteriores do mesmo changeset no histórico local.

Execução: sequencial, `tf merge /version:C<id>~C<id>` (um único changeset), comparação de pending changes antes/depois, conflitos listados e **nunca resolvidos** pelo CLI. Por padrão segue para os demais destinos após falha ou conflito (`--stop-on-failure` interrompe); rede/autenticação, indeterminado e cancelamento sempre interrompem.

## Códigos de saída

| Código | Significado |
|---|---|
| 0 | Sucesso |
| 1 | Erro inesperado |
| 2 | Uso ou configuração inválida |
| 3 | Pré-condição não atendida (bloqueio) |
| 4 | Falha operacional |
| 5 | Conflito ou resultado parcial |
| 130 | Cancelado |

## Terminal e acessibilidade

- `--no-color` (ou `NO_COLOR`), `--ascii`, saída redirecionada: sem ANSI, sem animações, banner ASCII.
- `--json`: somente JSON no stdout, sem prompts.
- `--non-interactive`: nunca pergunta; parâmetro ausente gera erro de uso.
- Estados sempre com ícone **e** texto (ex.: `✔ Aplicado com pending changes`, `[BLOQ] Bloqueado`).

## Documentação

| Documento | Conteúdo |
|---|---|
| [docs/COMANDOS.md](docs/COMANDOS.md) | Referência de todos os comandos e opções |
| [docs/CONFIGURACAO.md](docs/CONFIGURACAO.md) | Modelo, precedência, catálogo, nova versão, desativar legada |
| [docs/OPERACAO.md](docs/OPERACAO.md) | Corrigir mapeamento, revisar pending changes, falha parcial, retomar, logs, troubleshooting |
| [docs/MIGRACAO.md](docs/MIGRACAO.md) | Sintaxes antigas mantidas, alteradas e removidas |
| [docs/HOMOLOGACAO.md](docs/HOMOLOGACAO.md) | Roteiro de validação TFVC e situação dos testes |
| [docs/DISTRIBUICAO.md](docs/DISTRIBUICAO.md) | Build, publicação e atualização |
| [specs/](specs/README.md) | Specs de cada entrega e decisões registradas |
| [AGENTS.md](AGENTS.md) | Regras para agentes de IA e contribuidores |

## Limitações conhecidas

- Integração TFVC ainda não homologada: formatos de saída do `tf.exe` em pt-BR precisam ser confirmados (ver homologação). Saídas não reconhecidas resultam em **indeterminado/bloqueado**, nunca sucesso.
- Merge de um changeset por vez; sem busca de changesets.
- Em workspaces **locais**, adições/exclusões não detectadas pelo TFVC podem não aparecer na pré-verificação (é exibido aviso).
- "Workspace de outra máquina" não é distinguível de "pasta não mapeada" pela saída do `tf workfold`; ambos bloqueiam com a mesma orientação.
- Front do PEP (Git) fora do escopo.

⌨️ por [manolos] e equipe PEP RM
