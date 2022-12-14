# PEP RM CliHelper

Command line tool com comandos utilizados diariamente pela equipe do PEP RM

### 📋 Pré-requisitos

Sdk do .NET a partir da 5.0

https://dotnet.microsoft.com/en-us/download/dotnet/5.0

Utilizar o mapeamento padrão C:\Linha-RM\legado\... e C:\Linha-RM\atual\release\...

### 🔧 Execução

Executar utilizando o PEPCliHelper disponibilizado no driver.

### 🔩 Exemplos de comandos

Para as versões antigas que não seguem o commit semântico, utilizar apenas os dos últimos dígitos no comando. Ex: '12.1.32', fica apenas '32'.

#### get all - Realiza o get no tfs dos projetos Sau-PEP, Sau-Saude e FrameHTML em todas as versões utilizadas pela equipe.

```
get all
```

#### get version numero_versao - Realiza o get utilizando o número da versão especificada 

```
get version 32
```
```
get version 2209
```

#### build all - Realiza o build de todas as versões utilizadas do Sau-PEP e Sau-Saude.

```
build all
```

#### build version numero_versao - Realiza o build na versão especificada do Sau-PEP e Sau-Saude.

```
build version 32
```
```
build version 2209
```

#### merge projeto numero_versao numero_changeset - Realiza o merge de um changeset da versão para todas as outras

Comando só pode ser utilizado após o primeiro check in já ter sido realizado

Realize o get all ou o get versio antes de executar este comando

O changeset do check in precisa ser passado como argumento

O check in será replicado para todas as outras versões

O argumento projeto possui os valores de: back, front e sau

```
merge back 32 861487
```

```
merge front 2209 861488
```

```
merge sau atual 861799
```

#### delete broker numero_versao - Remove o arquivo broker.dat da versão especificada

```
delete broker 32
```

```
delete broker atual
```

#### open host numero_versao - Abre o rm.host.exe na versão especificada

```
open host 32
```

```
open host 2209
```

#### kill host - Mata a instância do rm.host.exe

```
kill host
```


⌨️ por [manolos]
