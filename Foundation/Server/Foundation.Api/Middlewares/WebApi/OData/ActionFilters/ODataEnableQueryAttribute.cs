﻿using Foundation.DataAccess.Contracts;
using Microsoft.Owin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Filters;
using System.Web.OData;
using System.Web.OData.Extensions;
using System.Web.OData.Query;

namespace Foundation.Api.Middlewares.WebApi.OData.ActionFilters
{
    public class ODataEnableQueryAttribute : EnableQueryAttribute
    {
        private int? _defaultPageSize = null;

        public virtual int? DefaultPageSize
        {
            get { return _defaultPageSize; }
            set
            {
                _defaultPageSize = value;
                if (value.HasValue)
                    PageSize = value.Value;
            }
        }

        public override async Task OnActionExecutedAsync(HttpActionExecutedContext actionExecutedContext, CancellationToken cancellationToken)
        {
            if (actionExecutedContext?.Response?.Content is ObjectContent &&
                actionExecutedContext?.Response?.IsSuccessStatusCode == true)
            {
                ObjectContent objContent = ((ObjectContent)(actionExecutedContext.Response.Content));

                if (objContent.Value == null)
                {
                    actionExecutedContext.Response.StatusCode = HttpStatusCode.NoContent;
                    actionExecutedContext.Response.Content = null;
                }
                else
                {
                    TypeInfo actionReturnType = objContent.Value.GetType().GetTypeInfo();

                    if (typeof(string) != actionReturnType && typeof(IEnumerable).GetTypeInfo().IsAssignableFrom(actionReturnType))
                    {
                        bool isIQueryable = typeof(IQueryable).GetTypeInfo().IsAssignableFrom(actionReturnType);

                        TypeInfo queryElementType = actionReturnType.HasElementType ? actionReturnType.GetElementType().GetTypeInfo() : actionReturnType.GetGenericArguments().First() /* Why not calling Single() ? http://stackoverflow.com/questions/41718323/why-variable-of-type-ienumerablesomething-gettype-getgenericargsuments-c */.GetTypeInfo();
                        IAsyncQueryableExecuter asyncQueryableExecuterToUse = null;

                        if (isIQueryable == true)
                        {
                            IEnumerable<IAsyncQueryableExecuter> asyncQueryableExecuters = actionExecutedContext.Request.GetOwinContext().GetDependencyResolver().ResolveAll<IAsyncQueryableExecuter>();
                            asyncQueryableExecuterToUse = (IAsyncQueryableExecuter)typeof(ODataEnableQueryAttribute).GetMethod(nameof(FindAsyncQueryableExecuter)).MakeGenericMethod(queryElementType).Invoke(this, new object[] { objContent.Value, asyncQueryableExecuters });
                        }
                        else
                        {
                            objContent.Value = typeof(ODataEnableQueryAttribute).GetMethod(nameof(ToQueryable)).MakeGenericMethod(queryElementType).Invoke(this, new object[] { objContent.Value });
                        }

                        HttpRequestMessageProperties requestODataProps = actionExecutedContext.Request.ODataProperties();
                        ODataQueryContext currentOdataQueryContext = new ODataQueryContext(actionExecutedContext.Request.GetModel(), queryElementType, requestODataProps.Path);
                        ODataQueryOptions currentOdataQueryOptions = new ODataQueryOptions(currentOdataQueryContext, actionExecutedContext.Request);
                        ODataQuerySettings globalODataQuerySettings = new ODataQuerySettings
                        {
                            EnableConstantParameterization = this.EnableConstantParameterization,
                            EnsureStableOrdering = this.EnsureStableOrdering,
                            HandleNullPropagation = this.HandleNullPropagation,
                            PageSize = this.DefaultPageSize
                        };

                        ValidateQuery(actionExecutedContext.Request, currentOdataQueryOptions);

                        int? currentQueryPageSize = currentOdataQueryOptions?.Top?.Value;
                        int? globalQuerypageSize = globalODataQuerySettings.PageSize;
                        int? pageSize = null;

                        if (currentQueryPageSize.HasValue)
                            pageSize = currentQueryPageSize.Value;
                        else if (globalQuerypageSize.HasValue == true)
                            pageSize = globalQuerypageSize.Value;
                        else
                            pageSize = null;

                        globalODataQuerySettings.PageSize = null; // ApplyTo will enumerates the query for values other than null. We are gonna apply take in ToList & ToListAsync methods.

                        if (currentOdataQueryOptions.Filter != null)
                        {
                            objContent.Value = currentOdataQueryOptions.Filter.ApplyTo(query: (IQueryable)objContent.Value, querySettings: globalODataQuerySettings);
                        }

                        if (currentOdataQueryOptions.Count?.Value == true && pageSize.HasValue == true)
                        {
                            long count = default(long);
                            if (asyncQueryableExecuterToUse != null)
                                count = await (Task<long>)typeof(ODataEnableQueryAttribute).GetMethod(nameof(GetCountAsync)).MakeGenericMethod(queryElementType).Invoke(this, new object[] { objContent.Value, asyncQueryableExecuterToUse, cancellationToken });
                            else
                                count = (long)typeof(ODataEnableQueryAttribute).GetMethod(nameof(GetCount)).MakeGenericMethod(queryElementType).Invoke(this, new object[] { objContent.Value });

                            actionExecutedContext.Request.Properties["System.Web.OData.TotalCountFunc"] = new Func<long>(() => count);
                        }

                        objContent.Value = currentOdataQueryOptions.ApplyTo(query: (IQueryable)objContent.Value, querySettings: globalODataQuerySettings, ignoreQueryOptions: AllowedQueryOptions.Filter);

                        if (currentOdataQueryOptions.SelectExpand != null)
                            queryElementType = objContent.Value.GetType().GetTypeInfo().GetGenericArguments().Single().GetTypeInfo();

                        if (asyncQueryableExecuterToUse != null)
                            objContent.Value = await (Task<object>)typeof(ODataEnableQueryAttribute).GetMethod(nameof(ToListAsync)).MakeGenericMethod(queryElementType).Invoke(this, new object[] { objContent.Value, asyncQueryableExecuterToUse, pageSize, cancellationToken });
                        else
                            objContent.Value = typeof(ODataEnableQueryAttribute).GetMethod(nameof(ToList)).MakeGenericMethod(queryElementType).Invoke(this, new object[] { objContent.Value, pageSize });

                        if (currentOdataQueryOptions.Count?.Value == true && pageSize.HasValue == false)
                        {
                            // We've no paging becuase there is no global config for max top and there is no top specified by the client's request, so the retured result of query's length is equivalent to total count of the query
                            long count = ((IList)objContent.Value).Count;
                            actionExecutedContext.Request.Properties["System.Web.OData.TotalCountFunc"] = new Func<long>(() => count);
                        }
                    }
                }
            }
        }

