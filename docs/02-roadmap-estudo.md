# Payment Gateway — Roteiro de Estudo (fase a fase)

> Este é o seu roteiro. **Você implementa; eu guio.** Cada fase tem: objetivo, o que
> estudar (com o *porquê*), um checklist de tarefas e o "critério de pronto".
> As tarefas já estão numeradas para virarem **issues no GitHub** (ver seção final).

Legenda: 🎯 = conceito-estrela · 📚 = tópico pra estudar · ✅ = tarefa (vira issue)

---

## Fase 0 — Esqueleto da API + Swagger
**Objetivo:** ter uma Web API .NET 10 de pé, com Swagger e a máquina de estados básica
(ainda síncrona, em memória). Foco no *contrato* antes da infra.

📚 Estudar:
- Minimal APIs vs Controllers no .NET 10 (quando usar cada um)
- OpenAPI nativo (`Microsoft.AspNetCore.OpenApi`) e como expor a Swagger UI
- Problem Details (RFC 7807) para erros

✅ Tarefas:
- [ ] 0.1 Criar solution + os 5 projetos de código (`Core`, `Domain`, `Contracts`, `Api`,
      `Worker`) + testes, com as referências (Core◄Domain◄Api/Worker; Contracts puro)
- [ ] 0.2 Configurar OpenAPI + Swagger UI (Swashbuckle ou Scalar)
- [ ] 0.3 `Core`: validadores universais (e-mail/formato), Guard, `Result`/`Error`
- [ ] 0.4 Modelar `Payment` e o enum de status no `Domain` (em memória por enquanto)
- [ ] 0.5 Implementar `POST /payments` (cria Pending) e `GET /payments/{id}`
- [ ] 0.6 Implementar a máquina de estados no `Domain` (transições válidas/inválidas)
- [ ] 0.7 `GET /health`
- [ ] 0.8 `PaymentGateway.Domain.Tests` — testar transições, `Money`/`EmailAddress` VOs e
      validadores do Core

**Pronto quando:** Swagger abre, dá pra criar e consultar um pagamento, e os testes de
transição passam.

---

## Fase 1 — Idempotência 🎯 (o carro-chefe)
**Objetivo:** `POST /payments` idempotente de verdade.

📚 Estudar:
- Padrão de `Idempotency-Key` (docs da Stripe e da Pagar.me são a melhor referência)
- Hash/fingerprint canônico de request (por que canonicalizar o JSON)
- Constraint única como ferramenta de concorrência

✅ Tarefas:
- [ ] 1.1 Ler e escrever no `docs/` seu resumo do algoritmo (ensina consolidando)
- [ ] 1.2 Middleware/filtro que lê o header `Idempotency-Key`
- [ ] 1.3 Store de idempotência (em memória primeiro) com chave + hash + snapshot
- [ ] 1.4 Ramo de **replay** (mesma chave, mesmo hash → resposta guardada)
- [ ] 1.5 Ramo de **conflito** (mesma chave, hash diferente → 409)
- [ ] 1.6 TTL/expiração das chaves
- [ ] 1.7 Testar: 3 requisições (nova, replay, conflito) + concorrência

**Pronto quando:** reenviar a mesma requisição **não** cria segundo pagamento; body
diferente com a mesma chave dá 409.

---

## Fase 2 — Mensageria com RabbitMQ puro
**Objetivo:** tirar a autorização do caminho síncrono. A API publica evento; o Worker
consome e processa.

📚 Estudar:
- AMQP: exchange, queue, binding, routing key
- `ack`/`nack`, prefetch, e por que "at-least-once" (não exactly-once)
- Dead-letter queue e retry

✅ Tarefas:
- [ ] 2.1 Projeto `PaymentGateway.Contracts` com os eventos (records `...V1`)
- [ ] 2.2 Publisher na API (`PaymentAuthorizationRequested`) → 202
- [ ] 2.3 Projeto `PaymentGateway.Worker` (Worker Service) consumindo a fila
- [ ] 2.4 Worker aplica autorização (regra simulada) e avança estado
- [ ] 2.5 Configurar DLQ + política de retry
- [ ] 2.6 Rodar RabbitMQ local via Docker só pra esta fase

**Pronto quando:** criar um pagamento → ver a mensagem no RabbitMQ → o Worker processar e o
`GET` refletir o novo estado.

---

