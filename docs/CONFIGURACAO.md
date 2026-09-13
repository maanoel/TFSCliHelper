# Configuração e catálogo de versões

## Onde fica

`%APPDATA%\PepCli\config.json`

**Precedência (única):**

1. argumentos explícitos — `--config <arquivo>` e opções do comando;
2. configuração do usuário — variável `PEPCLI_CONFIG` ou o arquivo acima;
3. defaults seguros — criados por `pep config init`.

Nada é criado silenciosamente: sem arquivo, os comandos param e orientam `pep config init`.

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
    { "alias": "sau",  "nome": "Sau-Saude", "pastaLocal": "Sau-Saude", "pastaServidor": "Sau-Saude", "solucao": "Sau-Saude.sln" },
    { "alias": "back", "nome": "Sau-PEP",   "pastaLocal": "Sau-PEP",   "pastaServidor": "Sau-PEP",   "solucao": "RM.Pep.sln" }
  ],
  "arquivosLocais": {
    "broker": "Bin\\_Broker.dat", "host": "Bin\\RM.Host.exe", "rm": "Bin\\RM.exe",
    "alias": "Bin\\Alias.dat", "hostConfig": "Bin\\RM.Host.exe.config"
  },
  "apresentacao": { "semCor": false, "ascii": false }
}
```

A ordem de `projetos` é a ordem do build.

### Convenção de caminho TFVC

Quando o mapeamento não informa o caminho de servidor, `env discover`/`env configure` usam a convenção da equipe, sem perguntar:

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

## Cadastrar nova versão (rotação)

Exemplo: sai a 12.1.2614 como atual; a 12.1.2610 vira legada; a 12.1.2602 deixa de ser usada.

1. Garanta que as pastas existem e estão mapeadas pelo Visual Studio (o CLI não cria mapeamentos).
2. `pep env discover` — confira o caminho TFVC efetivo de cada pasta.
3. `pep env configure`
   - escolha a pasta da **atual** e informe o id (`12.1.2614`);
   - marque até 4 legadas ativas (inclua a pasta da antiga atual, se ela foi movida para `Legado`, ou mantenha `Atual\Release` com o id antigo);
   - revise a tabela *Catálogo revisado* e confirme.
4. `pep env validate`.

Não interativo:

```powershell
pep env configure --atual 12.1.2614 --legado 12.1.2610 --legado 12.1.2606 --yes
```

O backup do arquivo anterior fica ao lado (`config.json.<data>.bak`).

## Desativar uma legada

- Pelo `pep env configure`, desmarque a versão; ou
- edite o arquivo e defina `"ativa": false`.

Versões desativadas **permanecem** no arquivo, não participam de `get all`, `build all`, `--all-legacy` e **nenhuma pasta é excluída**.
