using System.Diagnostics;

namespace NetworkProfiles.Core.Execution;

public sealed record CommandResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Runs netsh.exe with an explicit argument list (no shell, no string concatenation).
/// </summary>
public sealed class NetshRunner
{
    private static readonly string NetshPath = Path.Combine(Environment.SystemDirectory, "netsh.exe");

    public async Task<CommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(NetshPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start netsh.exe.");

        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        var output = ((await stdout) + (await stderr)).Trim();
        return new CommandResult(process.ExitCode, output);
    }
}
