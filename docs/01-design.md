# Payment Gateway — Documento de Design

> Microsserviço de **estudo** que simula um gateway de pagamentos (mini-Stripe/Pagar.me).
> **Não** movimenta dinheiro real, não integra banco de verdade e não é aconselhamento
> financeiro — é modelagem de software para praticar arquitetura de microsserviços,
> mensageria e integridade de dados.

- **Stack:** .NET 10, RabbitMQ, PostgreSQL, Docker, GitHub Actions
- **Carro-chefe do aprendizado:** Idempotência
- **Data:** 2026-07-31

---

## 1. Objetivo e escopo

Construir um serviço que recebe "pagamentos", processa-os por uma **máquina de estados**
de forma **assíncrona** (via fila), garante **idempotência** de ponta a ponta e notifica o
resultado por **e-mail** e **webhook**.

O valor de estudo está nos conceitos exercitados, não no domínio em si:

| Conceito de mercado | Onde aparece no projeto |
|---|---|
| Idempotência com chave | `POST /payments` com header `Idempotency-Key` |
| Máquina de estados | Ciclo de vida do pagamento (Pending→Authorized→Captured…) |
| Mensageria (payload) | Evento publicado na fila e consumido pelo Worker |
| Transactional Outbox | Publicar evento + gravar no banco sem inconsistência |
| Concorrência otimista | `version` na entidade `Payment` |
| Dead-letter queue / retry | Falhas no consumo da mensagem |
| Notificações multicanal | E-mail (recibo) + Webhook (lojista) |
| Containers | Docker Compose com todos os serviços |
| CI/CD | Pipeline build/test/docker no GitHub Actions |

**Fora de escopo (YAGNI):** autenticação real de merchant, PCI/tokenização real de cartão,
multi-tenancy, painel web, múltiplas moedas com câmbio. Tudo pode virar "bônus" depois.

---

## 2. Arquitetura

Dois serviços (deployáveis separados) + um contrato compartilhado:

```
              ┌───────────────────────────┐        ┌──────────────────────────────┐
Cliente ────► │  PaymentGateway.Api        │        │  PaymentGateway.Worker        │
  POST        │  (Producer)                │        │  (Consumer)                   │
/payments     │                            │        │                               │
              │  • valida request          │        │  • consome fila               │
   202 ◄──────│  • idempotência            │  Rabbit│  • aplica máquina de estados  │
              │  • grava Pending           │───MQ──►│  • envia e-mail (recibo)      │
              │  • [Outbox] publica evento │        │  • dispara webhook            │
              └─────────────┬──────────────┘        └───────────────┬──────────────┘
                            │                                       │
                            ▼                                       ▼
                       PostgreSQL   ◄──────── mesmo banco ────────► PostgreSQL
                                                                     │
                                                          SMTP (Papercut dev inbox)
```

- **`PaymentGateway.Contracts`** — records dos eventos (payloads). Referenciado pelos dois
  serviços; nenhum conhece a implementação do outro, só o formato da mensagem.
- **`PaymentGateway.Api`** — Web API .NET 10 com Swagger. Faz validação, idempotência,
  persiste o pagamento e publica o evento (via Outbox).
- **`PaymentGateway.Worker`** — Worker Service que consome a fila, avança a máquina de
  estados, envia e-mail e dispara webhook.

### Por que dois serviços?
Deixa o papel da mensageria óbvio: a API responde rápido (`202 Accepted`) e o processamento
pesado/lento acontece em background, desacoplado. É o padrão real de sistemas de pagamento.

---

## 3. Máquina de estados (núcleo do domínio)

```
                     ┌─────────► Failed
                     │
Pending ──authorize──► Authorized ──capture──► Captured ──refund──► Refunded
                     │                  │
                     └──void──► Voided ◄─┘ (void só antes da captura)
```

| De → Para | Gatilho | Regra |
|---|---|---|
| Pending → Authorized | autorização aprovada | sempre a partir de Pending |
| Pending → Failed | autorização recusada | — |
| Authorized → Captured | captura | só se Authorized |
| Authorized → Voided | cancelamento | só antes da captura |
| Captured → Refunded | estorno | só se Captured |

Transições inválidas devem lançar erro de domínio (ex.: capturar um `Failed`, estornar um
`Pending`). **Esta é a parte testável do domínio** — cada transição válida e inválida vira
um teste unitário (alinhado à regra de só testar domínio: entidades e value objects).

Value objects candidatos: `Money` (valor + moeda, sem ponto flutuante — usar `decimal` e
menor unidade), `PaymentStatus`, `IdempotencyKey`.

---

## 4. Idempotência (carro-chefe) — algoritmo

O cliente envia o header `Idempotency-Key: <uuid>` no `POST /payments`.

