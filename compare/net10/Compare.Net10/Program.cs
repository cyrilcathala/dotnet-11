using LaunchControl;

// Version .NET 10 des démos : seule la commande « countdown » est câblée.
if (args is ["countdown"])
{
    var stack = await new CountdownSequence().CaptureStackAsync(CancellationToken.None);
    var frames = stack.Split('\n').Count(line => line.StartsWith("   at ", StringComparison.Ordinal));
    Console.WriteLine($"Frames : {frames}");
    Console.WriteLine(stack);
    return 0;
}

Console.Error.WriteLine("Usage : dotnet run -- countdown");
return 1;
