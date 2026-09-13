# Operação e troubleshooting

> O PEP CLI realiza o merge e mantém as alterações em Pending Changes. A revisão, resolução de conflitos e o check-in são responsabilidades do usuário.

## Corrigir mapeamento

Sintoma: `Bloqueado · não mapeado`, `cloaked` ou `mapeamento divergente`.

1. Diagnostique: `pep workspace inspect 2606 --project back`.
2. Conforme o estado:
   - **pasta inexistente** — confirme `caminhoLocal` (`pep config show`) ou faça o get inicial pelo Visual Studio;
   - **não mapeado** — no Visual Studio, *Source Control Explorer* → mapeie o caminho TFVC da versão para a pasta local (pode ser na pasta raiz da versão; mapeamentos ancestrais são aceitos). Também pode ser workspace de outra máquina ou de outra coleção: confira com `tf workspaces`;
   - **cloaked** — remova o cloak no Source Control Explorer ou com `tf workfold /decloak "<caminho>"`;
   - **divergente** — a pasta aponta para outro caminho TFVC: corrija o mapeamento ou `caminhoServidor` na configuração.
3. Revalide: `pep env validate`.

O CLI apenas orienta; ele **nunca** cria workspace, mapeamento ou cloak.

## Revisar pending changes após o merge

```powershell
pep pending list 2606 --project back
```

Depois, no Visual Studio (*Team Explorer → Pending Changes*): revise o diff, resolva conflitos (*Resolve Conflicts*) e faça o check-in com o comentário adequado. Metadados de merge ficam registrados mesmo quando não há diferença de texto.

## Inspecionar falha parcial

O resumo final lista o estado de cada versão. Para os detalhes:

```powershell
pep history list
pep history show <id>
```

- **Aplicado com pending changes** — está pronto para revisão; não desfaça.
- **Aplicado com conflitos** — resolva manualmente.
- **Falhou** — nada novo foi pendurado; leia a mensagem do tf.exe.
- **Indeterminado** — o CLI não conseguiu confirmar o estado; revise pending changes e conflitos antes de qualquer nova ação.
- **Não iniciado / Bloqueado** — nada foi executado nesse destino.

Não há rollback automático. Se precisar desfazer um destino, faça pelo Visual Studio, conscientemente.

## Retomar sem duplicar

Repita o mesmo comando com `--dry-run`:

```powershell
pep merge --project back --source atual --all-legacy --changeset 861799 --dry-run
```

Destinos em que o changeset já foi integrado aparecem como **Já integrado** e não são reaplicados. Destinos com pending changes do merge anterior no escopo aparecem **Bloqueados** (proteção contra repetição cega). Execute novamente só com `--target` nos destinos que faltam.

## Localizar logs sem expor credenciais

- Histórico: `%LOCALAPPDATA%\PepCli\history\<id>.json` (`pep history show <id>` mostra o caminho).
- Contém comando, parâmetros, versões, etapas, exit codes e trecho da saída das ferramentas (máx. 4 000 caracteres por etapa).
- Não contém senhas, tokens ou conteúdo de arquivos: o CLI não recebe credenciais (usa a autenticação do Visual Studio).

## Troubleshooting

| Mensagem | Causa provável | Ação |
|---|---|---|
| `TF400324` / servidor indisponível | VPN desligada, proxy, certificado SSL | Conecte a VPN, abra a coleção no navegador, `pep doctor` |
| `TF30063` / sem autenticação | tf.exe de linha de comando sem credencial em cache (o login do Visual Studio não é compartilhado) ou sem permissão | `pep login` em um terminal, entre com a conta da coleção, depois `pep doctor` |
| Ferramenta ausente: tf.exe / MSBuild | VS sem Team Explorer, caminho configurado errado | Instale o componente ou ajuste `ferramentas.tfExe` / `msBuild` |
| Configuração inválida | JSON ou regra violada | `pep config validate` mostra os campos |
| Versão desconhecida/ambígua | Token parecido ou alias duplicado | Use o id completo (`pep env list`) |
| Changeset não contém itens do projeto | Projeto/origem errados | `pep changeset show <id> --project back --source atual` |
| Sem relação de merge | Branches sem hierarquia TFVC | Não há baseless automático; trate manualmente com o time |
| Pending changes no escopo / gravável sem checkout | Trabalho local nos mesmos arquivos | Faça check-in ou desfaça manualmente e rode de novo |
| Arquivo bloqueado (broker) | RM/Host aberto | `pep kill host --version <v>` |
| Build bloqueado por RM.Host | Host da versão aberto | `pep kill host --pid <n>` |
| Espaço insuficiente | Disco da raiz com < 5 GB | Libere espaço (aviso no `doctor`) |
| Processo não localizado | PID já encerrado | `pep kill host` para listar |
| Cancelado | Ctrl+C | Inspecione `pep pending list`; cancelamento não é rollback |
