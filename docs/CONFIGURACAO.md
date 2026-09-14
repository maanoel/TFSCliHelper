# Configuração e catálogo de versões

## Onde fica

`%APPDATA%\PepCli\config.json`

**Precedência (única):**

1. argumentos explícitos — `--config <arquivo>` e opções do comando;
2. configuração do usuário — variável `PEPCLI_CONFIG` ou o arquivo acima;
3. defaults seguros — usados pela configuração automática.

**Não existe configuração manual** (decisão de 2026-09-14). Na primeira execução — `pep` (menu) ou qualquer comando, sem arquivo ou sem versão atual — o PEP CLI aplica a configuração automática **sem perguntas** e avisa: *"Configuração automática concluída: o PEP CLI está pronto para uso."* Ajuda (`pep help`, `--help`) e comandos `pep config ...` não disparam a primeira configuração.

## Configuração automática

A primeira execução e `pep config auto` montam o catálogo pela convenção de pastas:

- **Atual:** `<raizLocal>\Atual\Release` (id `atual`, sem aliases). Se a pasta não existe, nada é gravado; o aviso orienta criar/conferir a pasta e rodar `pep config auto`.
- **Legadas:** subpastas de `<raizLocal>\Legado` com nome de versão numérica (`12.1.2606`, até 4 segmentos). Outras pastas (`backup`, `12.1.2606-old`) são ignoradas e listadas.
- **Ordem numérica** por segmento (`System.Version`), não alfabética: `12.1.2606 > 12.1.2602 > 12.1.2510`; `12.1.34 < 12.1.2306`.
- As **4 mais novas ficam ativas**; as mais antigas entram com `"ativa": false` (visíveis em `env list`, disponíveis para rotação). Nenhuma pasta é criada, movida ou excluída.
- Id da legada = nome da pasta; alias = último segmento (`2606`). `caminhoLocal` relativo (`Legado\12.1.2606`).
- `caminhoServidor` pela convenção `<raizServidor>/<pasta relativa>`, **sem consultar o TFVC** — confirme com `pep env validate`.
- Preserva coleção, ferramentas, projetos, arquivos locais, apresentação e `raizServidor`; mantém o `workspace` de versões já cadastradas (mesmo id ou mesma pasta). Versões antigas sem pasta correspondente permanecem, desativadas.
- Pasta de versão sem nenhum projeto configurado entra com aviso "sem projetos".
- Arquivo existente **inválido** nunca é sobrescrito automaticamente: corrija-o ou use `pep config auto --force` (recria com backup).
- Grava com backup (`config.json.<data>.bak`). Na primeira execução grava direto; `pep config auto` mostra a proposta e grava com Enter (`--yes` em scripts).

## Modelo

```jsonc
{
  "versaoEsquema": 1,
  "raizLocal": "C:\\Linha-RM",
  "colecao": "https://totvstfs.visualstudio.com/DefaultCollection",
  "raizServidor": "$/Linha-RM",   // espelho TFVC da raizLocal (convenção)
  "ferramentas": {
    "tfExe": null,          // null = localizar via vswhere
    "msBuild": null,
    "editor": "notepad++"
  },
  "versoes": [
    {
      "id": "12.1.2610",
      "atual": true,
      "ativa": true,
      "caminhoLocal": "Atual\\Release",               // relativo à raiz ou absoluto
      "caminhoServidor": "$/Linha-RM/atual/release",  // caminho TFVC, nunca local
      "aliases": ["2610"],
      "workspace": null
    },
    { "id": "12.1.2606", "atual": false, "ativa": true, "caminhoLocal": "Legado\\12.1.2606",
      "caminhoServidor": "$/Linha-RM/Legado/12.1.2606", "aliases": ["2606"], "workspace": null }
  ],
  "projetos": [
    { "alias": "back", "nome": "Sau-PEP",   "pastaLocal": "Sau-PEP",   "pastaServidor": "Sau-PEP",   "solucao": "RM.Pep.sln",    "principal": true },
    { "alias": "sau",  "nome": "Sau-Saude", "pastaLocal": "Sau-Saude", "pastaServidor": "Sau-Saude", "solucao": "Sau-Saude.sln", "principal": false }
  ],
  "arquivosLocais": {
    "broker": "Bin\\_Broker.dat", "host": "Bin\\RM.Host.exe", "rm": "Bin\\RM.exe",
    "alias": "Bin\\Alias.dat", "hostConfig": "Bin\\RM.Host.exe.config"
  },
  "apresentacao": { "semCor": false, "ascii": false }
}
```

Ordem do build: em cada versão, o projeto com `"principal": true` (no máximo um; o validador rejeita dois) compila primeiro; os demais seguem a ordem de `projetos`.
Configurações antigas sem nenhum `principal` tratam `back` (Sau-PEP) como principal — a ordem salva no arquivo não é alterada (nem pelo `config auto`).

### Convenção de caminho TFVC

Quando o mapeamento não informa o caminho de servidor, `config auto`/`env discover` usam a convenção da equipe, sem perguntar:

| Pasta local | Caminho TFVC |
|---|---|
| `C:\Linha-RM\Legado\12.1.2506` | `$/Linha-RM/Legado/12.1.2506` |
| `C:\Linha-RM\Atual\Release` | `$/Linha-RM/Atual/Release` |

Regra: `<raizServidor>/<pasta relativa à raizLocal>`. O TFVC não diferencia maiúsculas. Pastas fora da `raizLocal` continuam perguntando. Confirme com `pep env validate`.

### Regras validadas

- Exatamente **uma** versão `atual` (ativa) e **no máximo 4** legadas ativas.
- `id` e aliases únicos entre todas as versões.
- `caminhoLocal` não pode ser caminho TFVC; `caminhoServidor` deve começar com `$/`.
- Caminhos de projetos e arquivos locais relativos, sem `..`.
- **Senhas e tokens não são aceitos.** Campos como `senha`, `token`, `password`, `pat` invalidam o arquivo. A autenticação é a do tf.exe/Visual Studio.

Valide com `pep config validate`.

## Resolução de versões

| Token | Resultado |
|---|---|
| `12.1.2606` | id exato |
| `2606` | alias ou último segmento exato |
| `atual` | versão marcada como atual |
| `06`, `260` | **erro**: nunca escolhe versão parecida |
| alias em duas versões | **erro de ambiguidade** |
| versão desativada | erro informando que está desativada |

## Nova versão (rotação)

A rotação segue as pastas; não há seleção manual.

1. Garanta que as pastas existem em `<raizLocal>\Atual\Release` e `<raizLocal>\Legado\<versão>` e estão mapeadas pelo Visual Studio (o CLI não cria mapeamentos).
2. `pep config auto` — a atual continua `Atual\Release`; as 4 legadas mais novas ficam ativas e as mais antigas, desativadas.
3. `pep env validate`.

O backup do arquivo anterior fica ao lado (`config.json.<data>.bak`). Versões desativadas **permanecem** no arquivo, não participam de `get all`, `build all`, `--all-legacy` e **nenhuma pasta é excluída**.
