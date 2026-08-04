# Gmail-Pipeline

Reusable .NET Gmail access and email parsing pipeline module.

> [!WARNING]
> This module is intended for personal/installed-app Gmail automation. The default OAuth flow is interactive desktop authorization and the default token store uses Windows DPAPI. Use a custom `IGmailCredentialProvider` or `IGmailTokenStoreFactory` before using it in a service account, server, container, or non-Windows host.

## Projects

- `Gmail-Pipeline.Core`: provider-neutral module API, interaction contracts, parser resolver, and pipeline executor.
- `Gmail-Pipeline.Google`: internal Google Gmail provider adapter for Core API ports, installed-app OAuth, MIME mapping, attachment streams, label mutation, and DI registration.

The module does not include workers, schedulers, databases, finance rules, fixed labels, or fixed Gmail queries. Applications decide when to search, how to store data, and what business parsers to run.

## Architecture

Both projects are organized by Clean Architecture layer:

- `Domain/Contract`: external interaction contracts such as email models, search requests, labels, parsing results, exceptions, and Google options.
- `Application`: provider-neutral use cases and orchestration.
- `Presentation/Api`: external call interfaces and registration entrypoints.
- `Infrastructure`: provider implementation details.

External applications should depend on:

- `GmailPipeline.Core.Api`
- `GmailPipeline.Core.Contract.*`
- `GmailPipeline.Google.Api`
- `GmailPipeline.Google.Contract`

`GmailPipeline.Google.Infrastructure.*` is intentionally treated as provider internals. `Gmail-Pipeline.Google` depends on `Gmail-Pipeline.Core`; the reverse dependency is not allowed.

## Read-Only Usage

```csharp
using GmailPipeline.Core.Api;
using GmailPipeline.Core.Contract.Search;
using GmailPipeline.Google.Api;

services.AddGmailPipelineGoogleReadOnly(options =>
{
    options.ClientSecretPath = @"%LOCALAPPDATA%\GmailPipeline\auth\client_secret.json";
    options.TokenDirectory = @"%LOCALAPPDATA%\GmailPipeline\auth\tokens";
});
```

```csharp
var reader = serviceProvider.GetRequiredService<IEmailReader>();
var attachments = serviceProvider.GetRequiredService<IEmailAttachmentClient>();

var page = await reader.SearchAsync(new EmailSearchRequest
{
    Query = "has:attachment",
    PageSize = 100
}, cancellationToken);
```

`AddGmailPipelineGoogleReadOnly` forces the `gmail.readonly` scope and only registers `IEmailReader` plus `IEmailAttachmentClient`.

## MIME Model

`EmailMessage.TextBody` and `EmailMessage.HtmlBody` are convenience projections for the first top-level plain and HTML body representations. The full ordered top-level body list is available in `EmailMessage.BodySections`.

Composite MIME entities, including `multipart/*` attachments and encapsulated `message/rfc822` messages, are represented as `EmailAttachment` values with `Kind`, `BodySections`, and `Children`. Their inner body parts are not merged into the parent message body, and their child attachments are not promoted into the parent attachment list.

`IEmailAttachmentClient.OpenAttachmentAsync` opens embedded, external, and provider-addressable leaf attachments. Structural composite attachments that Gmail does not expose as a single bounded byte part throw `EmailCompositeAttachmentException`; callers should inspect their `BodySections` and `Children` instead.

## Label Modify Usage

```csharp
using GmailPipeline.Core.Api;
using GmailPipeline.Google.Api;

services.AddGmailPipelineGoogleModify(options =>
{
    options.ClientSecretPath = @"%LOCALAPPDATA%\GmailPipeline\auth\client_secret.json";
    options.TokenDirectory = @"%LOCALAPPDATA%\GmailPipeline\auth\tokens";
});
```

```csharp
var labels = serviceProvider.GetRequiredService<IEmailLabelClient>();
var label = await labels.GetOrCreateAsync("AutoBookkeeping", cancellationToken);
await labels.ModifyMessageLabelsAsync(
    messageId,
    new EmailLabelModification([label.Id], []),
    cancellationToken);
```

`AddGmailPipelineGoogleModify` forces the `gmail.modify` scope and registers `IEmailLabelClient`. Label modification accepts Gmail label IDs; use `FindByNameAsync` or `GetOrCreateAsync` when the application starts from a label name.

Read-only and modify tokens are stored under different client/user/scope namespace directories, so changing scope requires a separate authorization instead of silently reusing an incompatible token. The v2 token-store format does not migrate older flat-directory token files; after updating from an older module commit, authorize once again and leave old token cleanup as an explicit host-application decision.

## Content Limits

Gmail messages are preflighted with `format=METADATA` before `format=FULL`. The supported parser is the bounded Gmail `MessagePart` reader used by `IEmailReader`; the legacy unbounded RAW `MimeMessage` parser is not registered by DI. The reader parses text bodies and attachment metadata, but large attachment bytes are lazy and are not stored in `EmailMessage.Attachments` during `IEmailReader.GetAsync`.

Default resource limits:

- `MaxTextBodyBytes`: 4 MiB
- `MaxEmbeddedAttachmentBytes`: 256 KiB
- `MaxTotalEmbeddedAttachmentBytes`: 8 MiB
- `MaxOpenedAttachmentBytes`: 32 MiB
- `MaxAttachmentCount`: 256
- `MaxMimePartCount`: 2048
- `MaxMimeDepth`: 64
- `MaxMessageSizeEstimateBytes`: 64 MiB

Override limits before registering Gmail services:

```csharp
using GmailPipeline.Google.Contract;

services.AddSingleton(new GmailContentLimitsOptions
{
    MaxTextBodyBytes = 2 * 1024 * 1024,
    MaxEmbeddedAttachmentBytes = 128 * 1024,
    MaxTotalEmbeddedAttachmentBytes = 4 * 1024 * 1024,
    MaxOpenedAttachmentBytes = 16 * 1024 * 1024
});
```

When content exceeds the configured limit, the module throws `EmailResourceLimitException` instead of attempting unbounded allocation.

## Credential And Token Stores

The default `InstalledAppCredentialProvider` loads an installed-app OAuth client JSON file and starts Google interactive authorization when no token exists. This is only suitable for local desktop/personal use.

For non-Windows hosts, register a custom `IGmailTokenStoreFactory` before calling the DI extension. For non-interactive or hosted flows, register a custom `IGmailCredentialProvider` or use:

```csharp
using GmailPipeline.Google.Api;
using GmailPipeline.Google.Contract;

services.AddGmailPipelineGoogleReadOnlyWithCredentialProvider<CustomProvider>(options =>
{
    options.ClientSecretPath = "/secure/client_secret.json";
    options.TokenDirectory = "/secure/tokens";
});
```

## Tests

```powershell
dotnet test Gmail-Pipeline.slnx --filter "Category!=Integration"
```

The integration smoke test is marked with `Category=Integration`. It only contacts Gmail when `GMAIL_PIPELINE_RUN_INTEGRATION=true` is set, and needs `GMAIL_PIPELINE_CLIENT_SECRET_PATH`; optional variables are `GMAIL_PIPELINE_TOKEN_DIRECTORY`, `GMAIL_PIPELINE_QUERY`, and `GMAIL_PIPELINE_LABEL`.
