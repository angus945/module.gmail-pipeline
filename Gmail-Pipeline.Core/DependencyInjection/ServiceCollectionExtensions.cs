using GmailPipeline.Core.Abstractions;
using GmailPipeline.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace GmailPipeline.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEmailPipeline<TResult>(this IServiceCollection services)
    {
        services.AddSingleton<IEmailParserResolver<TResult>, EmailParserResolver<TResult>>();
        services.AddSingleton<IEmailPipeline<TResult>, EmailPipeline<TResult>>();
        return services;
    }

    public static IServiceCollection AddEmailParser<TResult, TParser>(this IServiceCollection services)
        where TParser : class, IEmailParser<TResult>
    {
        services.AddSingleton<IEmailParser<TResult>, TParser>();
        return services;
    }
}
