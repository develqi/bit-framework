﻿using Foundation.DataAccess.Contracts;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using System;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Foundation.DataAccess.Implementations.EntityFrameworkCore
{
    public class EfCoreAsyncQueryableExecuter : IAsyncQueryableExecuter
    {
        public Task<T> FirstOrDefaultAsync<T>(IQueryable<T> source, CancellationToken cancellationToken)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return source.FirstOrDefaultAsync(cancellationToken);
        }

        public virtual Task<long> LongCountAsync<T>(IQueryable<T> source, CancellationToken cancellationToken)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return source
                .LongCountAsync(cancellationToken);
        }

        public bool SupportsAsyncExecution<T>(IQueryable source)
        {
            // https://github.com/aspnet/EntityFramework/issues/6534
            return false;

            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return source is EntityQueryable<T>;
        }

        public virtual Task<List<T>> ToListAsync<T>(IQueryable<T> source, CancellationToken cancellationToken)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return source
                .ToListAsync(cancellationToken);
        }
    }
}
