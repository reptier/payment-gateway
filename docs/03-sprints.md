# Payment Gateway — Plano de Sprints

> Organização das tarefas do [roadmap](02-roadmap-estudo.md) em **sprints de 2 semanas**.
> Capacidade assumida: **~3 dias de esforço/semana** → **~6 dias de esforço por sprint**.
> Estimativas em **dias de esforço** (1 dia = 8h), piso de 1 dia por tarefa.
> Repositório: https://github.com/reptier/payment-gateway

- **Início:** 2026-08-01
- **Total:** 48 tarefas · ~57 dias de esforço · 10 sprints · ~5 meses de calendário
- **Milestones no GitHub:** `Sprint 1` … `Sprint 10` (com data de entrega)

---

## Visão geral

| Sprint | Período | Foco | Esforço | Entrega |
|---|---|---|---|---|
| 1 | 01/08–15/08 | Fundação (Fase 0) | 6d | Solution, Swagger, máquina de estados |
| 2 | 15/08–29/08 | Fecha Fase 0 + Idempotência | 6d | Testes de domínio + início da idempotência |
| 3 | 29/08–12/09 | Idempotência + Mensageria | 5d | Idempotência pronta, começa RabbitMQ |
| 4 | 12/09–26/09 | Mensageria (Fase 2) | 6d | Worker consumindo, DLQ/retry |
| 5 | 26/09–10/10 | Persistência (Fase 3) | 6d | Postgres + Outbox |
| 6 | 10/10–24/10 | Fecha Fase 3 + Identidade | 6d | Concorrência + início do JWT |
| 7 | 24/10–07/11 | Identidade JWT (Fase 4A) | 6d | Login/JWT + API protegida + API keys |
| 8 | 07/11–21/11 | Fecha Identidade + Notificações | 6d | API keys + e-mail/webhook |
| 9 | 21/11–05/12 | Notificações + Docker | 6d | HMAC + Docker Compose |
| 10 | 05/12–19/12 | CI/CD (Fase 7) | 4d | Pipeline GitHub Actions |

---

## Detalhe por sprint

### Sprint 1 — Fundação · entrega 15/08 · 6d
- **0.1** Criar solution + 5 projetos + referências — 1d
- **0.2** Configurar OpenAPI + Swagger UI — 1d
- **0.3** Core: validadores universais, Guard, Result/Error — 1d
- **0.4** Modelar Payment e enum de status (em memória) — 1d
- **0.5** Implementar POST /payments e GET /payments/{id} — 1d
- **0.6** Implementar máquina de estados no Domain — 1d

### Sprint 2 — Fecha Fase 0 + Idempotência · entrega 29/08 · 6d
- **0.7** GET /health — 1d
- **0.8** Domain.Tests: transições, Money/EmailAddress, validadores — 1d
- **1.1** Documentar o algoritmo de idempotência — 1d
- **1.2** Middleware/filtro que lê Idempotency-Key — 1d
- **1.3** Store de idempotência (em memória) — 1d
- **1.4** Ramo de replay (mesma chave, mesmo hash) — 1d

### Sprint 3 — Idempotência + Mensageria · entrega 12/09 · 5d
- **1.5** Ramo de conflito (hash diferente → 409) — 1d
- **1.6** TTL/expiração das chaves — 1d
- **1.7** Testar idempotência (nova, replay, conflito, concorrência) — 1d
- **2.1** Contracts: eventos (records V1) — 1d
- **2.2** Publisher na API → 202 — 1d

### Sprint 4 — Mensageria · entrega 26/09 · 6d
- **2.3** Worker consumindo a fila — 2d
- **2.4** Worker aplica autorização e avança estado — 1d
- **2.5** Configurar DLQ + política de retry — 2d
- **2.6** Rodar RabbitMQ local via Docker — 1d

### Sprint 5 — Persistência · entrega 10/10 · 6d
- **3.1** EF Core + Npgsql + migrations — 2d
- **3.2** Gravar Payment + OutboxMessage na mesma transação — 1d
- **3.3** Outbox dispatcher (lê e publica no RabbitMQ) — 2d
- **3.4** Migrar store de idempotência para o banco — 1d

### Sprint 6 — Fecha Fase 3 + Identidade · entrega 24/10 · 6d
- **3.5** Concorrência otimista na atualização de estado — 1d
- **4.1** Criar PaymentGateway.Identity (webapi, DB próprio) — 1d
- **4.2** ASP.NET Core Identity + EF (tabelas de usuário) — 2d
- **4.3** Endpoints register/login emitindo JWT — 2d

### Sprint 7 — Identidade JWT + API keys · entrega 07/11 · 6d
- **4.4** Proteger Payment API com JWT Bearer — 1d
- **4.5** Refresh token (opcional) — 1d
- **4.6** Testar auth (401 sem token, 200 com token) — 1d
- **4.7** Modelar Merchant + ApiKey (hash) — 1d
- **4.8** Custom AuthenticationHandler de API key — 2d

### Sprint 8 — Fecha Identidade + Notificações · entrega 21/11 · 6d
- **4.9** Endpoint para gerar/rotacionar key do merchant — 1d
- **5.1** Serviço de e-mail (recibo) via SMTP → Papercut — 1d
- **5.2** Template do recibo — 1d
- **5.3** Cadastro de webhook do merchant (URL) — 1d
- **5.4** Dispatch do webhook nas transições, com retry — 2d

### Sprint 9 — Notificações + Docker · entrega 05/12 · 6d
- **5.5** Assinatura HMAC do payload do webhook — 1d
- **6.1** Dockerfiles multi-stage (api, worker, identity) — 2d
- **6.2** docker-compose.yml (todos os serviços) — 1d
- **6.3** Healthchecks e ordem de subida — 1d
- **6.4** README com "como rodar" — 1d

### Sprint 10 — CI/CD · entrega 19/12 · 4d
- **7.1** Workflow ci.yml: restore → build → test — 1d
- **7.2** Job de docker build das imagens — 1d
- **7.3** Badge de status no README — 1d
- **7.4** Push das imagens para o GHCR (opção) — 1d

---

## Backlog (Fase 8 — Bônus, fora dos sprints)
Puxar pra um sprint futuro conforme o interesse: MassTransit, Polly, Serilog/OpenTelemetry,
rate limiting/versioning, dedupe no consumidor, split do Identity em repo próprio, Kubernetes.

## Observações
- As datas são **calendário**; o esforço é o que cabe dentro. Se um sprint atrasar, empurra
  os seguintes — os milestones no GitHub servem de termômetro, não de contrato.
- Reestimar é esperado: ao fim de cada sprint, ajuste as estimativas dos próximos com o que
  aprendeu sobre seu próprio ritmo (isso *é* parte do estudo de gestão de projeto).
