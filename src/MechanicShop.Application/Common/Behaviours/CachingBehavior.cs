using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results.Abstractions;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace MechanicShop.Application.Common.Behaviours
{
    public class CachingBehavior<TRequest, TResponse>(
        ILogger<CachingBehavior<TRequest, TResponse>> logger,
        HybridCache cache,
        ICurrentBranchContext? currentBranchContext = null)
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly HybridCache _cache = cache;
        private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger = logger;
        private readonly ICurrentBranchContext? _currentBranchContext = currentBranchContext;
        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
        {
            if (request is not ICachedQuery cacheableRequest)
            {
                return await next();
            }
            var cacheKey = BuildCacheKey(cacheableRequest.CacheKey);
            _logger.LogInformation("Checking cache for {RequestName}", typeof(TRequest).Name);
            var result = await _cache.GetOrCreateAsync<TResponse>(
                cacheKey,
                _ => new ValueTask<TResponse>((TResponse)(object)null!),
                new HybridCacheEntryOptions
                {
                    Flags = HybridCacheEntryFlags.DisableUnderlyingData
                },
                cancellationToken:ct);
            if  (result is null)
            {
                _logger.LogInformation("Cache miss for {RequestName}", typeof(TRequest).Name);
               
                result = await next();
                if ( result is IResult res && res.IsSuccess)
                {
                    await _cache.SetAsync(
                  cacheKey,
                  result,
                  new HybridCacheEntryOptions
                  {
                      Expiration = cacheableRequest.Expiration,
                  },
                  cancellationToken: ct);
                }
              

                  
            }
            else
            {
                _logger.LogInformation("Cache hit for {RequestName}", typeof(TRequest).Name);
            }
            return result;

        }

        private string BuildCacheKey(string key)
        {
            if (_currentBranchContext is null)
            {
                return key;
            }

            var scope = _currentBranchContext.IsCompanyWide ? "company" : "branch";
            var branchId = _currentBranchContext.BranchId?.ToString("N") ?? "none";
            var identityPartition = _currentBranchContext.IsLabor
                ? $"labor:{_currentBranchContext.EmployeeId?.ToString("N") ?? "none"}"
                : "management";
            return $"{scope}:{branchId}:{identityPartition}:{key}";
        }
    }
}
