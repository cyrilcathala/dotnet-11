using System.ComponentModel.DataAnnotations;
using MissionControl.Contracts;

namespace ProbeFactory;

// .NET 11 : les messages viennent du localiseur (Resources/ValidationMessages*.resx), plus d'ErrorMessageResourceType.
public sealed record CreateProbeRequest(
    [property: Required, StringLength(32, MinimumLength = 3), UniqueProbeName] string Name,
    ProbeKind Kind);

/// <summary>Validation uniquement asynchrone : le nom ne doit être ni réservé ni déjà enregistré.</summary>
public sealed class UniqueProbeNameAttribute : AsyncValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        => throw new InvalidOperationException($"{nameof(UniqueProbeNameAttribute)} ne se valide qu'en asynchrone.");

    protected override async Task<ValidationResult?> IsValidAsync(
        object? value, ValidationContext validationContext, CancellationToken cancellationToken)
    {
        if (value is not string name)
            return ValidationResult.Success;

        var registry = validationContext.GetRequiredService<IProbeRegistry>();
        return await registry.IsTakenAsync(name, cancellationToken)
            ? new ValidationResult(FormatErrorMessage(validationContext.DisplayName))
            : ValidationResult.Success;
    }
}

public static class CreateProbeEndpoint
{
    // .NET 11 : AddValidation() valide aussi les attributs asynchrones et localise les messages, avant l'endpoint.
    public static IServiceCollection AddCreateProbe(this IServiceCollection services) => services
        .AddLocalization(o => o.ResourcesPath = "Resources")
        .AddValidation(o => o.LocalizerProvider = (_, factory) => factory.Create(typeof(ValidationMessages)));

    public static IEndpointRouteBuilder MapCreateProbe(this IEndpointRouteBuilder app)
    {
        // Plus de vérification d'unicité manuelle : [UniqueProbeName] s'en charge.
        app.MapPost("/probes", (CreateProbeRequest request, IProbeRegistry registry) =>
        {
            var probe = registry.Add(request.Name, request.Kind);
            return Results.Created($"/probes/{probe.Name}", probe);
        });
        return app;
    }
}
