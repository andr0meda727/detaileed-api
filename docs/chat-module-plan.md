# Plan: Chat module (customer ↔ studio, live over SignalR)

Status: backend done (2026-11-21), steps 0–9 below; the `InitialChat` migration still has to go out to Supabase as in
`docs/database.md`. The frontend is separate work. Where the build differed from the first draft (cursor paging,
open endpoints with a body, `TrySaveChangesAsync`, the rename done in SQL), this document was updated to match.

## Context
Marketplace customers and studios have no way to talk today apart from booking requests and emails. The goal is a
Messenger-like chat: messages are stored in the database (the source of truth for history, unread counts and catching
up after being offline) and delivered live through SignalR (the server pushes instead of clients polling the database).
Nothing real-time exists in the codebase yet.

Decisions:
- **One thread per studio–customer pair** (unique `(StudioId, CustomerUserId)`).
- A **customer** can start a conversation with any published studio; a **studio** only with a customer who has sent it
  a booking request.
- The studio side is a **shared inbox**: every active staff member sees and answers conversations; the customer sees
  the studio's name. Read state is per side, not per person: when one staff member reads a conversation, it is read
  for the whole studio (and the customer's read receipt says "the studio has read it").
- A studio that stops being published keeps its existing conversations: both sides can still read and write in them.
  Only **opening** a conversation as a customer needs a published studio.
- Nobody chats with their own studio: a customer who is an active member of the studio gets `CHAT_OWN_STUDIO`.
- MVP: text, history, live push, unread counts (per conversation and a total for the badge) **+ live read receipts**.
  Out of scope: unread-message emails, "typing…" indicator, attachments, editing/deleting messages, a backplane
  (single instance).

## Module shape
`Modules/Chat/{Chat.Domain, Chat.Application, Chat.Infrastructure, Chat.Api}`. No `Chat.Contracts` (nothing outside
uses it, like Inventory) and no outbox (the module publishes nothing; it only consumes `StudioDetailsChanged`). Named
"Chat" because `Messaging` is already taken by MassTransit in `Shared.Infrastructure.Messaging`.

### Domain (`Chat.Domain`)
- `Conversation : ITenantEntity, IAuditableEntity`: `Id`, `StudioId`, `CustomerUserId`, `StudioName`,
  `StudioNameAsOf` (when that copy was taken, for ordering `StudioDetailsChanged`), `CustomerName` (copies taken when
  the conversation is opened), `LastMessageAt?`, `LastMessagePreview?`, `CustomerReadUpTo?`, `StudioReadUpTo?` (UTC).
  - `static Open(studioId, studioName, customerUserId, customerName, DateTime now)`: sets `StudioId` explicitly (the
    customer side has no tenant, or a different one; `TenantDbContext` keeps an explicit studio).
  - `Result<Message> Post(ChatSide side, Guid senderUserId, string body, Guid clientMessageId, DateTime now)`:
    trims; empty or too long (`MaxBodyLength = 2000`) is an error; updates `LastMessageAt`/preview and the sender's
    read state (your own messages count as read).
  - `bool MarkRead(ChatSide side, DateTime upTo)`: only moves forward; returns whether anything changed. Callers pass
    the `SentAt` of a stored message, never a client's clock (see `MarkRead` below).
  - No rename method: the studio name is updated in bulk by the consumer (Infrastructure), with the ordering rule in SQL.
- `Message : ITenantEntity`: `Id` (`Guid.CreateVersion7()`), `ConversationId`, `StudioId`, `Side` (`Customer`/`Studio`),
  `SenderUserId`, `Body`, `SentAt`, `ClientMessageId`.
  - `SentAt` is truncated to whole microseconds when created (Postgres `timestamptz` keeps microseconds, .NET keeps
    100 ns ticks), so the value in memory, in the push and in the database is the same and read receipts compare
    exactly.
- `enum ChatSide { Customer, Studio }`, `ChatErrors` (`CONVERSATION_NOT_FOUND`, `MESSAGE_NOT_FOUND`,
  `CHAT_STUDIO_NOT_AVAILABLE`, `CHAT_PROFILE_REQUIRED`, `CHAT_NO_CONTACT`, `CHAT_OWN_STUDIO`,
  `MESSAGE_EMPTY`/`MESSAGE_TOO_LONG`), modelled on `RequestErrors` in
  `Modules/Scheduling/Scheduling.Domain/Requests/OnlineBookingRequest.cs`.

### Infrastructure (`Chat.Infrastructure`)
- `ChatDbContext : TenantDbContext, IChatDbContext`, schema `chat`, its own migrations history table (pattern:
  `Modules/Customers/Customers.Infrastructure/Persistence/CustomersDbContext.cs`, `CustomersInfrastructureExtensions.cs`).
- Sets without the tenant filter for the customer side, as in Scheduling: `ConversationsOfAnyStudio`,
  `MessagesOfAnyStudio` (`IgnoreQueryFilters([TenantFilter])`). Every customer query matches the caller itself:
  conversations by `CustomerUserId == tenant.UserId`; messages only through a conversation found that way
  (`Message` has no `CustomerUserId`, so filter by `ConversationId` of an already checked conversation, or join).
- Indexes: unique `(studio_id, customer_user_id)`; `(studio_id, last_message_at desc)`;
  `(customer_user_id, last_message_at desc)`; `(conversation_id, sent_at, id)`; unique
  `(conversation_id, sender_user_id, client_message_id)` (idempotent retries).
- `TrySaveChangesAsync` returns false on a violation of either unique key (named `ConversationPairKey` and
  `MessageClientIdKey`) and clears the change tracker, so a race between two "open conversation" calls or a double
  send ends with reading the existing row instead of a 500.
- `Conversation` has an `xmin` row version: a message and a read receipt (or two messages) changing the same
  conversation at once throw `DbUpdateConcurrencyException` instead of one undoing the other. Send and MarkRead
  reload and retry once or twice on it.
- `ConversationStudioNameConsumer : IConsumer<StudioDetailsChanged>` (pattern: Marketplace's
  `ListingStudioDetailsConsumer`): bypasses the tenant filter by name, scopes by the event's `StudioId`, and updates
  `StudioName` with `ExecuteUpdateAsync` where `StudioNameAsOf < ChangedAt`, so redelivered or out-of-order events
  are harmless. Exposed as `ChatModule.ConsumersAssembly`.
- Migration: `dotnet ef migrations add InitialChat -p Modules/Chat/Chat.Infrastructure -s Api -c ChatDbContext -o Persistence/Migrations`.

### Application (`Chat.Application`, vertical slices, mediator)
The side is a parameter of the message (the controller sets it), so one handler serves both sides. A shared helper
`ConversationAccess.FindAsync(db, tenant, side, id)`: `Studio` looks up through the tenant filter, `Customer` through
`ConversationsOfAnyStudio` + `CustomerUserId == tenant.UserId`. Not found (or someone else's) is `CONVERSATION_NOT_FOUND`
(404) either way.
- `Conversations/OpenAsCustomer`: `OpenConversationCommand(Guid StudioId)` → get-or-create. The studio must be
  published (`IPublicStudios.FindPublishedAsync`, Marketplace.Contracts; none = `CHAT_STUDIO_NOT_AVAILABLE`), its
  name comes from there; the customer's name from `ICustomerDirectory.FindProfileAsync` (Customers.Contracts; none =
  `CHAT_PROFILE_REQUIRED`), as in `SubmitBookingRequestHandler`. A caller who is an active member of that studio
  (`IStudioDirectory.IsActiveMemberAsync`) gets `CHAT_OWN_STUDIO`. These checks guard **creating** a conversation:
  when one already exists, it is returned as it is, even if the studio has been unpublished since (see Decisions).
- `Conversations/OpenAsStudio`: `OpenStudioConversationCommand(Guid BookingRequestId)` → asks Scheduling for the
  request's customer through a new contract (below); none = `CHAT_NO_CONTACT`. Studio name from
  `IStudioDirectory.FindDetailsAsync` (works whether or not the studio is published).
- `Conversations/GetConversations`: `GetConversationsQuery(ChatSide, PageRequest)` → `PagedResponse<ConversationDto>`
  (the other side's name, preview, `LastMessageAt`, `UnreadCount`, the other side's read state), ordered
  `LastMessageAt desc, Id desc` (the indexes are built in that order). Only conversations with at least one message
  are listed: one opened but never written to would be noise in the studio's inbox (and Postgres sorts NULLs first
  in a descending order). `UnreadCount` = the other side's messages with
  `SentAt >` my side's `ReadUpTo` (all of them when null).
