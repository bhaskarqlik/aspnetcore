// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure
{
    internal class PageActionInvokerProvider : IActionInvokerProvider
    {
        private readonly PageLoader _pageLoader;
        private readonly PageActionInvokerCache _pageActionInvokerCache;
        private readonly IReadOnlyList<IValueProviderFactory> _valueProviderFactories;
        private readonly IModelMetadataProvider _modelMetadataProvider;
        private readonly ITempDataDictionaryFactory _tempDataFactory;
        private readonly MvcViewOptions _mvcViewOptions;
        private readonly IPageHandlerMethodSelector _selector;
        private readonly DiagnosticListener _diagnosticListener;
        private readonly ILogger<PageActionInvoker> _logger;
        private readonly IActionResultTypeMapper _mapper;
        private readonly IActionContextAccessor _actionContextAccessor;

        public PageActionInvokerProvider(
            PageLoader pageLoader,
            PageActionInvokerCache pageActionInvokerCache,
            IModelMetadataProvider modelMetadataProvider,
            ITempDataDictionaryFactory tempDataFactory,
            IOptions<MvcOptions> mvcOptions,
            IOptions<MvcViewOptions> mvcViewOptions,
            IPageHandlerMethodSelector selector,
            DiagnosticListener diagnosticListener,
            ILoggerFactory loggerFactory,
            IActionResultTypeMapper mapper,
            IActionContextAccessor actionContextAccessor = null)
        {
            _pageLoader = pageLoader;
            _pageActionInvokerCache = pageActionInvokerCache;
            _valueProviderFactories = mvcOptions.Value.ValueProviderFactories.ToArray();
            _modelMetadataProvider = modelMetadataProvider;
            _tempDataFactory = tempDataFactory;
            _mvcViewOptions = mvcViewOptions.Value;
            _selector = selector;
            _diagnosticListener = diagnosticListener;
            _logger = loggerFactory.CreateLogger<PageActionInvoker>();
            _mapper = mapper;
            _actionContextAccessor = actionContextAccessor ?? ActionContextAccessor.Null;
        }

        // For testing
        internal PageActionInvokerCache Cache => _pageActionInvokerCache;

        public int Order { get; } = -1000;

        public void OnProvidersExecuting(ActionInvokerProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var actionContext = context.ActionContext;

            if (actionContext.ActionDescriptor is not PageActionDescriptor page)
            {
                return;
            }

            if (page.CompiledPageDescriptor == null)
            {
                // With legacy routing, we're forced to perform a blocking call. The exceptation is that
                // in the most common case - build time views or successsively cached runtime views - this should finish synchronously.
                page.CompiledPageDescriptor = _pageLoader.LoadAsync(page).GetAwaiter().GetResult();
            }

            var (cacheEntry, filters) = _pageActionInvokerCache.GetCachedResult(actionContext);

            var pageContext = new PageContext(actionContext)
            {
                ActionDescriptor = cacheEntry.ActionDescriptor,
                ValueProviderFactories = new CopyOnWriteList<IValueProviderFactory>(_valueProviderFactories),
                ViewData = cacheEntry.ViewDataFactory(_modelMetadataProvider, actionContext.ModelState),
                ViewStartFactories = cacheEntry.ViewStartFactories.ToList(),
            };

            context.Result = new PageActionInvoker(
                _selector,
                _diagnosticListener,
                _logger,
                _actionContextAccessor,
                _mapper,
                pageContext,
                filters,
                cacheEntry,
                _tempDataFactory,
                _mvcViewOptions.HtmlHelperOptions);
        }

        public void OnProvidersExecuted(ActionInvokerProviderContext context)
        {

        }
    }
}
