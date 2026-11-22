using System.ComponentModel.DataAnnotations;

namespace ProbeFactory;

/// <summary>Async-only validation: the name must not be reserved or already registered.</summary>
public sealed class UniqueProbeNameAttribute : AsyncValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        => throw new InvalidOperationException($"{nameof(UniqueProbeNameAttribute)} is async-only.");

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
