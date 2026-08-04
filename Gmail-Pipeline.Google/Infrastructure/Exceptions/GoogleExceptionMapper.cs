using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using GmailPipeline.Core.Contract.Exceptions;
using GmailPipeline.Google.Infrastructure.Clients;
using Google;
using Google.Apis.Auth.OAuth2.Responses;

namespace GmailPipeline.Google.Infrastructure.Exceptions;

public static class GoogleExceptionMapper
{
    public static bool CanMap(Exception exception) =>
        exception is GoogleApiException
            or HttpRequestException
            or TokenResponseException
            or EmailClientException
            or IOException
            or UnauthorizedAccessException
            or CryptographicException
            or JsonException
            or FormatException;

    public static Exception Map(Exception exception, string operation) =>
        exception switch
        {
            EmailClientException => exception,
            TokenResponseException => new EmailAuthenticationException($"Gmail authentication failed during {operation}.", exception),
            GoogleApiException { HttpStatusCode: HttpStatusCode.Unauthorized } => new EmailAuthenticationException($"Gmail authentication failed during {operation}.", exception),
            GoogleApiException googleException when GoogleErrorClassifier.Classify(googleException).IsRateLimit => CreateRateLimitException(googleException, operation),
            GoogleApiException { HttpStatusCode: HttpStatusCode.Forbidden } => new EmailAuthorizationException($"Gmail authorization failed during {operation}.", exception),
            GoogleApiException { HttpStatusCode: HttpStatusCode.NotFound } => new EmailNotFoundException($"Gmail resource was not found during {operation}.", exception),
            GoogleApiException => new EmailClientException($"Gmail API failed during {operation}.", exception),
            HttpRequestException => new EmailClientException($"Gmail network request failed during {operation}.", exception),
            FileNotFoundException => new EmailCredentialStoreException($"Gmail credential file was not found during {operation}.", exception),
            DirectoryNotFoundException => new EmailCredentialStoreException($"Gmail credential path was not found during {operation}.", exception),
            UnauthorizedAccessException => new EmailCredentialStoreException($"Gmail credential store access failed during {operation}.", exception),
            IOException => new EmailCredentialStoreException($"Gmail credential store IO failed during {operation}.", exception),
            CryptographicException => new EmailCredentialStoreException($"Gmail credential protection failed during {operation}.", exception),
            JsonException => new EmailCredentialStoreException($"Gmail credential JSON was invalid during {operation}.", exception),
            FormatException => new EmailContentFormatException($"Gmail content format was invalid during {operation}.", exception),
            _ => exception
        };

    private static EmailRateLimitException CreateRateLimitException(GoogleApiException exception, string operation)
    {
        var classification = GoogleErrorClassifier.Classify(exception);
        return new EmailRateLimitException(
            $"Gmail rate limit was hit during {operation}.",
            operation,
            classification.Reason,
            classification.RetryAfter,
            exception);
    }
}
