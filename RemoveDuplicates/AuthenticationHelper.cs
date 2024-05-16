//Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license.
//See LICENSE in the project root for license information.
using Microsoft.Graph;
using Microsoft.Identity.Client;
using Microsoft.Kiota.Abstractions.Authentication;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace console_csharp_connect_sample
{
    class AuthenticationHelper
    {
        // The Client ID is used by the application to uniquely identify itself to the v2.0 authentication endpoint.
        private const string ClientId = "d92f94c1-4d0d-4255-8bf0-7681eb4c168e";

        // The Group.Read.All permission is an admin-only scope, so authorization will fail if you 
        // want to sign in with a non-admin account. Remove that permission and comment out the group operations in 
        // the UserMode() method if you want to run this sample with a non-admin account.
        //public static string[] Scopes = { "https://outlook.office.com/calendars.readwrite" };
        public static string[] Scopes = ["calendars.readwrite"];

        public static IPublicClientApplication IdentityClientApp = PublicClientApplicationBuilder
            .Create(ClientId)
            .WithCacheOptions(new CacheOptions(useSharedCache: true))
            .Build();
        public static string UserToken = null;
        public static DateTimeOffset Expiration;

        private static GraphServiceClient _graphClient = null;

        // Get an access token for the given context and resourceId. An attempt is first made to 
        // acquire the token silently. If that fails, then we try to acquire the token by prompting the user.
        public static GraphServiceClient GetAuthenticatedClient()
        {
            // Create Microsoft Graph client.
            try
            {
                _graphClient = new GraphServiceClient(
                    //baseUrl: "https://graph.microsoft.com/v1.0",
                    //baseUrl: "https://outlook.office.com/api/v2.0/",
                    //authenticationProvider: new DelegateAuthenticationProvider(
                    //async (requestMessage) =>
                    //{
                    //    var token = await GetTokenForUserAsync();
                    //    requestMessage.Headers.Authorization = new AuthenticationHeaderValue("bearer", token);
                    //}
                    authenticationProvider: new BaseBearerTokenAuthenticationProvider(new DelegateAuthenticationProvider())
                );
                return _graphClient;
            }

            catch (Exception ex)
            {
                Debug.WriteLine("Could not create a graph client: " + ex.Message);
            }

            return _graphClient;
        }

        internal class DelegateAuthenticationProvider : IAccessTokenProvider
        {
            public AllowedHostsValidator AllowedHostsValidator => throw new NotImplementedException();

            //public async Task AuthenticateRequestAsync(
            //    RequestInformation request,
            //    Dictionary<string, object> additionalAuthenticationContext = null,
            //    CancellationToken cancellationToken = default)
            //{
            //    request.Headers.Add("Authorization", "Bearer: " + await GetTokenForUserAsync());
            //}

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
                    authResult = await IdentityClientApp.AcquireTokenSilent(Scopes, accounts.First()).ExecuteAsync();
                    UserToken = authResult.AccessToken;
                }

                catch (Exception)
                {
                    if (UserToken == null || Expiration <= DateTimeOffset.UtcNow.AddMinutes(5))
                    {
                        authResult = await IdentityClientApp.AcquireTokenInteractive(Scopes).ExecuteAsync();

                        UserToken = authResult.AccessToken;
                        Expiration = authResult.ExpiresOn;
                    }
                }

                return UserToken;
            }

            public Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
            {
                return GetTokenForUserAsync();
            }
        }
    }
}