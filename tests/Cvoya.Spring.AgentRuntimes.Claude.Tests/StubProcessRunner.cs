// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.AgentRuntimes.Claude.Tests;

using System.ComponentModel;

using Cvoya.Spring.AgentRuntimes.Claude.Internal;

/// <summary>
/// Deterministic <see cref="IProcessRunner"/> double for tests. Records
/// the last invocation's environment overlay and arguments so the test
/// can assert which env var the runtime selected for the credential.
/// </summary>
internal sealed class StubProcessRunner : IProcessRunner
{
    private readonly Queue<ProcessRunResult> _results = new();
    private readonly Queue<Exception> _exceptions = new();

    public int InvocationCount { get; private set; }
    public IReadOnlyDictionary<string, string>? LastEnvironment { get; private set; }
    public IReadOnlyList<string>? LastArguments { get; private set; }
    public string? LastFileName { get; private set; }

    public void EnqueueSuccess(ProcessRunResult result) => _results.Enqueue(result);
    public void EnqueueException(Exception exception) => _exceptions.Enqueue(exception);

    /// <summary>
    /// Convenience: the runtime's <c>VerifyContainerBaselineAsync</c> path
    /// hits the runner first (with <c>--version</c>), then
    /// <c>ValidateCredentialAsync</c> follows up with the actual probe.
    /// Tests that exercise validation must enqueue both responses in
    /// order — this helper is shorthand for "the baseline probe passed."
    /// </summary>
    public void EnqueueBaselineSuccess() =>
        EnqueueSuccess(new ProcessRunResult(ExitCode: 0, StandardOutput: "1.0.0", StandardError: string.Empty));

    public void EnqueueBaselineMissing() =>
        EnqueueException(new Win32Exception("simulated: claude not found"));

    public Task<ProcessRunResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        InvocationCount++;
        LastFileName = fileName;
        LastArguments = arguments;
        LastEnvironment = environment;

        if (_exceptions.Count > 0)
        {
            throw _exceptions.Dequeue();
        }

        if (_results.Count == 0)
        {
            throw new InvalidOperationException(
                $"StubProcessRunner: no result queued for invocation {InvocationCount} of {fileName}.");
        }

        return Task.FromResult(_results.Dequeue());
    }
}