```
1. Recebe request + Idempotency-Key
2. fingerprint = hash(corpo canônico do request)
3. Tenta inserir (chave, fingerprint) com CONSTRAINT ÚNICA na chave:
   ├─ inseriu (chave nova)        → processa, grava snapshot da resposta, retorna 202
   ├─ chave existe, MESMO hash    → REPLAY: retorna a resposta guardada (não reprocessa)
   └─ chave existe, hash DIFERENTE→ 409 Conflict (reuso indevido da chave)
4. Chaves têm TTL (ex.: 24h) e são limpas por um job/expiração
```

Detalhes importantes:
- **Concorrência:** duas requisições simultâneas com a mesma chave — a constraint única
  garante que só uma "ganha" a inserção; a outra cai no ramo de replay.
- **Snapshot da resposta:** guardar statusCode + corpo para devolver idêntico no replay.
- **Escopo da chave:** por merchant (a mesma chave de merchants diferentes é independente).

Referência de mercado: este é o mesmo modelo documentado por Stripe e Pagar.me para o
header `Idempotency-Key`.

---

## 5. Fluxo assíncrono + eventos (mensageria)

```
POST /payments (Idempotency-Key)
   └► API: valida → grava Payment(Pending) + OutboxMessage(PaymentAuthorizationRequested)
                    (tudo na MESMA transação de banco)
   └► 202 Accepted { id, status: "Pending" }

[Outbox dispatcher] lê OutboxMessage não publicadas → publica no RabbitMQ → marca publicada

RabbitMQ (exchange → queue) 
   └► Worker consome PaymentAuthorizationRequested
        → decide Authorized/Failed (regra simulada: ex. valor > X recusa)
        → atualiza Payment, publica PaymentAuthorized | PaymentFailed
        → (auto) capture → Captured → publica PaymentCaptured
             └► envia e-mail (recibo) + dispara webhook pro lojista
        → ack. Em falha → nack → retry → dead-letter queue
```

### Eventos (contratos / payloads)
- `PaymentAuthorizationRequested { paymentId, merchantId, amount, currency, method, occurredAt }`
- `PaymentAuthorized { paymentId, occurredAt }`
- `PaymentFailed { paymentId, reason, occurredAt }`
- `PaymentCaptured { paymentId, amount, occurredAt }`
- `PaymentRefunded { paymentId, amount, occurredAt }`

Versionar no nome (`...V1`) para praticar evolução de contrato.

---

## 6. API & Swagger

| Método | Rota | Descrição | Respostas |
|---|---|---|---|
| POST | `/payments` | Cria pagamento (idempotente) | 202, 400, 409 |
| GET | `/payments/{id}` | Consulta status | 200, 404 |
| POST | `/payments/{id}/capture` | Captura autorização | 200, 404, 409 |
| POST | `/payments/{id}/refund` | Estorna captura | 200, 404, 409 |
| POST | `/payments/{id}/void` | Cancela autorização | 200, 404, 409 |
| GET | `/health` | Health check (Docker/CI) | 200 |

- **OpenAPI nativo do .NET 10** (`Microsoft.AspNetCore.OpenApi`) + Swagger UI.
- Documentar exemplos de request/response e o header `Idempotency-Key`.
- Erros no formato **Problem Details** (RFC 7807).

### Exemplo `POST /payments`
```jsonc
// Headers: Idempotency-Key: 3f9a...  Content-Type: application/json
{
  "merchantId": "mrc_123",
  "amount": 1990,            // menor unidade (centavos)
  "currency": "BRL",
  "method": { "type": "card", "token": "tok_test_visa" },
  "customerEmail": "cliente@exemplo.com"
}
// 202 Accepted
{ "id": "pay_abc", "status": "Pending" }
```

---

## 7. Modelo de dados

- **Payment** — `id, merchantId, amount, currency, status, method, customerEmail,
  createdAt, updatedAt, version (concorrência otimista)`
- **IdempotencyKey** — `key (ÚNICA), merchantId, requestHash, paymentId, responseStatus,
  responseBody, createdAt, expiresAt`
- **OutboxMessage** — `id, type, payload(json), occurredAt, processedAt (null=pendente)`
- **PaymentEvent** (auditoria) — `id, paymentId, fromStatus, toStatus, at`

---

## 8. Estrutura de projetos

```
payment-gateway/
├─ src/
│  ├─ PaymentGateway.Core/         → validadores universais, Guard, Result/Error, bases (VO/Entity), hashing
│  ├─ PaymentGateway.Domain/       → entidade Payment, máquina de estados, Money/EmailAddress VOs
│  ├─ PaymentGateway.Contracts/    → eventos (payloads compartilhados) — puro, sem deps
│  ├─ PaymentGateway.Api/          → Web API + Swagger + idempotência + outbox (protegida por auth)
│  ├─ PaymentGateway.Worker/       → consumer + máquina de estados + e-mail + webhook
│  └─ PaymentGateway.Identity/     → serviço de identidade (ASP.NET Core Identity + JWT), DB próprio — bounded context isolado
├─ tests/
│  └─ PaymentGateway.Domain.Tests/ → transições de estado, Money VO, validadores do Core
├─ docker-compose.yml              → api + worker + rabbitmq + postgres + papercut
├─ .github/workflows/ci.yml
├─ Dockerfile.api / Dockerfile.worker
├─ docs/
│  ├─ 01-design.md                 → este documento
│  └─ 02-roadmap-estudo.md         → roteiro fase a fase
└─ README.md
```

