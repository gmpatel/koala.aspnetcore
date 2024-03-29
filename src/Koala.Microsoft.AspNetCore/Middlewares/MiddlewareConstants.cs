﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Constants;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Microsoft.AspNetCore.Middlewares
{
    public static class MiddlewareConstants
    {
        public static bool? ForceGarbageCollectBeforeEveryApiCall { get; set; }

        public static bool? ForceGarbageCollectAfterEveryApiCall { get; set; }

        public static bool? LogApiCallExecuting { get; set; }

        public static bool? LogApiCallExecuted { get; set; }

        public static readonly IList<string> IncludedPathsInRequestAuthorization = new List<string>
            {
                "/api"
            };

        public static readonly IList<string> IncludedPathsForXIdAutoGenerated =
            new List<string> // "POST:/api/edit/configure"
            {
                "/api"
            };

        public static readonly IList<string> ExemptedPathsFromRequestAuthorization = new List<string>
            {
                "/api/xerror",
                "/api/xhealth",
                "/api/xinfo",
                "/api/xswitch",
                "/api/xtroubleshoot",
                "/api/error",
                "/api/health",
                "/api/info",
                "/api/switch",
                "/api/troubleshoot",
                "/api/helper",
                "/api/teamtools"
            };

        public static readonly IList<string> ExemptedPathsFromRequestLogs = new List<string>
            {
                "/api/xhealth"
            };

        public static string UnauthorizedMessage { get; set; } =
            "Unauthorized to access the resource! Please provide required access information.";

        public static bool DoesRequireXIdHeaderValueFilled(HttpContext httpContext = null, OperationFilterContext operationFilterContext = null)
        {
            var relativePath = GetRelativePath(httpContext, operationFilterContext, false);

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return false;
            }

            if (!IncludedPathsForXIdAutoGenerated.Any(x =>
                    relativePath.StartsWith(x, StringComparison.InvariantCultureIgnoreCase)))
            {
                return false;
            }

            return true;
        }

        public static bool DoesRequireRequestLogging(HttpContext httpContext = null, OperationFilterContext operationFilterContext = null)
        {
            var relativePath = GetRelativePath(httpContext, operationFilterContext);

            if (ExemptedPathsFromRequestLogs.Any(x =>
                    relativePath.StartsWith(x, StringComparison.InvariantCultureIgnoreCase)))
            {
                return false;
            }

            return true;
        }

        public static bool DoesRequireAccessTokenHeader(HttpContext httpContext = null, OperationFilterContext operationFilterContext = null)
        {
            var relativePath = GetRelativePath(httpContext, operationFilterContext);
            var relativePathWithMethod = GetRelativePath(httpContext, operationFilterContext, true);

            if (string.IsNullOrWhiteSpace(relativePath))
                return false;

            if (!IncludedPathsInRequestAuthorization.Any(x => relativePath.StartsWith(x, StringComparison.InvariantCultureIgnoreCase)))
                return false;

            if (ExemptedPathsFromRequestAuthorization.Any(x => relativePath.StartsWith(x, StringComparison.InvariantCultureIgnoreCase)))
                return false;

            if (ExemptedPathsFromRequestAuthorization.Any(x => relativePathWithMethod.StartsWith(x, StringComparison.InvariantCultureIgnoreCase)))
                return false;

            return true;
        }

        public static bool DoesAuthTokenHasAccess(HttpContext httpContext = null, AuthToken authToken = default, OperationFilterContext operationFilterContext = null)
        {
            var relativePath = GetRelativePath(httpContext, operationFilterContext);

            if (!string.IsNullOrWhiteSpace(relativePath) && authToken?.ApiClaims != null && authToken.ApiClaims.Any())
            {
                return authToken.ApiClaims.Any(x => relativePath.StartsWith(x, StringComparison.InvariantCultureIgnoreCase) || relativePath.StartsWith($"/{x}", StringComparison.InvariantCultureIgnoreCase));
            }
            
            return false;
        }

        public static async Task ResponseUnauthorized(HttpContext context)
        {
            // Build Response text
            var responseText = new
            {
                Message = UnauthorizedMessage,
                Status = $"{(int)HttpStatusCode.Unauthorized} ({nameof(HttpStatusCode.Unauthorized)})"
            }.Json();

            // Set Response
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.Headers.Add(SetConstants.ContentTypeHeaderName,
                new[] { SetConstants.ContentTypeHeaderValueJson });

            // Write Response
            await context.Response.WriteAsync(responseText);
        }

        public static string GetRelativePath(HttpContext httpContext = null,
            OperationFilterContext operationFilterContext = null, bool? includeHttpMethod = null)
        {
            if (httpContext != null)
            {
                var relativePath = !httpContext.Request.Path.Value.StartsWith('/')
                    ? $"/{httpContext.Request.Path.Value}"
                    : httpContext.Request.Path.Value;

                return (includeHttpMethod ?? false)
                    ? $"{httpContext.Request.Method.ToUpper()}:{relativePath}"
                    : relativePath;
            }

            if (operationFilterContext != null)
            {
                var relativePath = !operationFilterContext.ApiDescription.RelativePath.StartsWith('/')
                    ? $"/{operationFilterContext.ApiDescription.RelativePath}"
                    : operationFilterContext.ApiDescription.RelativePath;

                return (includeHttpMethod ?? false)
                    ? $"{operationFilterContext.ApiDescription.HttpMethod.ToUpper()}:{relativePath}"
                    : relativePath;
            }

            return string.Empty;
        }
    }
}