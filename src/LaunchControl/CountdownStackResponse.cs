namespace LaunchControl;

/// <param name="RuntimeAsync">Variante capturée : true = compilée avec runtime async, false = sans.</param>
public sealed record CountdownStackResponse(int Frames, string Stack, bool RuntimeAsync);
