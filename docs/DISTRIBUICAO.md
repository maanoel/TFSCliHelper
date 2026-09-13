# Build, distribuição e atualização

## Desenvolvimento

```powershell
dotnet --version          # deve ser 9.0.306 (global.json, sem roll forward)
dotnet build PEPCliHelper.sln
dotnet test PEPCliHelper.sln
dotnet run --project src\PEPCliHelper -- version
```

Estrutura:

```
src/PEPCliHelper.Core    domínio, casos de uso, adapter tf.exe, processos, arquivos, histórico (sem UI)
src/PEPCliHelper         executável pep: CommandChain, CommandsBuilders, Menus, Presentation
tests/PEPCliHelper.Tests xUnit (unitários com fakes + CLI não interativo)
specs/                   specs de cada entrega
docs/                    documentação de uso e homologação
```

## Publicação

```powershell
.\scripts\publish.ps1                 # Release, win-x64, framework-dependent
.\scripts\publish.ps1 -Output D:\pep  # destino alternativo
```

Gera `artifacts\pep\pep.exe` (arquivo único). Requer o **.NET 9 Runtime** na máquina de destino.

## Instalação na equipe

1. Copie a pasta publicada para um local compartilhado (drive da equipe) e dali para `C:\Ferramentas\pep`.
2. Adicione ao `PATH` do usuário.
3. `pep config init` → `pep env discover` → `pep env configure` → `pep doctor`.

## Atualização

1. Feche terminais com `pep` em execução.
2. Substitua o conteúdo da pasta de instalação pela nova publicação.
3. A configuração (`%APPDATA%\PepCli`) e o histórico (`%LOCALAPPDATA%\PepCli`) são preservados.
4. Rode `pep version` e `pep config validate`.

Se o esquema de configuração mudar (`versaoEsquema`), a nota da versão trará o passo de migração.