### Direção de dependências (as setas nunca invertem)
```
Core  ◄──  Domain  ◄──  Api / Worker
  ▲                     Contracts (independente, sem deps)
  └──────────  Identity (bounded context isolado — usa Core, NÃO usa Domain)
```
- **`Core`** não depende de ninguém. Guarda o **transversal e genérico**: validação de
  *formato* (e-mail, string, faixa), Guard clauses, `Result`/`Error`, bases `ValueObject`/
  `Entity`, helpers de hashing (usado no fingerprint da idempotência). Não contém regra de
  negócio. Cuidado para não virar "saco de gato".
- **`Domain`** depende só do `Core`. Aqui mora a regra de negócio: `Payment`, a máquina de
  estados, e os VOs `Money` e `EmailAddress` (que *usam* o validador de formato do Core —
  Core valida o formato, Domain dá o significado de negócio).
- **`Contracts`** fica puro (só records dos eventos) — é o formato de fio, não referencia Core.
- **`Identity`** é outro *bounded context*: tem o próprio modelo de usuário e o próprio DB.
  Pode usar `Core` (validadores/guards), mas **nunca** referencia o `Domain` de pagamento —
  os dois serviços só se conhecem via **JWT** (o Identity emite, a Payment API valida).

Mantido enxuto de propósito: **sem** Clean Architecture de 4 camadas por serviço, porque o
foco é mensageria + integridade, não DDD tático. Application/Infra ficam dentro de Api e
Worker.

---

## 9. Docker & CI/CD

- **docker-compose:** RabbitMQ (management UI :15672), PostgreSQL, Papercut (inbox web),
  API e Worker. Um `docker compose up` sobe tudo.
- **GitHub Actions (`ci.yml`):** `restore → build → test → docker build` das duas imagens.
  Evoluir depois para push em registry.

---

## 10. Decisões e alternativas

| Decisão | Escolha | Alternativa considerada |
|---|---|---|
| Mensageria (fase 2) | RabbitMQ.Client puro | MassTransit (fica p/ fase bônus, comparar) |
| Topologia | 2 serviços (API + Worker) | 1 serviço com BackgroundService |
| Banco | PostgreSQL | SQL Server / in-memory (fase 0) |
| SMTP | Papercut (dev inbox) | MailHog / SMTP real |
| Recorte de domínio | Gateway de pagamento | Carteira+transferências / Ledger puro |
| Identidade | ASP.NET Core Identity + JWT | OpenIddict (OAuth2/OIDC) / custom mínimo |
| Estrutura de solution | Uma solution, contextos isolados | Solutions/repos separados (fase avançada) |

---

## 11. Identidade & autenticação (auth distribuída)

Serviço `PaymentGateway.Identity` — **bounded context isolado**, com DB próprio de usuários.
Não referencia o Domain de pagamento. A Payment API passa a exigir autenticação.

### Caminho progressivo A → B (= C no final)

**A) JWT para usuários (login)** — o padrão de auth distribuída:
```
POST /identity/login { email, senha }  →  200 { accessToken: "eyJhbGc..." }
POST /payments   Authorization: Bearer eyJhbGc...
   └► a Payment API valida a ASSINATURA e as CLAIMS do token localmente,
      SEM chamar o Identity (stateless). É o pulo do gato: um serviço emite, o outro confia.
```
- Implementação: **ASP.NET Core Identity** (user store, hash de senha, roles) + emissão de
  **JWT** assinado. A Payment API usa **JWT Bearer** para validar.
- Chave de assinatura compartilhada por configuração (evoluir depois para JWKS/endpoint).

**B) API keys para merchants (M2M)** — autêntico ao domínio de pagamento:
```
POST /payments   Authorization: Bearer sk_live_9f3a...
   └► a API busca a key (comparando por HASH), identifica o merchant e checa se está ativa.
```
- Modelar `Merchant` + `ApiKey` (guardada com **hash**, nunca em texto puro).
- Custom `AuthenticationHandler` de API key nos endpoints de pagamento.

**Resultado (C):** JWT para humanos (painel) + API keys para máquinas (servidor do lojista)
convivendo, cada endpoint aceitando o esquema adequado.

### Nota de segurança
Emitir JWT "na mão" é excelente para **aprender os fundamentos**, mas em produção prefira um
IdP consolidado (**OpenIddict**, **Duende IdentityServer** ou **Keycloak**). Nunca versione
segredos/chaves no repositório — use user-secrets/variáveis de ambiente.
