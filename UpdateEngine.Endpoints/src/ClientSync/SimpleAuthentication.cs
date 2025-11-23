// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

namespace UpdateEngine.Metadata.Endpoints.ClientSync;

using Microsoft.UpdateServices.WebServices.ClientAuthentication;
using System.Threading.Tasks;

/// <summary>
/// Authentication service implementation. Reference implementation: all requests are authenticated and a token issued.
/// </summary>
public class SimpleAuthenticationWebService : ISimpleAuthenticationWebService
{
    /// <summary>
    /// Handle requests for a authorization token. This implementation issues tokens for all requests without performing any checks.
    /// </summary>
    /// <param name="request">The request paramerets. Not used in this implementation.</param>
    /// <returns>Authorization cookie</returns>
    public Task<AuthorizationCookie> GetAuthorizationCookieAsync(GetAuthorizationCookieRequest request)
    {
        return Task.FromResult(new AuthorizationCookie() { CookieData = new byte[5], PlugInId = "15" });
    }
}
