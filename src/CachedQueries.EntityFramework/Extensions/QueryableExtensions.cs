﻿using System.Linq.Expressions;
using System.Reflection;
using CachedQueries.Core;
using CachedQueries.Core.Enums;
using CachedQueries.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace CachedQueries.EntityFramework.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    ///     Cache and return query results with write-through strategy.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        TimeSpan expire,
        CancellationToken cancellationToken) where T : class
    {
        var context = query.GetContext();
        if (context is null)
            return await query.ToListAsync(cancellationToken);

        var cacheManager = context.CacheManager;
        var key = cacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.ToListAsync(cancellationToken);

        var cacheStore = cacheManager.CacheStoreProvider.GetCacheStore(key, tags, CacheContentType.Collection);
        var cached = await cacheStore.GetAsync<IEnumerable<T>>(key, useLock: true, cancellationToken);
        if (cached is not null)
            return cached.ToList();

        var value = await query.ToListAsync(cancellationToken);
        await cacheStore.SetAsync(key, value, useLock: true, expire, cancellationToken);
        await cacheManager.CacheInvalidator.LinkTagsAsync(key, tags, cancellationToken);

        return value;
    }

    /// <summary>
    ///     Cache and return query results with write-through strategy.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken) where T : class
    {
        var context = query.GetContext();
        if (context is null)
            return await query.ToListAsync(cancellationToken);

        return await query.ToCachedListAsync(tags, context.CacheManager.CacheOptions.DefaultExpiration, cancellationToken);
    }

    /// <summary>
    ///     Cache query results with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        TimeSpan expire,
        CancellationToken cancellationToken) where T : class
    {
        var tags = RetrieveInvalidationTagsFromQuery(query);
        return query.ToCachedListAsync(tags, expire, cancellationToken);
    }

    /// <summary>
    ///     Cache query results with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>List query results</returns>
    public static async Task<List<T>> ToCachedListAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken) where T : class
    {
        var context = query.GetContext();
        if (context is null)
            return await query.ToListAsync(cancellationToken);

        var tags = RetrieveInvalidationTagsFromQuery(query);
        return await query.ToCachedListAsync(tags, context.CacheManager.CacheOptions.DefaultExpiration, cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        TimeSpan expire,
        CancellationToken cancellationToken) where T : class
    {
        var context = query.GetContext();
        if (context is null)
            return await query.FirstOrDefaultAsync(cancellationToken);

        var cacheManager = context.CacheManager;
        var key = cacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.FirstOrDefaultAsync(cancellationToken);

        var cacheStore = cacheManager.CacheStoreProvider.GetCacheStore(key, tags, CacheContentType.Object);
        var cached = await cacheStore.GetAsync<T>(key, useLock: true, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await query.FirstOrDefaultAsync(cancellationToken);
        await cacheStore.SetAsync(key, value, useLock: true, expire, cancellationToken);
        await cacheManager.CacheInvalidator.LinkTagsAsync(key, tags, cancellationToken);

        return value;
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken) where T : class
    {
        var context = query.GetContext();
        if (context is null)
            return await query.FirstOrDefaultAsync(cancellationToken);

        return await query.CachedFirstOrDefaultAsync(tags, context.CacheManager.CacheOptions.DefaultExpiration, cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        IReadOnlyCollection<string> tags,
        TimeSpan expire,
        CancellationToken cancellationToken) where T : class
    {
        var context = query.GetContext();
        if (context is null)
            return await query.FirstOrDefaultAsync(cancellationToken);

        var cacheManager = context.CacheManager;
        query = query.Where(predicate);
        var key = cacheManager.CacheKeyFactory.GetCacheKey(query, tags);
        if (string.IsNullOrEmpty(key))
            return await query.FirstOrDefaultAsync(cancellationToken);

        var cacheStore = cacheManager.CacheStoreProvider.GetCacheStore(key, tags, CacheContentType.Object);
        var cached = await cacheStore.GetAsync<T>(key, useLock: true, cancellationToken);
        if (cached is not null)
            return cached;

        var value = await query.FirstOrDefaultAsync(cancellationToken);
        await cacheStore.SetAsync(key, value, useLock: true, expire, cancellationToken);
        await cacheManager.CacheInvalidator.LinkTagsAsync(key, tags, cancellationToken);

        return value;
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <param name="expire">Expiration timespan</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        TimeSpan expire,
        CancellationToken cancellationToken) where T : class
    {
        var tags = RetrieveInvalidationTagsFromQuery(query);
        return query.CachedFirstOrDefaultAsync(tags, expire, cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        CancellationToken cancellationToken) where T : class
    {
        var context = query.GetContext();
        if (context is null)
            return await query.FirstOrDefaultAsync(cancellationToken);

        var tags = RetrieveInvalidationTagsFromQuery(query);
        return await query.CachedFirstOrDefaultAsync(tags, context.CacheManager.CacheOptions.DefaultExpiration, cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="expire">Expiration timespan</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        TimeSpan expire,
        CancellationToken cancellationToken) where T : class
    {
        var tags = RetrieveInvalidationTagsFromQuery(query);
        return query.CachedFirstOrDefaultAsync(predicate, tags, expire, cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="tags">Invalidation tags</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        IReadOnlyCollection<string> tags,
        CancellationToken cancellationToken) where T : class
    {
        var context = query.GetContext();
        if (context is null)
            return await query.FirstOrDefaultAsync(cancellationToken);

        return await query.CachedFirstOrDefaultAsync(predicate, tags, context.CacheManager.CacheOptions.DefaultExpiration, cancellationToken);
    }

    /// <summary>
    ///     Cache and return query first result with write-through strategy.
    ///     Using tags for invalidation as type names from Include and ThenInclude methods.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="query">Query to cache</param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>FirstOrDefault query result</returns>
    public static async Task<T?> CachedFirstOrDefaultAsync<T>(this IQueryable<T> query,
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken) where T : class
    {
        var context = query.GetContext();
        if (context is null)
            return await query.FirstOrDefaultAsync(cancellationToken);

        var tags = RetrieveInvalidationTagsFromQuery(query);
        return await query.CachedFirstOrDefaultAsync(predicate, tags, context.CacheManager.CacheOptions.DefaultExpiration, cancellationToken);
    }

    private static List<string> RetrieveInvalidationTagsFromQuery(IQueryable query)
    {
        var includedTypes = query.GetIncludeTypes();
        var tags = includedTypes
            .Select(x => x.FullName)
            .Where(x => !string.IsNullOrEmpty(x))
            .Cast<string>()
            .ToList();
        return tags;
    }

    private static ICachedContext? GetContext<T>(this IQueryable<T> query) where T : class
    {
        if (query is not EntityQueryable<T> entityQuery)
            return null;

        var provider = entityQuery.Provider;

        var cf= provider
            .GetType()
            .GetField("_queryCompiler", BindingFlags.NonPublic | BindingFlags.Instance)?
            .GetValue(provider);

        if (cf is null)
        {
            return null;
        }
        return null;
    }
}
