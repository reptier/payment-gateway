# Payment Gateway (projeto de estudo)

Microsserviço de **estudo** em **.NET 10** que simula um gateway de pagamentos
(mini-Stripe/Pagar.me). Não movimenta dinheiro real — é modelagem de software para praticar
arquitetura de microsserviços, mensageria e integridade de dados.

> **Status:** dia zero — planejamento e estrutura. A implementação segue o
> [plano de sprints](docs/03-sprints.md).

## O que este projeto exercita
- **Idempotência** (carro-chefe) — `Idempotency-Key`, replay e conflito
- **Mensageria** — RabbitMQ (producer/consumer, DLQ, retry)
- **Máquina de estados** de pagamento (Pending → Authorized → Captured → …)
- **Transactional Outbox** e concorrência otimista (PostgreSQL + EF Core)
- **Identidade & Auth** — ASP.NET Core Identity + JWT, e API keys de merchant (M2M)
- **Notificações** — e-mail (recibo) + webhooks
- **Docker Compose** e **CI/CD** (GitHub Actions)

## Documentação
- [Design / escopo](docs/01-design.md)
- [Roadmap de estudo (fases)](docs/02-roadmap-estudo.md)
- [Plano de sprints](docs/03-sprints.md)

## Estrutura planejada
```
src/
  PaymentGateway.Core/         validadores universais, Guard, Result/Error
  PaymentGateway.Domain/       Payment, máquina de estados, VOs
  PaymentGateway.Contracts/    eventos (payloads)
  PaymentGateway.Api/          Web API + Swagger (producer)
  PaymentGateway.Worker/       consumer + e-mail + webhook
  PaymentGateway.Identity/     ASP.NET Core Identity + JWT
tests/
  PaymentGateway.Domain.Tests/ testes de domínio
```

## Acompanhamento
Tarefas organizadas em **sprints de 2 semanas** nas
[issues](https://github.com/reptier/payment-gateway/issues) e nos
[milestones](https://github.com/reptier/payment-gateway/milestones) do repositório.
