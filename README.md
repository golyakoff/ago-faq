# AGO FAQ

[![CI](https://github.com/golyakoff/ago-faq/actions/workflows/ci.yml/badge.svg)](https://github.com/golyakoff/ago-faq/actions/workflows/ci.yml)

A small AI knowledge-base module for AGO Chat: a visitor's routine question (return policy, business
hours, shipping cost) is answered by an LLM grounded in a knowledge base the tenant supplies - through
the same chat-module contract AGO Calendar already uses (`../ago-root/docs/adr/0065-*`,
`../ago-root/docs/adr/0077-*`). `Ago.Chat.*` gains no new knowledge of what "FAQ" means, the same way it
gained no knowledge of what "booking" means for Calendar.

This repository holds the module's Domain, Application, Contracts, Infrastructure and Module, plus the
one deployable built from them: `Ago.Faq.Api`. There is deliberately no `Worker` and no `Webhooks` host
- this module has exactly one failure profile (answer an HTTP request), no async pipeline and no
webhook source, unlike `Ago.Chat.*` or `Ago.Calendar.*`'s multi-host split
(`../ago-root/docs/adr/0013-*`). See `../ago-root/docs/backlog/19-03-ai-faq-module.md`'s own "Decided"
section for the full reasoning trail behind that choice, the knowledge-base storage format, and the
low-confidence escape.

It consumes `Ago.Platform.*` as NuGet packages and **never** reaches into the platform's source -
except through the dev override below, which must never survive to a merged branch.

## The wire contract

`POST /api/v1/module-tasks` and `POST /api/v1/module-tasks/{externalTaskId}/replies` -
`AllowAnonymous`, server-to-server, hand-synchronized with `Ago.Chat.Infrastructure.Modules.ModuleWireContract`
in `ago-chat`. See `src/Ago.Faq.Contracts/ModuleTaskContracts.cs` for the exact field-level shape and
`src/Ago.Faq.Api/ModuleTasks/ModuleTaskEndpoints.cs` for the endpoint. No service-to-service
authentication exists on this surface yet - `../ago-root/docs/adr/0077-*` already names this as a real,
accepted gap for Calendar's identical surface, extended here without re-litigating it.

The low-confidence escape (a knowledge base that does not cover a question, or an unreachable/unconfigured
LLM provider) uses Chat's fifth closed-vocabulary primitive, `"escalate"` -
`../ago-root/docs/adr/0081-*` records the full design.

## The knowledge base

One plain-text row per site (`Ago.Faq.Domain.KnowledgeBase`), bounded at 8000 characters - "a few
paragraphs", not a document-ingestion pipeline. The whole text is passed to the LLM as context on every
question; no retrieval step exists because none is needed at this scale. A tenant supplies/edits it
through `GET`/`PUT /api/v1/sites/{siteId}/knowledge-base`
(`src/Ago.Faq.Api/KnowledgeBase/KnowledgeBaseEndpoints.cs`), authenticated with the same shared
Keycloak-issued operator JWT `ago-chat`'s own console already obtains - no second Keycloak client. See
that endpoint's own remarks for a real, named gap: this pass does not re-verify that the authenticated
operator specifically holds a permission over the named site.

## Build

```bash
cd C:/git/ago/ago-faq
dotnet restore Ago.Faq.slnx
dotnet format Ago.Faq.slnx --verify-no-changes
dotnet build Ago.Faq.slnx --no-restore -c Release
dotnet test Ago.Faq.slnx --no-build -c Release
```

`nuget.config` restores `Ago.Platform.*` from the local file feed
(`../ago-root/docs/runbooks/workspace.md`); pack `ago-platform` into it first if it is empty. CI uses
`nuget.ci.config` and the real GitHub Packages feed instead (`../ago-root/docs/adr/0018-*`).

## Database

Its **own** Postgres database, never a schema inside `ago-chat`'s or `ago-calendar`'s. `dotnet ef`
needs a connection string, supplied through an environment variable so no credential shape is ever
committed here:

```bash
cd C:/git/ago/ago-faq
export AGO_FAQ_CONNECTION_STRING="Host=localhost;Port=5432;Database=ago_faq;Username=...;Password=..."
dotnet ef migrations add <StageVerbSubject> \
  -p src/Ago.Faq.Infrastructure.Postgres -s src/Ago.Faq.Infrastructure.Postgres
```

`Ago.Faq.Integration.Tests` needs a **running Docker daemon**: it starts a real Postgres through
Testcontainers and applies the migrations from scratch.

## The LLM call

`Ago.Faq.Infrastructure.OpenAiCompatible.OpenAiCompatibleFaqAnswerGenerator` speaks the OpenAI Chat
Completions HTTP shape - hand-rolled JSON DTOs and `System.Net.Http.Json`, no LLM SDK package, matching
every provider client in `ago-chat`. It never throws: every failure (network fault, timeout, non-2xx, or
an unparseable response) resolves to `FaqAnswerResult.Unavailable` directly, which
`AnswerFaqQuestionHandler` maps to the low-confidence escalation. Config keys:
`FaqAnswer:OpenAiCompatible:{ApiKey,BaseUrl,Model,MaxTokens}` - deliberately **not**
`.ValidateOnStart()`'d (no environment has real credentials for this yet); `Ago.Faq.Module.FaqModule`
registers `UnconfiguredFaqAnswerGenerator` instead of the real client when `ApiKey`/`BaseUrl` are blank,
so a misconfigured or absent provider degrades this one feature rather than crash-looping the host.

**Not confirmed against a real provider** - no real API key exists in any environment this project runs
in yet. `Ago.Faq.Integration.Tests` proves the request shape and every documented response/error path
against a fake, in-process HTTP host standing in for one.

## Rules

- Layering and what goes where: `../ago-root/docs/architecture/clean-architecture.md`
- The module contract this repository implements: `../ago-root/docs/adr/0065-*`, `../ago-root/docs/adr/0077-*`
- The low-confidence escape: `../ago-root/docs/adr/0081-*`
- Decisions: `../ago-root/docs/adr/`
- Working agreements: `../ago-root/CLAUDE.md`

## Dev override

For a change that genuinely spans this repository and `ago-platform`, set `AgoFaqDevOverride` to build
against a sibling `../ago-platform` checkout instead of the published package:

```bash
cd C:/git/ago/ago-faq
AgoFaqDevOverride=true dotnet build
```

**A branch that gets merged must build against the published package.** CI never sets this variable.