        public virtual IAsyncQueryableExecuter FindAsyncQueryableExecuter<T>(IQueryable<T> query, IEnumerable<IAsyncQueryableExecuter> asyncQueryableExecuters)
            where T : class
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            if (asyncQueryableExecuters == null)
                throw new ArgumentNullException(nameof(asyncQueryableExecuters));

            return asyncQueryableExecuters
                .FirstOrDefault(asyncQueryableExecuter => asyncQueryableExecuter.SupportsAsyncExecution<T>(query));
        }

        public virtual long GetCount<T>(IQueryable<T> query)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            return query.Count();
        }

        public virtual List<T> ToList<T>(IQueryable<T> query, int? pageSize)
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            if (pageSize.HasValue == true)
            {
                query = query.Take(pageSize.Value);
            }

            return query.ToList();
        }

        public virtual async Task<long> GetCountAsync<T>(IQueryable<T> query, IAsyncQueryableExecuter asyncQueryableExecuter, CancellationToken cancellationToken)
            where T : class
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            if (asyncQueryableExecuter == null)
                throw new ArgumentNullException(nameof(asyncQueryableExecuter));

            return await asyncQueryableExecuter.LongCountAsync(query, cancellationToken);
        }

        public virtual async Task<object> ToListAsync<T>(IQueryable<T> query, IAsyncQueryableExecuter asyncQueryableExecuter, int? pageSize, CancellationToken cancellationToken)
            where T : class
        {
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            if (asyncQueryableExecuter == null)
                throw new ArgumentNullException(nameof(asyncQueryableExecuter));

            if (pageSize.HasValue == true)
            {
                query = query.Take(pageSize.Value);
            }

            return await asyncQueryableExecuter.ToListAsync(query, cancellationToken);
        }

        public virtual IQueryable<T> ToQueryable<T>(IEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            return source.AsQueryable();
        }

        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {

        }
    }
}
