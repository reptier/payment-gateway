# Fluxo de revisão

> Como uma issue sai de `Todo` e chega em `Done`, e onde entra a revisão automatizada.
> Board: https://github.com/users/reptier/projects/1

## Estados do card

| Status | Significado | Quem move |
|---|---|---|
| `Todo` | não começada | — |
| `In Progress` | em implementação | você, ao começar |
| `Code Review` | issue fechada, código aguardando revisão | automação (issue fechada) |
| `Done` | revisada e aprovada | você, depois de ler a revisão |

O ponto do `Code Review` é que **fechar a issue não significa aprovado**. Num projeto
solo não existe outro revisor, e é justamente aí que vício de arquitetura passa batido.

## O ciclo

1. Move o card para `In Progress`.
2. Implementa. Commit com a palavra-chave de fechamento na mensagem:
   ```
   feat: implementa a maquina de estados do Payment

   Closes #6
   ```
   Funciona em commit direto na `main` — não precisa de PR.
3. `git push`. O GitHub fecha a issue e a automação move o card para `Code Review`.
4. Roda a revisão (ver abaixo) e lê os achados.
5. Se aprovado, move para `Done`. Se houver `bloqueio`, corrige e commita de novo —
   o card fica em `Code Review` até você aprovar.

## Rodando a revisão

O agente vive em [.claude/agents/revisor-payment-gateway.md](../.claude/agents/revisor-payment-gateway.md)
e só carrega se o Claude Code for aberto **na raiz deste repositório**:

```bash
cd C:\Users\lucas\source\repos\PaymentGateway
claude
```

Depois, no prompt:

```
Use o agente revisor-payment-gateway para revisar a issue #6 (commits 55ba0df..HEAD)
```

Ele lê o design, lê o corpo da issue, roda `dotnet build`, e devolve veredito + achados
com gravidade. Ele **não corrige** nada — aponta e explica, você implementa.

## O que o agente verifica

Os invariantes de [01-design.md](01-design.md), resumidos:

- direção de dependências nunca inverte (`Core ◄ Domain ◄ Api/Worker`; `Contracts` isolado)
- nomenclatura `PaymentGateway.*`
- teste **só de domínio** — teste de API ou integração é escopo excedido
- sem comentários no código; nomes descritivos em português
- nenhuma fonte NuGet além de `nuget.org`
- nada de trabalho antecipado de issue futura

## Configuração da automação (feita uma vez, na UI)

No board: ⚙️ → **Workflows** → **Item closed** → `Set Status` para **Code Review**.

Ligue também **Auto-add to project** com o filtro `is:issue`, para issue nova entrar
no board sozinha.

> Para adicionar opção nova ao campo `Status`, use **sempre a UI**. A mutation
> `updateProjectV2Field` da API recria as opções e apaga o status de todos os cards.