- `Conversations/GetUnreadCount`: `GetUnreadCountQuery(ChatSide)` → total unread messages across the side's
  conversations, for the navigation badge (the paged list can't give a total).
- `Messages/GetMessages`: `GetMessagesQuery(ChatSide, Guid ConversationId, CursorRequest Paging)` →
  `CursorResponse<MessageDto>`, newest first. This is the cursor paging `docs/api-conventions.md` already describes
  (as for reviews): `TimeCursor(SentAt, Id)`, `INVALID_CURSOR` on a bad one, `ToCursorResponseAsync`. Offset pages
  would repeat messages as new ones arrive at the top.
  - Older history: pass `nextCursor` back as `cursor`. Catching up after a reconnect: read from the top (no cursor)
    and keep going until a message the app already has; there is no separate "after" mode.
  - The (SentAt, Id) comparison runs in Postgres, which also does the ordering, so comparing UUIDs there is
    consistent. `SentAt` is stored to the microsecond, so a cursor round-trips exactly.
- `Messages/SendMessage`: `SendMessageCommand(ChatSide, Guid ConversationId, SendMessageRequest(Body, ClientMessageId))`
  → save, then `IChatNotifier.MessageSentAsync(...)`. A retry with the same `ClientMessageId` returns the existing message.
- `Messages/MarkRead`: `MarkReadCommand(ChatSide, Guid ConversationId, MarkReadRequest(UpToMessageId))` → loads that message from
  the conversation (else `MESSAGE_NOT_FOUND`) and calls `MarkRead(side, message.SentAt)`. The client says *which*
  message it has seen, never *when*, so a wrong or hostile clock can't mark future messages as read. If anything
  changed, push `ConversationRead` to the other side.
- `IChatNotifier` (interface in Application, no ASP.NET, as `Application_DoesNotDependOnInfrastructureOrWeb` requires):
  `MessageSentAsync(studioId, customerUserId, MessageDto)`, `ConversationReadAsync(studioId, customerUserId,
  ConversationReadDto(conversationId, side, upTo))`. Called **after** `SaveChanges`; a failed push is logged and
  swallowed (the database is the source of truth; the client catches up by reading again). Until step 7 a
  `NullChatNotifier` in `Chat.Api` stands in.
- FluentValidation validators for `SendMessageRequest` (not empty, ≤ 2000 after trimming, `ClientMessageId` not
  empty), `MarkReadRequest` and both open requests. `CursorRequest`/`PageRequest` limits are model validation.

### Api (`Chat.Api`)
- `MyConversationsController`: `api/me/conversations`, `[Authorize]`, `[AllowWithoutStudio]` (pattern:
  `MyBookingRequestsController`): `POST { studioId }` (open), `GET` (list), `GET unread-count`, `GET {id}/messages`,
  `POST {id}/messages` (`[EnableRateLimiting(RateLimits.Chat)]`), `POST {id}/read`.
- `ConversationsController`: `api/conversations`, `[Authorize]`, tenant-scoped (pattern: `BookingRequestsController`):
  `POST { bookingRequestId }`, `GET`, `GET unread-count`, `GET {id}/messages`,
  `POST {id}/messages` (`[EnableRateLimiting(RateLimits.Chat)]`), `POST {id}/read`.
- `ChatHub : Hub` at `/hubs/chat`, `[Authorize]`. Clients send nothing through the hub (sending stays in REST and the
  mediator, so validation and tests work as everywhere else). One method: `WatchStudio(Guid studioId)` checks an
  active membership (`IStudioDirectory.IsActiveMemberAsync`) and adds the connection to the `studio:{id}` group; a
  staff member of several studios may watch each of them.
  - Groups belong to a connection: after an automatic reconnect the client calls `WatchStudio` again.
  - Customers get pushes through `Clients.User(customerUserId)` (all their tabs), studios through
    `Clients.Group("studio:{id}")`.
  - Events to the frontend: `MessageReceived(MessageDto + conversationId)`, `ConversationRead(conversationId, side, upTo)`.
- `SignalRChatNotifier : IChatNotifier` (using `IHubContext<ChatHub>`).
- `ChatUserIdProvider : IUserIdProvider` reading `ClaimTypes.NameIdentifier` or `sub`, like `TenantMiddleware.GetUserId`
  (`TestAuthHandler` sets a raw `sub`).
- `ChatModule.AddChatModule()` (validators, infrastructure, `AddSignalR()`, notifier, user-id provider,
  `AddApplicationPart`), `ChatModule.ConsumersAssembly`, and `MapChatHub(this IEndpointRouteBuilder)` with
  `.WithMetadata(new AllowWithoutStudioAttribute())`, so `TenantMiddleware` doesn't answer `STUDIO_REQUIRED` during
  negotiation (a browser WebSocket can't send `X-Studio-Id`).

### Changes outside the module
- **Done: `TenantDbContext`** now stamps the current studio only on new rows whose `StudioId` is empty. Before, it
  overwrote it, so a staff member of studio B (one membership, so B is resolved as their tenant even on
  `[AllowWithoutStudio]` endpoints) who sent a booking request to studio A as a customer saved it under B. Covered by
  `BookingRequestEndpointTests.StaffOfAnotherStudio_CanBookAsACustomer_AndTheRequestGoesToTheChosenStudio`. Chat's
  customer-side writes rely on this.
- **Identity**: `IStudioDirectory.IsActiveMemberAsync(Guid studioId, Guid userId)` in
  `Modules/Identity/Identity.Contracts/IStudioDirectory.cs`, implemented in
  `Modules/Identity/Identity.Application/Directory/StudioDirectory.cs` (`db.Members`, `IsActive`).
- **Identity / JWT**: in `IdentityModule.AddSupabaseAuthentication` add `JwtBearerEvents.OnMessageReceived` that takes
  `access_token` from the query string only for `/hubs/*` paths (a browser can't attach the header to a WebSocket).
  - The token then appears in URLs. ASP.NET's hosting log ("Request starting … `?access_token=…`", category
    `Microsoft.AspNetCore.Hosting.Diagnostics`, Information) prints the query string. Today it is silenced only
    because `Microsoft.AspNetCore` is at `Warning` in `Api/appsettings.json`. Pin
    `"Microsoft.AspNetCore.Hosting.Diagnostics": "Warning"` there explicitly (with the reason in CLAUDE.md), so
    turning ASP.NET logs up for debugging doesn't write tokens to the logs. Don't add `UseHttpLogging` with the query
    string for `/hubs`.
- **Host rate limiting**: `RateLimits.Public` is per client IP (`Api/Extensions/EdgeExtensions.cs`), which is wrong for
  signed-in sending (customers behind one NAT would share a budget). Add `RateLimits.Chat`: a fixed window per user
  (`sub`/`NameIdentifier`, falling back to the IP), e.g. 30 messages a minute, set by `RateLimiting:ChatPermitLimit`.
  The user is only known after authentication, so in `Api/Program.cs` move `UseAuthentication()` before
  `UseRateLimiter()`. The order becomes `UseRouting → UseCors → UseAuthentication → UseRateLimiter → UseOutputCache →
  UseTenantContext → UseAuthorization`: cached answers still count against the limit, and a cache hit still skips the
  profile lookup (that's `TenantMiddleware`, not authentication). Check the public endpoints' tests still pass.
- **Scheduling**: a new contract in `Scheduling.Contracts`, e.g.
  `IRequestContacts.FindRequestCustomerAsync(Guid studioId, Guid requestId) → RequestCustomerSummary(UserId, Name)?`,
  implemented in `Scheduling.Infrastructure` over `BookingRequestsOfAnyStudio` (pattern: `BookingReportSource` and its
  registration in `SchedulingInfrastructureExtensions`).
- **Host** (`Api/Program.cs`): `AddChatModule`, `ChatModule.ConsumersAssembly` in `AddMessaging`, `app.MapChatHub()`
  after `MapControllers`. CORS stays as it is; the frontend connects with `withCredentials: false` (tokens, not
  cookies). `@microsoft/signalr` defaults to `withCredentials: true`, which our CORS policy (no `AllowCredentials`)
  would reject, so this goes on the frontend's list too.
- `Api/Extensions/ServiceCollectionExtensions.cs`: `typeof(IChatDbContext)` in `options.Assemblies`.
- `detaileed_api.sln` (`src` folder), `Api/Dockerfile` (4 new `.csproj`), project references in `Api.csproj`.
- `ArchitectureTests/ModuleBoundaryTests.cs`: a `new("Chat", …)` entry in `Modules`.
- Docs: `CLAUDE.md` (Chat module, hub, query-string token and the pinned log level, the `RateLimits.Chat` policy and
  middleware order), `docs/marketplace-roadmap.md` (step 6), `docs/api-conventions.md` (keyset paging for messages),
  `docs/database.md` if it lists the contexts; regenerate `docs/openapi.json` (build).

## Order (small commits)
0. Done: `TenantDbContext` keeps an explicit `StudioId` (+ regression test).
1. Project skeletons + sln/Dockerfile/host/architecture test.
2. Domain + unit tests (`Post`, `MarkRead`, limits, `SentAt` truncation).
3. DbContext, configurations, `InitialChat` migration.
4. Contracts: `IStudioDirectory.IsActiveMemberAsync`, Scheduling `IRequestContacts`.
5. Open/List/UnreadCount/Messages/Send/Read slices + controllers (notifier a no-op for now). Add
   `typeof(IChatDbContext)` to the mediator's `options.Assemblies` here, with the first handler: the source generator
   rejects an assembly that doesn't reference `Mediator.Abstractions` yet (`MSG0007`).
6. Host: `RateLimits.Chat` + middleware order; pinned hosting log level.
7. SignalR: hub, notifier, user-id provider, query-string token, `MapChatHub`.
8. `StudioDetailsChanged` consumer.
9. Integration tests + docs.

## Verification
- `dotnet build detaileed_api.sln`, `dotnet test ArchitectureTests`, `dotnet test UnitTests`.
- `dotnet test IntegrationTests --filter "FullyQualifiedName~Chat"` (Docker):
  - handlers on real Postgres: get-or-create is idempotent, a repeated `ClientMessageId` doesn't duplicate, keyset
    cursor paging (including two messages with the same `SentAt`), unread counts and read state only move forward,
    `MarkRead` with a message of another conversation is `MESSAGE_NOT_FOUND`;
  - isolation: studio B doesn't see studio A's conversations, customer X doesn't see customer Y's (404);
  - a staff member of studio B with one membership opens a conversation with studio A as a customer: it is stored
    under A and shows up in A's inbox, not B's; the same person opening one with B gets `CHAT_OWN_STUDIO`;
  - a studio with no request from the customer → `CHAT_NO_CONTACT`; an unpublished studio → `CHAT_STUDIO_NOT_AVAILABLE`
    on open, but an existing conversation with it still accepts messages;
  - endpoints: 400 on an empty or too long body; 429 after `ChatPermitLimit` sends by one user while another user
    from the same IP can still send; a multi-studio staff member without the header gets `STUDIO_REQUIRED` on
    `api/conversations`, but the hub still negotiates;
  - `StudioDetailsChanged` renames the studio in its conversations; an older event after a newer one changes nothing;
  - SignalR end to end: `Microsoft.AspNetCore.SignalR.Client` against `ApiFactory.Server`
    (`HttpMessageHandlerFactory = server.CreateHandler()`, LongPolling transport, `X-Test-User-Id` header); the
    customer sends over REST and a staff member who called `WatchStudio` receives `MessageReceived` (poll with a
    deadline); `WatchStudio` for someone else's studio is refused.
- The existing suites still pass after the middleware reorder (public map, output cache, rate limit tests).
- By hand: `dotnet run --project Api`, two windows (dashboard 3000 and apps/web 3001) or an `.http` file plus a small
  `@microsoft/signalr` client; a message shows up on the other side without a refresh, and "read" arrives live.
- Don't apply the migration to Supabase locally (forbidden in CLAUDE.md); deploy as in `docs/database.md`.

## Known limitations (on purpose)
- Removing a membership doesn't disconnect an open connection; it takes effect on reconnect (REST checks access on
  every request anyway).
- The customer's name is a copy from when the conversation was opened: Customers publishes no event when a profile
  changes. (The studio's name is kept current from `StudioDetailsChanged`.)
- A customer opening a conversation takes the studio's name from Marketplace's listing (itself a copy) and dates it
  "now". If the studio was renamed seconds earlier and the listing hasn't caught up, that rename's event is older and
  is skipped, so the old name stays until the next change.
- Read state is per side, so the studio can't see which staff member read a message.
- A single API instance; scaling out needs a backplane (Redis).
- Hub URLs carry the access token; they are short-lived Supabase JWTs, and the hosting log level is pinned so they
  aren't logged.
- The frontend (two apps in `WebstormProjects/detaileed`) is separate work: conversation list, chat window, unread
  badge, a `HubConnection` with `accessTokenFactory`, `withCredentials: false` and `withAutomaticReconnect`, calling
  `WatchStudio` again and reading from the top until a known message after a reconnect.
