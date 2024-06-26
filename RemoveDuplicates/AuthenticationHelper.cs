﻿//Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license.
//See LICENSE in the project root for license information.
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using System.Diagnostics;

namespace RemoveDuplicates;

class AuthenticationHelper
{
    // The Client ID is used by the application to uniquely identify itself to the v2.0 authentication endpoint.
    private const string ClientId = "d92f94c1-4d0d-4255-8bf0-7681eb4c168e";

    // The Group.Read.All permission is an admin-only scope, so authorization will fail if you 
    // want to sign in with a non-admin account. Remove that permission and comment out the group operations in 
    // the UserMode() method if you want to run this sample with a non-admin account.
    //public static string[] Scopes = { "https://outlook.office.com/calendars.readwrite" };
    public static string[] Scopes = ["calendars.readwrite"];

    public static IPublicClientApplication IdentityClientApp =
        PublicClientApplicationBuilder
            .Create(ClientId)
            .WithAuthority("https://login.microsoftonline.com/common/")
            .WithRedirectUri("http://localhost")
            .Build();
    public static string? UserToken = null;
    public static DateTimeOffset Expiration;

    private static GraphServiceClient? _graphClient = null;

    // Get an access token for the given context and resourceId. An attempt is first made to 
    // acquire the token silently. If that fails, then we try to acquire the token by prompting the user.
    public static async Task<GraphServiceClient?> GetAuthenticatedClientAsync()
    {
        // Create Microsoft Graph client.
        try
        {
            await TokenCacheHelper.AddCacheAsync(IdentityClientApp, ClientId);

            _graphClient = new GraphServiceClient(
                baseUrl: "https://graph.microsoft.com/v1.0",
                authenticationProvider: new BaseBearerTokenAuthenticationProvider(new CustomTokenProvider()));

            return _graphClient;
        }

        catch (Exception ex)
        {
            Debug.WriteLine("Could not create a graph client: " + ex.Message);
        }

        return _graphClient;
    }

    private class CustomTokenProvider : IAccessTokenProvider
    {
        public AllowedHostsValidator AllowedHostsValidator => throw new NotImplementedException();

        public async Task<string> GetAuthorizationTokenAsync(
            Uri uri,
            Dictionary<string, object>? additionalAuthenticationContext = null,
            CancellationToken cancellationToken = default)
        {
            return await GetTokenForUserAsync();
        }
    }

    /// <summary>
    /// Get Token for User.
    /// </summary>
    /// <returns>Token for user.</returns>
    public static async Task<string> GetTokenForUserAsync()
    {
        AuthenticationResult authResult;
        try
        {
            var accounts = await IdentityClientApp.GetAccountsAsync();
            authResult = await IdentityClientApp
                .AcquireTokenSilent(Scopes, accounts.FirstOrDefault())
                .ExecuteAsync();
            UserToken = authResult.AccessToken;
        }
        catch (MsalUiRequiredException ex)
        {
            authResult = await IdentityClientApp
                .AcquireTokenInteractive(Scopes)
                .WithClaims(ex.Claims)
                .ExecuteAsync();
            UserToken = authResult.AccessToken;
            Expiration = authResult.ExpiresOn;
        }

        return UserToken;
    }
}