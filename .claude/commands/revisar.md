---
description: Revisa o código de uma issue contra os invariantes de docs/01-design.md
argument-hint: <numero-da-issue>
---

Revise a issue #$ARGUMENTS usando o agente `revisor-payment-gateway`.

Antes de despachar o agente, descubra o range de commits a revisar:

1. `gh issue view $ARGUMENTS --json title,body,milestone,labels,closedAt`
2. Ache os commits que mencionam a issue:
   `git log --oneline --all --grep="#$ARGUMENTS"`
3. Se nenhum commit mencionar a issue, use `git log --oneline -10` e me pergunte qual é o
   range antes de prosseguir — não adivinhe.
4. Verifique se há trabalho não commitado com `git status --porcelain`. Se houver, avise o
   agente de que a árvore está suja e quais arquivos não fazem parte da issue.

Passe ao agente: o número da issue, o range de commits, e o aviso sobre árvore suja se
aplicável.

Ao receber o relatório, repasse-o para mim por inteiro — veredito, achados com gravidade,
o que ficou bom e o que fica para depois. Se algum achado contradisser algo que você
afirmou antes nesta conversa, verifique você mesmo antes de repassar.

Não corrija nada. O relatório é o entregável.
