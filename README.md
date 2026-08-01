# Gmail-Pipeline

Reusable .NET Gmail access and email parsing pipeline module.

## Projects

- `Gmail-Pipeline.Core`: provider-neutral email models, search models, labels, parser contracts, parser resolver, and pipeline executor.
- `Gmail-Pipeline.Google`: Google Gmail API adapter, installed-app OAuth, MIME mapping, attachment streams, label mutation, and DI registration.

The module does not include workers, schedulers, databases, finance rules, fixed labels, or fixed Gmail queries. Applications decide when to search, how to store data, and what business parsers to run.

## Usage

```csharp
services.AddGmailPipelineGoogle(options =>
{
    options.ClientSecretPath = @"%LOCALAPPDATA%\GmailPipeline\auth\client_secret.json";
    options.TokenDirectory = @"%LOCALAPPDATA%\GmailPipeline\auth\tokens";
});
```

```csharp
var client = serviceProvider.GetRequiredService<IEmailClient>();
var attachments = serviceProvider.GetRequiredService<IEmailAttachmentClient>();

var page = await client.SearchAsync(new EmailSearchRequest
{
    Query = "has:attachment",
    PageSize = 100
}, cancellationToken);
```

## Tests

```powershell
dotnet test Gmail-Pipeline.slnx
```

Integration tests that require real Google accounts should be marked with `Category=Integration` and excluded from normal CI.
