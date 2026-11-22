namespace MissionControl.Web.Services;

/// <summary>État partagé de la console : une seule régie, un seul présentateur.</summary>
public sealed class MissionState
{
    private string _probe = "Pioneer-12";

    public DateTimeOffset ConsoleStartedAt { get; } = DateTimeOffset.UtcNow;

    public string Probe
    {
        get => _probe;
        set
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == _probe) return;
            _probe = value.Trim();
            LiftoffAt = null;
            Changed?.Invoke();
        }
    }

    /// <summary>Heure du décollage de la sonde courante (GO reçu de LaunchControl).</summary>
    public DateTimeOffset? LiftoffAt { get; private set; }

    public void MarkLiftoff()
    {
        LiftoffAt = DateTimeOffset.UtcNow;
        Changed?.Invoke();
    }

    public event Action? Changed;
}
