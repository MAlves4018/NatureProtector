using NatureProtector.Core.Scenarios;
using Xunit;

namespace NatureProtector.Core.Tests.Scenarios;

/// <summary>
/// Unit tests for SimulationRun.
/// These tests cover constructor validation and lifecycle transitions.
/// </summary>
public class SimulationRunTests
{
    [Fact]
    public void Ctor_AssignsProperties_WhenValidWithoutSeed()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var run = new SimulationRun(id);

        // Assert
        Assert.Equal(id, run.Id);
        Assert.Null(run.ExecutionSeed);
        Assert.Equal(SimulationRunStatus.Defined, run.Status);
        Assert.Null(run.StartedAt);
        Assert.Null(run.EndedAt);
    }

    [Fact]
    public void Ctor_AssignsProperties_WhenValidWithSeed()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var run = new SimulationRun(id, executionSeed: 42);

        // Assert
        Assert.Equal(id, run.Id);
        Assert.Equal(42, run.ExecutionSeed);
        Assert.Equal(SimulationRunStatus.Defined, run.Status);
        Assert.Null(run.StartedAt);
        Assert.Null(run.EndedAt);
    }

    [Fact]
    public void Ctor_Throws_WhenIdIsEmpty()
    {
        // Act
        var ex = Assert.Throws<ArgumentException>(() => new SimulationRun(Guid.Empty));

        // Assert
        Assert.Equal("id", ex.ParamName);
        Assert.Contains("must not be an empty GUID", ex.Message);
    }

    [Fact]
    public void MarkReady_TransitionsFromDefinedToReady()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());

        // Act
        run.MarkReady();

        // Assert
        Assert.Equal(SimulationRunStatus.Ready, run.Status);
        Assert.Null(run.StartedAt);
        Assert.Null(run.EndedAt);
    }

    [Fact]
    public void MarkReady_Throws_WhenStatusIsNotDefined()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        run.MarkReady();
        run.Start(DateTimeOffset.UtcNow);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => run.MarkReady());

        // Assert
        Assert.Contains("cannot be marked ready", ex.Message);
    }

    [Fact]
    public void Start_FromDefined_SetsStartedAt_AndRunning()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = DateTimeOffset.UtcNow;

        // Act
        run.Start(startedAt);

        // Assert
        Assert.Equal(startedAt, run.StartedAt);
        Assert.Equal(SimulationRunStatus.Running, run.Status);
        Assert.Null(run.EndedAt);
    }

    [Fact]
    public void Start_FromReady_SetsStartedAt_AndRunning()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = DateTimeOffset.UtcNow;
        run.MarkReady();

        // Act
        run.Start(startedAt);

        // Assert
        Assert.Equal(startedAt, run.StartedAt);
        Assert.Equal(SimulationRunStatus.Running, run.Status);
    }

    [Fact]
    public void Start_Throws_WhenTimeIsDefault()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());

        // Act
        var ex = Assert.Throws<ArgumentException>(() => run.Start(default));

        // Assert
        Assert.Equal("startedAt", ex.ParamName);
        Assert.Contains("must be a valid, non-default timestamp", ex.Message);
    }

    [Fact]
    public void Start_Throws_WhenAlreadyStarted()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = DateTimeOffset.UtcNow;
        run.Start(startedAt);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => run.Start(startedAt));

        // Assert
        Assert.Contains("cannot start from status Running", ex.Message);
        Assert.Equal(SimulationRunStatus.Running, run.Status);
        Assert.Equal(startedAt, run.StartedAt);
    }

    [Fact]
    public void Start_Throws_WhenStatusDoesNotAllowStarting()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        run.Start(DateTimeOffset.UtcNow);
        run.Complete(DateTimeOffset.UtcNow.AddMinutes(1));

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => run.Start(DateTimeOffset.UtcNow.AddMinutes(2)));

        // Assert
        Assert.Contains("cannot start from status", ex.Message);
    }

    [Fact]
    public void Complete_SetsEndedAt_AndCompleted()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = DateTimeOffset.UtcNow;
        var endedAt = startedAt.AddMinutes(10);

        run.Start(startedAt);

        // Act
        run.Complete(endedAt);

        // Assert
        Assert.Equal(startedAt, run.StartedAt);
        Assert.Equal(endedAt, run.EndedAt);
        Assert.Equal(SimulationRunStatus.Completed, run.Status);
    }

    [Fact]
    public void Complete_Throws_WhenNotRunning()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => run.Complete(DateTimeOffset.UtcNow));

        // Assert
        Assert.Contains("cannot end from status", ex.Message);
    }

    [Fact]
    public void Complete_Throws_WhenEndTimeIsDefault()
    {
        var run = new SimulationRun(Guid.NewGuid());
        run.Start(new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero));

        var ex = Assert.Throws<ArgumentException>(() => run.Complete(default));

        Assert.Equal("endedAt", ex.ParamName);
        Assert.Contains("End time must be a valid, non-default timestamp.", ex.Message);
    }

    [Fact]
    public void Complete_Throws_WhenEndTimeIsBeforeStartTime()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = DateTimeOffset.UtcNow;
        run.Start(startedAt);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => run.Complete(startedAt.AddMinutes(-1)));

        // Assert
        Assert.Contains("cannot be earlier than the start time", ex.Message);
    }

    [Fact]
    public void Fail_SetsEndedAt_AndFailed()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = DateTimeOffset.UtcNow;
        var endedAt = startedAt.AddMinutes(3);

        run.Start(startedAt);

        // Act
        run.Fail(endedAt);

        // Assert
        Assert.Equal(endedAt, run.EndedAt);
        Assert.Equal(SimulationRunStatus.Failed, run.Status);
    }

    [Fact]
    public void Fail_Throws_WhenNotRunning()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => run.Fail(DateTimeOffset.UtcNow));

        // Assert
        Assert.Contains("cannot end from status", ex.Message);
    }

    [Fact]
    public void Fail_Throws_WhenEndTimeIsDefault()
    {
        var run = new SimulationRun(Guid.NewGuid());
        run.Start(new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero));

        var ex = Assert.Throws<ArgumentException>(() => run.Fail(default));

        Assert.Equal("endedAt", ex.ParamName);
        Assert.Contains("End time must be a valid, non-default timestamp.", ex.Message);
    }

    [Fact]
    public void Fail_Throws_WhenEndTimeIsBeforeStartTime()
    {
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero);
        run.Start(startedAt);

        var ex = Assert.Throws<InvalidOperationException>(() => run.Fail(startedAt.AddTicks(-1)));

        Assert.Contains("cannot be earlier than the start time", ex.Message);
    }

    [Fact]
    public void Cancel_FromDefined_SetsCancelled_AndEndedAt()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        var cancelledAt = DateTimeOffset.UtcNow;

        // Act
        run.Cancel(cancelledAt);

        // Assert
        Assert.Equal(cancelledAt, run.EndedAt);
        Assert.Equal(SimulationRunStatus.Cancelled, run.Status);
        Assert.Null(run.StartedAt);
    }

    [Fact]
    public void Cancel_FromRunning_SetsCancelled_AndEndedAt()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = DateTimeOffset.UtcNow;
        var cancelledAt = startedAt.AddMinutes(2);

        run.Start(startedAt);

        // Act
        run.Cancel(cancelledAt);

        // Assert
        Assert.Equal(startedAt, run.StartedAt);
        Assert.Equal(cancelledAt, run.EndedAt);
        Assert.Equal(SimulationRunStatus.Cancelled, run.Status);
    }

    [Fact]
    public void Cancel_FromReady_SetsCancelledWithoutStartedAt()
    {
        var run = new SimulationRun(Guid.NewGuid());
        var cancelledAt = new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero);
        run.MarkReady();

        run.Cancel(cancelledAt);

        Assert.Null(run.StartedAt);
        Assert.Equal(cancelledAt, run.EndedAt);
        Assert.Equal(SimulationRunStatus.Cancelled, run.Status);
    }

    [Fact]
    public void Cancel_Throws_WhenTimeIsDefault()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());

        // Act
        var ex = Assert.Throws<ArgumentException>(() => run.Cancel(default));

        // Assert
        Assert.Equal("endedAt", ex.ParamName);
        Assert.Contains("must be a valid, non-default timestamp", ex.Message);
    }

    [Fact]
    public void Cancel_Throws_WhenAlreadyCompleted()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = DateTimeOffset.UtcNow;

        run.Start(startedAt);
        run.Complete(startedAt.AddMinutes(1));

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => run.Cancel(startedAt.AddMinutes(2)));

        // Assert
        Assert.Contains("cannot be cancelled from status", ex.Message);
    }

    [Theory]
    [InlineData(SimulationRunStatus.Completed)]
    [InlineData(SimulationRunStatus.Failed)]
    [InlineData(SimulationRunStatus.Cancelled)]
    public void Cancel_TerminalStatus_ThrowsInvalidOperationException(SimulationRunStatus terminalStatus)
    {
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero);
        run.Start(startedAt);

        switch (terminalStatus)
        {
            case SimulationRunStatus.Completed:
                run.Complete(startedAt.AddMinutes(1));
                break;
            case SimulationRunStatus.Failed:
                run.Fail(startedAt.AddMinutes(1));
                break;
            case SimulationRunStatus.Cancelled:
                run.Cancel(startedAt.AddMinutes(1));
                break;
        }

        var ex = Assert.Throws<InvalidOperationException>(() => run.Cancel(startedAt.AddMinutes(2)));

        Assert.Contains($"cannot be cancelled from status {terminalStatus}", ex.Message);
    }

    [Fact]
    public void Complete_CalledTwice_ThrowsInvalidOperationException()
    {
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero);
        run.Start(startedAt);
        run.Complete(startedAt.AddMinutes(1));

        var ex = Assert.Throws<InvalidOperationException>(() => run.Complete(startedAt.AddMinutes(2)));

        Assert.Contains("cannot end from status Completed", ex.Message);
    }

    [Fact]
    public void Fail_CalledTwice_ThrowsInvalidOperationException()
    {
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero);
        run.Start(startedAt);
        run.Fail(startedAt.AddMinutes(1));

        var ex = Assert.Throws<InvalidOperationException>(() => run.Fail(startedAt.AddMinutes(2)));

        Assert.Contains("cannot end from status Failed", ex.Message);
        Assert.Equal(SimulationRunStatus.Failed, run.Status);
        Assert.Equal(startedAt.AddMinutes(1), run.EndedAt);
    }

    [Fact]
    public void Complete_AfterFail_ThrowsInvalidOperationException()
    {
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.Zero);
        run.Start(startedAt);
        run.Fail(startedAt.AddMinutes(1));

        var ex = Assert.Throws<InvalidOperationException>(() => run.Complete(startedAt.AddMinutes(2)));

        Assert.Contains("cannot end from status Failed", ex.Message);
        Assert.Equal(SimulationRunStatus.Failed, run.Status);
    }

    [Fact]
    public void Cancel_Throws_WhenTimeIsBeforeStartTime()
    {
        // Arrange
        var run = new SimulationRun(Guid.NewGuid());
        var startedAt = DateTimeOffset.UtcNow;
        run.Start(startedAt);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => run.Cancel(startedAt.AddMinutes(-1)));

        // Assert
        Assert.Contains("cannot be earlier than the start time", ex.Message);
    }
}
