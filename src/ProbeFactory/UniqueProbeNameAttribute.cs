using System.ComponentModel.DataAnnotations;

namespace ProbeFactory;

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
