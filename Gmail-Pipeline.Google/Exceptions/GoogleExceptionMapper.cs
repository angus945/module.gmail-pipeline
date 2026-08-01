using System.Net;
using GmailPipeline.Core.Exceptions;
using Google;
using Google.Apis.Auth.OAuth2.Responses;

namespace GmailPipeline.Google.Exceptions;

public static class GoogleExceptionMapper
{
    public static Exception Map(Exception exception, string operation) =>
        exception switch
        {
            TokenResponseException => new EmailAuthenticationException($"Gmail authentication failed during {operation}.", exception),
            GoogleApiException { HttpStatusCode: HttpStatusCode.Unauthorized } => new EmailAuthenticationException($"Gmail authentication failed during {operation}.", exception),
            GoogleApiException { HttpStatusCode: HttpStatusCode.Forbidden } => new EmailAuthorizationException($"Gmail authorization failed during {operation}.", exception),
            GoogleApiException { HttpStatusCode: HttpStatusCode.NotFound } => new EmailNotFoundException($"Gmail resource was not found during {operation}.", exception),
            GoogleApiException { HttpStatusCode: HttpStatusCode.TooManyRequests } => new EmailRateLimitException($"Gmail rate limit was hit during {operation}.", exception),
            GoogleApiException => new EmailClientException($"Gmail API failed during {operation}.", exception),
            HttpRequestException => new EmailClientException($"Gmail network request failed during {operation}.", exception),
            _ => exception
        };
}
