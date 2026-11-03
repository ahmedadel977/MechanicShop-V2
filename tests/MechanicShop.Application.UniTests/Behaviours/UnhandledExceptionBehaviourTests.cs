using MechanicShop.Application.Common.Behaviours;

using MediatR;

using Microsoft.Extensions.Logging;

using Moq;
using Xunit;

namespace MechanicShop.Application.UnitTests.Behaviours;

public class UnhandledExceptionBehaviourTests
{
    private readonly Mock<ILogger<DummyRequest>> _loggerMock = new();

    private readonly UnhandledExceptionBehaviour<DummyRequest, string> _sut;

    public UnhandledExceptionBehaviourTests()
    {
        _sut = new UnhandledExceptionBehaviour<DummyRequest, string>(
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoException_InvokesNextAndReturnsResult()
    {
        // Arrange
        var request = new DummyRequest();

        RequestHandlerDelegate<string> next =
            _ => Task.FromResult("OK");

        // Act
        var result = await _sut.Handle(
            request,
            next,
            CancellationToken.None);

        // Assert
        Assert.Equal("OK", result);
    }

    [Fact]
    public async Task Handle_WhenExceptionThrown_LogsErrorAndRethrows()
    {
        // Arrange
        var request = new DummyRequest();

        var exception = new InvalidOperationException("test failure");

        RequestHandlerDelegate<string> next =
            _ => Task.FromException<string>(exception);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.Handle(
                request,
                next,
                CancellationToken.None));

        // Assert
        Assert.Equal(exception, ex);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>(
                    (state, type) =>
                        state != null && state.ToString()!.Contains("Unhandled Exception")),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    public class DummyRequest;
}