## Fase 3 — Persistência + Transactional Outbox
**Objetivo:** PostgreSQL de verdade e publicação consistente (nunca "gravou mas não
publicou").

📚 Estudar:
- EF Core + Postgres (Npgsql), migrations
- Transactional Outbox pattern (por que publicar direto na fila é arriscado)
- Concorrência otimista com `version`/`xmin`

✅ Tarefas:
- [ ] 3.1 EF Core + Npgsql + migrations para `Payment`, `IdempotencyKey`, `OutboxMessage`
- [ ] 3.2 Gravar Payment + OutboxMessage na **mesma transação**
- [ ] 3.3 Dispatcher que lê a Outbox e publica no RabbitMQ (marca `processedAt`)
- [ ] 3.4 Migrar o store de idempotência para o banco (constraint única real)
- [ ] 3.5 Concorrência otimista na atualização de estado

**Pronto quando:** derrubar o RabbitMQ na hora do POST **não** perde o evento — ele sai
assim que a fila volta.

---

## Fase 4 — Identidade & Auth (JWT → API keys)
**Objetivo:** proteger a Payment API com **auth distribuída**. Um serviço `Identity` emite
JWT; a Payment API valida. Caminho progressivo: **A (JWT)** primeiro, depois **B (API keys)**
— terminando com os dois convivendo (**C**). Bounded context isolado, DB próprio.

📚 Estudar:
- ASP.NET Core Identity (user store, hash de senha, roles)
- JWT: claims, assinatura, expiração; por que o recurso valida **sem** chamar o emissor (stateless)
- JWT Bearer authentication/authorization no ASP.NET Core
- (parte B) API keys M2M: geração, armazenamento com **hash**, custom `AuthenticationHandler`

✅ Tarefas — A (JWT):
- [ ] 4.1 Criar `PaymentGateway.Identity` (webapi, DB próprio) — usa `Core`, NÃO usa `Domain`
- [ ] 4.2 ASP.NET Core Identity + EF (tabelas de usuário)
- [ ] 4.3 Endpoints `register`/`login` que emitem JWT assinado (com claims)
- [ ] 4.4 Proteger a Payment API com JWT Bearer (`[Authorize]`), validando o token do Identity
- [ ] 4.5 Refresh token (opcional)
- [ ] 4.6 Testar: sem token → 401; com token válido → 200

✅ Tarefas — B (API keys, só depois de A funcionar):
- [ ] 4.7 Modelar `Merchant` + `ApiKey` (guardada com hash)
- [ ] 4.8 Custom `AuthenticationHandler` de API key nos endpoints de pagamento
- [ ] 4.9 Endpoint para gerar/rotacionar a key do merchant

**Pronto quando:** chamar `/payments` sem credencial dá **401**; com JWT (usuário) **ou**
API key (merchant) válida, passa. (= C: humanos por JWT, máquinas por API key.)

---

## Fase 5 — Notificações: e-mail + webhook
**Objetivo:** avisar o resultado. É aqui que sua meta de "aprender e-mail" fecha.

📚 Estudar:
- Envio SMTP no .NET (`MailKit` é o padrão de mercado)
- Papercut/MailHog como inbox de desenvolvimento
- Webhooks: entrega confiável, assinatura HMAC, idempotência do lado do receptor

✅ Tarefas:
- [ ] 5.1 Serviço de e-mail (recibo ao capturar) via SMTP → Papercut
- [ ] 5.2 Template simples do recibo
- [ ] 5.3 Cadastro de webhook do merchant (URL)
- [ ] 5.4 Dispatch do webhook nas transições, com retry
- [ ] 5.5 Assinatura HMAC do payload do webhook

**Pronto quando:** capturar um pagamento gera um e-mail no Papercut e um POST no endpoint
de webhook de teste.

---

## Fase 6 — Docker Compose completo
**Objetivo:** `docker compose up` sobe tudo.

📚 Estudar:
- Dockerfile multi-stage para .NET (build + runtime enxuto)
- `depends_on`, healthchecks, redes e volumes no Compose

✅ Tarefas:
- [ ] 6.1 `Dockerfile.api`, `Dockerfile.worker` e `Dockerfile.identity` (multi-stage)
- [ ] 6.2 `docker-compose.yml`: api, worker, identity, rabbitmq, postgres, papercut
- [ ] 6.3 Healthchecks e ordem de subida
- [ ] 6.4 README com "como rodar"

**Pronto quando:** máquina limpa + `docker compose up` = tudo funcionando.

---

## Fase 7 — CI/CD no GitHub Actions
**Objetivo:** pipeline automatizado a cada push/PR.

📚 Estudar:
- Estrutura de workflow (jobs, steps, matrix)
- Cache de restore, build de imagem Docker no CI

✅ Tarefas:
- [ ] 7.1 Workflow `ci.yml`: restore → build → test
- [ ] 7.2 Job de `docker build` das imagens (api, worker, identity)
- [ ] 7.3 Badge de status no README
- [ ] 7.4 (opção) push das imagens para o GitHub Container Registry

**Pronto quando:** abrir um PR dispara build + testes verdes automaticamente.

---

## Fase 8 — Bônus (resiliência, observabilidade, MassTransit, K8s)
Escolha o que te interessar mais — cada um é um mini-estudo:
- [ ] 8.1 **Migrar mensageria para MassTransit** e comparar com o RabbitMQ puro
- [ ] 8.2 **Polly**: retry, circuit breaker, timeout no webhook/SMTP
- [ ] 8.3 **Serilog** (log estruturado) + **OpenTelemetry** (tracing distribuído API↔Worker)
- [ ] 8.4 **Rate limiting** e **API versioning**
- [ ] 8.5 Idempotência também no consumidor (dedupe de mensagem no Worker)
- [ ] 8.6 Separar o `Identity` na própria solution/repo (Contracts via NuGet) — sentir o polyrepo
- [ ] 8.7 **Kubernetes**: manifests (Deployment/Service/Ingress) e subir o stack num cluster
      local (kind/minikube). Só depois do Docker Compose estar sólido.

---

## Mapeamento para GitHub

Sugestão de organização quando formos criar as issues:

- **Milestones:** uma por fase (`Fase 0` … `Fase 8`).
- **Labels:** `fase-0`…`fase-8`, `idempotência`, `mensageria`, `identidade`, `auth`,
  `docker`, `ci-cd`, `notificações`, `domínio`, `bônus`.
- **Issues:** uma por tarefa `X.Y` acima (título = texto da tarefa; corpo = o "estudar" +
  "critério de pronto" da fase).
- **Project (board):** colunas `Backlog → Em andamento → Em revisão → Concluído`.

Total: ~45 issues. Dá pra criar tudo de uma vez ou fase a fase — recomendo fase a fase pra
não poluir o board.
