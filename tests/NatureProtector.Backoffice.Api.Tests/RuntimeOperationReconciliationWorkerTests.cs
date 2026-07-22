using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NatureProtector.Backoffice.Api.ControlPlane.Services;
using NatureProtector.Backoffice.Api.Tests.TestInfrastructure;
using NatureProtector.Infrastructure.Postgres.Control;

namespace NatureProtector.Backoffice.Api.Tests;

public sealed class RuntimeOperationReconciliationWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_ReconcilesOldestNonTerminalOperations_AndEnsuresEvidence()
    {
        await using var scope = new SqliteControlDbContextScope();
        var oldest = Guid.NewGuid();
        var terminal = Guid.NewGuid();
        await SeedOperationsAsync(scope, oldest, terminal);
        var controlPlane = RecordingControlPlaneDispatchProxy.Create(expectedEvidenceCalls: 1);
        using var services = new ServiceCollection()
            .AddSingleton<IControlPlaneService>(controlPlane.Service)
            .BuildServiceProvider();
        var logger = new RecordingLogger<RuntimeOperationReconciliationWorker>();
        using var worker = new RuntimeOperationReconciliationWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            scope.Factory,
            Options.Create(new RuntimeOperationReconciliationOptions { Enabled = true, IntervalSeconds = 60, BatchSize = 10 }),
            logger);

        await worker.StartAsync(CancellationToken.None);
        await WaitForEvidenceCallsAsync(controlPlane, logger);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal([oldest], controlPlane.ReconciledOperationIds);
        Assert.Equal([oldest], controlPlane.EvidenceOperationIds);
        Assert.DoesNotContain(terminal, controlPlane.ReconciledOperationIds);
    }

    [Fact]
    public async Task ExecuteAsync_DisabledWorker_DoesNotResolveControlPlaneOrReconcile()
    {
        await using var scope = new SqliteControlDbContextScope();
        await SeedOperationsAsync(scope, Guid.NewGuid());
        var controlPlane = RecordingControlPlaneDispatchProxy.Create(expectedEvidenceCalls: 1);
        using var services = new ServiceCollection()
            .AddSingleton<IControlPlaneService>(controlPlane.Service)
            .BuildServiceProvider();
        using var worker = new RuntimeOperationReconciliationWorker(
            services.GetRequiredService<IServiceScopeFactory>(),
            scope.Factory,
            Options.Create(new RuntimeOperationReconciliationOptions { Enabled = false }),
            NullLogger<RuntimeOperationReconciliationWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await worker.StopAsync(CancellationToken.None);

        Assert.Empty(controlPlane.ReconciledOperationIds);
        Assert.Empty(controlPlane.EvidenceOperationIds);
    }

    private static Task SeedOperationsAsync(
        SqliteControlDbContextScope scope,
        params Guid[] operationIds)
        => scope.SeedAsync(dbContext =>
        {
            var now = new DateTimeOffset(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);
            for (var index = 0; index < operationIds.Length; index++)
            {
                dbContext.RuntimeOperations.Add(new RuntimeOperationRecord
                {
                    OperationId = operationIds[index],
                    RequestId = Guid.NewGuid(),
                    IdempotencyKey = operationIds[index].ToString("N"),
                    CorrelationId = operationIds[index].ToString("N"),
                    State = index == operationIds.Length - 1 ? "Completed" : "Running",
                    TerminalOutcome = index == operationIds.Length - 1 ? "Succeeded" : null,
                    IsOperational = true,
                    AcceptedAt = now.AddMinutes(-10),
                    UpdatedAt = now.AddMinutes(index),
                    DeadlineAt = now.AddMinutes(10)
                });
            }

            return Task.CompletedTask;
        });

    private static async Task WaitForEvidenceCallsAsync(
        RecordingControlPlaneDispatchProxy controlPlane,
        RecordingLogger<RuntimeOperationReconciliationWorker> logger)
    {
        try
        {
            await controlPlane.WaitForEvidenceCallsAsync();
        }
        catch (TimeoutException exception)
        {
            throw new TimeoutException(
                $"Runtime operation reconciliation worker did not call evidence. Logs: {string.Join(" | ", logger.Messages)}",
                exception);
        }
    }

    [SuppressMessage("Performance", "CA1852:Seal internal types", Justification = "DispatchProxy requires a non-sealed proxy type.")]
    private class RecordingControlPlaneDispatchProxy : DispatchProxy
    {
        private TaskCompletionSource<IReadOnlyList<Guid>> _evidenceCalls =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IControlPlaneService Service { get; private set; } = null!;
        public List<Guid> ReconciledOperationIds { get; } = [];
        public List<Guid> EvidenceOperationIds { get; } = [];
        public int ExpectedEvidenceCalls { get; private set; }

        public static RecordingControlPlaneDispatchProxy Create(int expectedEvidenceCalls)
        {
            var service = DispatchProxy.Create<IControlPlaneService, RecordingControlPlaneDispatchProxy>();
            var proxy = (RecordingControlPlaneDispatchProxy)(object)service;
            proxy.Service = service;
            proxy.ExpectedEvidenceCalls = expectedEvidenceCalls;
            return proxy;
        }

        public Task<IReadOnlyList<Guid>> WaitForEvidenceCallsAsync()
            => _evidenceCalls.Task.WaitAsync(TimeSpan.FromSeconds(5));

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            var methodName = targetMethod?.Name ?? string.Empty;
            if (methodName == "get_IsAvailable")
            {
                return true;
            }

            if (methodName == "get_AvailabilityMessage")
            {
                return "Test control plane";
            }

            if (methodName == nameof(IControlPlaneService.ReconcileRuntimeOperationWithProviderAsync))
            {
                ReconciledOperationIds.Add((Guid)args![0]!);
                return CreateDefaultTask(targetMethod!.ReturnType);
            }

            if (methodName == nameof(IControlPlaneService.EnsureRuntimeEvidenceAsync))
            {
                EvidenceOperationIds.Add((Guid)args![0]!);
                if (EvidenceOperationIds.Count >= ExpectedEvidenceCalls)
                {
                    _evidenceCalls.TrySetResult(EvidenceOperationIds);
                }

                return Task.CompletedTask;
            }

            return CreateDefaultReturnValue(targetMethod!.ReturnType);
        }

        private static object? CreateDefaultReturnValue(Type returnType)
            => returnType == typeof(Task)
                ? Task.CompletedTask
                : returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>)
                    ? CreateDefaultTask(returnType)
                    : returnType.IsValueType
                        ? Activator.CreateInstance(returnType)
                        : null;

        private static object CreateDefaultTask(Type taskType)
        {
            var resultType = taskType.GetGenericArguments()[0];
            var defaultValue = resultType.IsValueType ? Activator.CreateInstance(resultType) : null;
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType)
                .Invoke(null, [defaultValue])!;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add($"{logLevel}: {formatter(state, exception)} {exception?.GetType().Name}: {exception?.Message}");
        }
    }
}
