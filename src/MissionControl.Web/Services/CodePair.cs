namespace MissionControl.Web.Services;

/// <summary>Un onglet de comparaison : la version de gauche (optionnelle) et celle de droite.</summary>
/// <remarks>Par défaut gauche = .NET 10 et droite = .NET 11 ; les libellés se changent pour d'autres comparaisons.</remarks>
public sealed record CodePair(string Label, string? Before, string After,
    string[]? BeforeMarks = null, string[]? AfterMarks = null,
    string BeforeLabel = ".NET 10", string AfterLabel = ".NET 11")
{
    public string[] BeforeHighlight => BeforeMarks ?? [];
    public string[] AfterHighlight => AfterMarks ?? [];
}
