# Gmail-Pipeline

Reusable .NET Gmail access and email parsing pipeline module.

> [!WARNING]
> This module is intended for personal/installed-app Gmail automation. The default OAuth flow is interactive desktop authorization and the default token store uses Windows DPAPI. Use a custom `IGmailCredentialProvider` or `IGmailTokenStoreFactory` before using it in a service account, server, container, or non-Windows host.

## Projects

- `Gmail-Pipeline.Core`: provider-neutral email models, search models, labels, parser contracts, parser resolver, and pipeline executor.
- `Gmail-Pipeline.Google`: Google Gmail API adapter, installed-app OAuth, MIME mapping, attachment streams, label mutation, and DI registration.

The module does not include workers, schedulers, databases, finance rules, fixed labels, or fixed Gmail queries. Applications decide when to search, how to store data, and what business parsers to run.

## Read-Only Usage

```csharp
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

## Label Modify Usage

```csharp
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

Read-only and modify tokens are stored under different client/user/scope namespaces, so changing scope requires a separate authorization instead of silently reusing an incompatible token.

## Credential And Token Stores

The default `InstalledAppCredentialProvider` loads an installed-app OAuth client JSON file and starts Google interactive authorization when no token exists. This is only suitable for local desktop/personal use.

For non-Windows hosts, register a custom `IGmailTokenStoreFactory` before calling the DI extension. For non-interactive or hosted flows, register a custom `IGmailCredentialProvider` or use:

```csharp
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
