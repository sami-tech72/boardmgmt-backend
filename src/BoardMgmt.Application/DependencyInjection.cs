using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace BoardMgmt.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            // Scan the Application assembly for handlers/requests/notifications
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        // Optional: auto-register FluentValidation validators in Application
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Optional: validation pipeline behavior
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
