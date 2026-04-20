// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.AgentRuntimes.Claude.Internal;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Abstraction over <see cref="Process"/> so the Claude CLI invoker and
/// container-baseline check are testable without shelling out to a real
/// binary. Internal — the seam is consumed only by the runtime's own
/// classes, and tests reach in via <c>InternalsVisibleTo</c>.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>
    /// Runs <paramref name="fileName"/> with the given arguments and
    /// environment overlay. Returns once the process exits or
    /// <paramref name="timeout"/> elapses.
    /// </summary>
    Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>Captures the outcome of an <see cref="IProcessRunner"/> call.</summary>
internal sealed record ProcessRunResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Default <see cref="IProcessRunner"/>; wraps <see cref="Process"/>.</summary>
internal sealed class DefaultProcessRunner : IProcessRunner
{
    /// <summary>Shared instance — stateless.</summary>
    public static readonly DefaultProcessRunner Instance = new();

    /// <inheritdoc />
    public async Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = false,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }
        foreach (var kv in environment)
        {
            psi.Environment[kv.Key] = kv.Value;
        }

        using var process = new Process { StartInfo = psi };
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) stdoutBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) stderrBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException($"{fileName} did not exit within {timeout}.");
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        return new ProcessRunResult(
            ExitCode: process.ExitCode,
            StandardOutput: stdoutBuilder.ToString(),
            StandardError: stderrBuilder.ToString());
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort — the process may have exited between the
            // HasExited check and the Kill call.
        }
    }
}