// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Microsoft.EntityFrameworkCore.Update.Internal;

/// <summary>
///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
///     the same compatibility standards as public APIs. It may be changed or removed without notice in
///     any release. You should only use it directly in your code with extreme caution and knowing that
///     doing so can result in application failures when updating to a new Entity Framework Core release.
/// </summary>
public abstract class CompositeRowValueFactory
{
    private readonly IReadOnlyList<ColumnAccessors> _columnAccessors;

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public CompositeRowValueFactory(IReadOnlyList<IColumn> columns)
    {
        Columns = columns;
        var columnAccessors = new ColumnAccessors[columns.Count];
        for(var i = 0; i < columns.Count; i++)
        {
            columnAccessors[i] = ((Column)columns[i]).Accessors;
        }

        _columnAccessors = columnAccessors;

        EqualityComparer = CreateEqualityComparer(columns);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual IEqualityComparer<object?[]> EqualityComparer { get; }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected virtual IReadOnlyList<IColumn> Columns { get; }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual bool TryCreateDependentKeyValue(object?[] keyValues, [NotNullWhen(true)] out object?[]? key)
    {
        key = keyValues;
        return keyValues.All(k => k != null);
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual bool TryCreateDependentKeyValue(IDictionary<string, object?> keyValues, [NotNullWhen(true)] out object?[]? key)
    {
        key = new object[Columns.Count];
        var index = 0;

        foreach (var column in Columns)
        {
            if (!keyValues.TryGetValue(column.Name, out var value)
                || value == null)
            {
                key = null;
                return false;
            }
            key[index++] = value;
        }

        return true;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    public virtual bool TryCreateDependentKeyValue(IReadOnlyModificationCommand command, bool fromOriginalValues, [NotNullWhen(true)] out object?[]? key)
    {
        key = new object[Columns.Count];
        var index = 0;

        for (var i = 0; i < Columns.Count; i++)
        {
            var column = Columns[i];

            if (command.Entries.Count > 0)
            {
                object? value = null;
                var valueFound = false;
                foreach (var entry in command.Entries)
                {
                    var property = column.FindMappedProperty(entry.EntityType);
                    if (property == null)
                    {
                        continue;
                    }

                    value = fromOriginalValues ? entry.GetOriginalValue(property) : entry.GetCurrentProviderValue(property);
                    if (!fromOriginalValues
                        && (entry.EntityState == EntityState.Added
                            || entry.IsModified(property)))
                    {
                        key[index++] = value;
                        continue;
                    }

                    if (fromOriginalValues
                        && (entry.EntityState == EntityState.Unchanged
                            || (entry.EntityState == EntityState.Modified && !entry.IsModified(property))))
                    {
                        key[index++] = value;
                        continue;
                    }
                    valueFound = true;
                }

                if (!valueFound)
                {
                    return false;
                }
                key[index++] = value;
            }
            else
            {
                var modification = command.ColumnModifications.FirstOrDefault(m => m.ColumnName == column.Name);
                if (modification == null)
                {
                    key = null;
                    return false;
                }

                var value = fromOriginalValues ? modification.OriginalValue : modification.Value;
                key[index++] = value;
            }
        }

        return true;
    }

    /// <summary>
    ///     This is an internal API that supports the Entity Framework Core infrastructure and not subject to
    ///     the same compatibility standards as public APIs. It may be changed or removed without notice in
    ///     any release. You should only use it directly in your code with extreme caution and knowing that
    ///     doing so can result in application failures when updating to a new Entity Framework Core release.
    /// </summary>
    protected static IEqualityComparer<object?[]> CreateEqualityComparer(IReadOnlyList<IColumn> columns)
        => new CompositeCustomComparer(columns.Select(c => c.PropertyMappings.First().TypeMapping.ProviderComparer).ToList());

    private sealed class CompositeCustomComparer : IEqualityComparer<object?[]>
    {
        private readonly Func<object?, object?, bool>[] _equals;
        private readonly Func<object, int>[] _hashCodes;

        public CompositeCustomComparer(IList<ValueComparer> comparers)
        {
            _equals = comparers.Select(c => (Func<object?, object?, bool>)c.Equals).ToArray();
            _hashCodes = comparers.Select(c => (Func<object, int>)c.GetHashCode).ToArray();
        }

        public bool Equals(object?[]? x, object?[]? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null)
            {
                return y is null;
            }

            if (y is null)
            {
                return false;
            }

            if (x.Length != y.Length)
            {
                return false;
            }

            for (var i = 0; i < x.Length; i++)
            {
                if (!_equals[i](x[i], y[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(object?[] obj)
        {
            var hashCode = 0;

            // ReSharper disable once ForCanBeConvertedToForeach
            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var i = 0; i < obj.Length; i++)
            {
                var value = obj[i];
                var hash = value == null ? 0 : _hashCodes[i](value);
                hashCode = (hashCode * 397) ^ hash;
            }

            return hashCode;
        }
    }
}
