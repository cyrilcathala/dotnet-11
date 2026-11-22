using System.ComponentModel.DataAnnotations;
using MissionControl.Contracts;

namespace ProbeFactory;

// .NET 10 : pas de validation asynchrone ; les messages passent par ErrorMessageResourceType sur chaque attribut.
public static class ValidationMessages
{
    public static string Required => "The {0} field is required.";
    public static string StringLength => "The {0} field must be between {2} and {1} characters.";
}

public sealed record CreateProbeRequest(
    [property: Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Required))]
    [property: StringLength(32, MinimumLength = 3, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.StringLength))]
    string Name,
    ProbeKind Kind);

public static class CreateProbeEndpoint
{
    // .NET 10 : AddValidation() valide déjà les attributs synchrones des minimal APIs, avant l'endpoint.
    public static IServiceCollection AddCreateProbe(this IServiceCollection services) => services.AddValidation();

    public static IEndpointRouteBuilder MapCreateProbe(this IEndpointRouteBuilder app)
    {
        app.MapPost("/probes", async (CreateProbeRequest request, IProbeRegistry registry, CancellationToken ct) =>
        {
            // Vérification d'unicité manuelle : la validation par attributs ne sait pas être asynchrone.
            if (await registry.IsTakenAsync(request.Name, ct))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [nameof(request.Name)] = [$"The {nameof(request.Name)} field must be unique."],
                });

            var probe = registry.Add(request.Name, request.Kind);
            return Results.Created($"/probes/{probe.Name}", probe);
        });
        return app;
    }
}
