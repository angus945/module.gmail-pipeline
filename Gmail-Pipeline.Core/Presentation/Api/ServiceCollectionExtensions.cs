using GmailPipeline.Core.Api;
using GmailPipeline.Core.Application.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace GmailPipeline.Core.Api;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmailPipeline<TResult>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        services.Add(new ServiceDescriptor(
            typeof(IEmailParserResolver<TResult>),
            typeof(EmailParserResolver<TResult>),
            lifetime));
        services.Add(new ServiceDescriptor(
            typeof(IEmailPipeline<TResult>),
            typeof(EmailPipeline<TResult>),
            lifetime));
        return services;
    }

    public static IServiceCollection AddEmailParser<TResult, TParser>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TParser : class, IEmailParser<TResult>
    {
        services.Add(new ServiceDescriptor(
            typeof(IEmailParser<TResult>),
            typeof(TParser),
            lifetime));
        return services;
    }
}
