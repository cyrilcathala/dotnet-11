namespace MissionControl.Web.Services;

/// <summary>Lit les sources du dépôt pour les afficher telles quelles (aucune copie figée dans la console).</summary>
public sealed class SourceFiles(IConfiguration configuration, IHostEnvironment env)
{
    public string RepoRoot { get; } = Path.GetFullPath(
        configuration["Console:RepoRoot"] ?? Path.Combine(env.ContentRootPath, "..", ".."));

    public string FullPath(string relative) =>
        Path.Combine(RepoRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    public string? Read(string relative)
    {
        var path = FullPath(relative);
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n").TrimStart('﻿') : null;
    }
}
