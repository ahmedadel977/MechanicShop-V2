using MechanicShop.Application.Common.Behaviours;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

using Moq;
using Xunit;

namespace MechanicShop.Application.UnitTests.Behaviours;

public class CachingBehaviorTests
{
    // Arrange: Mock Dependencies
    private readonly Mock<HybridCache> _cacheMock = new();

    private readonly Mock<ILogger<CachingBehavior<CachedQuery, Result<string>>>>
        _loggerMock = new();

    // System Under Test
    private readonly CachingBehavior<CachedQuery, Result<string>> _sut;

    public CachingBehaviorTests()
    {
        _sut = new CachingBehavior<CachedQuery, Result<string>>(
            _loggerMock.Object,
            _cacheMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNotCachedQuery_ShouldSkipCacheAndReturnResult()
    {
        // Arrange
        var uncachedRequest = new NonCachedQuery();

        var loggerMock =
            new Mock<ILogger<CachingBehavior<NonCachedQuery, string>>>();

        var behavior = new CachingBehavior<NonCachedQuery, string>(
            loggerMock.Object,
            _cacheMock.Object);

        // Act
        var result = await behavior.Handle(
            uncachedRequest,
            _ => Task.FromResult("OK"),
            CancellationToken.None);

        // Assert
        Assert.Equal("OK", result);

        _cacheMock.Verify(
            x => x.SetAsync<string>(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCachedQueryAndResultIsSuccess_ShouldCacheResult()
    {
        // Arrange
        var request = new CachedQuery();

        var response = (Result<string>)"test-value";

        string? actualKey = null;
        Result<string>? actualValue = null;
        HybridCacheEntryOptions? actualOptions = null;
        IEnumerable<string>? actualTags = null;

        _cacheMock
            .Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<Result<string>>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Result<string>, HybridCacheEntryOptions,
                IEnumerable<string>, CancellationToken>(
                (key, value, options, tags, token) =>
                {
                    actualKey = key;
                    actualValue = value;
                    actualOptions = options;
                    
                })
            .Returns(ValueTask.CompletedTask);

        // Act
        var result = await _sut.Handle(
            request,
            _ => Task.FromResult(response),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        Assert.Equal(request.CacheKey, actualKey);

        Assert.NotNull(actualValue);
        Assert.True(actualValue.IsSuccess);
        Assert.Equal("test-value", actualValue.Value);

        Assert.NotNull(actualOptions);
        Assert.Equal(request.Expiration, actualOptions.Expiration);

       
    }

    [Fact]
    public async Task Handle_WhenCachedQueryAndResultIsError_ShouldNotCacheResult()
    {
        // Arrange
        var request = new CachedQuery();

        var errorResult =
            (Result<string>)Error.Validation("code", "message");

        // Act
        var result = await _sut.Handle(
            request,
            _ => Task.FromResult(errorResult),
            CancellationToken.None);

        // Assert
        Assert.True(result.IsError);

        _cacheMock.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<Result<string>>(),
                It.IsAny<HybridCacheEntryOptions>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // Request without caching
    public class NonCachedQuery;

    // Request with caching
    public class CachedQuery : ICachedQuery
    {
        public string CacheKey => "test-key";

        public TimeSpan Expiration => TimeSpan.FromMinutes(5);

        public string[] Tags => ["unit-test"];
    }
}