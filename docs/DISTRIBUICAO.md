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

## Instalador (`PEPCLI-Setup-x.y.z.exe`)

```powershell
.\scripts\build-installer.ps1   # testes + pep.exe self-contained + instalador
.\scripts\make-icon.ps1         # (opcional) regenera assets\pep-cli.ico / .png
```

Gera `artifacts\installer\PEPCLI-Setup-<versao>.exe` (versão de `Directory.Build.props`), um único arquivo com o `pep.exe` self-contained embutido: **não requer .NET nem administrador**.

O que faz (janela única: pasta, duas opções e **Instalar**/**Atualizar**):

- Copia `pep.exe` para `%LOCALAPPDATA%\Programs\PepCli` (alterável) e grava `uninstall.cmd`. A pasta precisa ser nova, vazia ou uma instalação anterior.
- Opcional: adiciona a pasta ao `PATH` do usuário (sem duplicar) e cria o atalho **PEP CLI** no Menu Iniciar.
- Registra a desinstalação em *Aplicativos instalados* (HKCU, por usuário).
- Recusa instalar se o `pep` estiver em execução a partir da pasta.
- Atualizar = rodar o instalador novo. Configuração (`%APPDATA%\PepCli`) e histórico (`%LOCALAPPDATA%\PepCli`) são preservados.

Modo silencioso (automação/teste): `PEPCLI-Setup-x.y.z.exe /silent [/dir:<pasta>] [/nopath] [/noshortcut] [/noregistry]`. Exit `0` sucesso, `1` falha, `2` argumento inválido; log em `<pasta>\install.log`.

Desinstalação: *Configurações → Aplicativos instalados → PEP CLI*, ou `<pasta>\uninstall.cmd`. Remove só a entrada do PATH, o atalho, a chave de registro e a pasta de instalação; não toca na configuração.

O `.exe` não é assinado: o SmartScreen pode exibir "O Windows protegeu o computador" → **Mais informações → Executar assim mesmo**.

## Instalação manual na equipe

1. Copie a pasta publicada para um local compartilhado (drive da equipe) e dali para `C:\Ferramentas\pep`.
2. Adicione ao `PATH` do usuário.
3. `pep config init` → `pep env discover` → `pep env configure` → `pep doctor`.

## Atualização (instalação manual)

1. Feche terminais com `pep` em execução.
2. Substitua o conteúdo da pasta de instalação pela nova publicação.
3. A configuração (`%APPDATA%\PepCli`) e o histórico (`%LOCALAPPDATA%\PepCli`) são preservados.
4. Rode `pep version` e `pep config validate`.

Se o esquema de configuração mudar (`versaoEsquema`), a nota da versão trará o passo de migração.
