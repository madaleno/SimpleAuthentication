﻿using System;
using WorldDomination.Web.Authentication;

namespace Nancy.Authentication.WorldDomination
{
    public class WorldDominationAuthenticationModule : NancyModule
    {
        private const string StateKey = "WorldDomination-StateKey-cf92a651-d638-4ce4-a393-f612d3be4c3a";
        public static string RedirectRoute = "/authentication/redirect/{providerkey}";
        public static string CallbackRoute = "/authentication/authenticatecallback";

        public WorldDominationAuthenticationModule(IAuthenticationService authenticationService)
            : this(authenticationService, null)
        {
            throw new ApplicationException(
                "World Domination requires you implement your own IAuthenticationCallbackProvider.");
        }

        public WorldDominationAuthenticationModule(IAuthenticationService authenticationService,
                                                   IAuthenticationCallbackProvider authenticationCallbackProvider)
        {
            Get[RedirectRoute] = _ =>
            {
                var providerKey = (string)_.providerkey;
                if (string.IsNullOrEmpty(providerKey))
                {
                    throw new ArgumentException(
                        "You need to supply a valid provider key so we know where to redirect the user.");
                }
                
                // Kthxgo!
                return RedirectToAuthenticationProvider(authenticationService, providerKey);
            };

            Post[RedirectRoute] = _ =>
            {
                var providerKey = (string)_.providerkey;
                if (string.IsNullOrEmpty(providerKey))
                {
                    throw new ArgumentException(
                        "You need to supply a valid provider key so we know where to redirect the user.");
                }

                Uri identifier = null;
                if (string.IsNullOrEmpty(Request.Form.Identifier) ||
                    !Uri.TryCreate(Request.Form.Identifier, UriKind.RelativeOrAbsolute, out identifier))
                {
                    throw new ArgumentException(
                        "You need to POST the identifier to redirect the user. Eg. http://myopenid.com");
                }

                return RedirectToAuthenticationProvider(authenticationService, providerKey, identifier);
            };

            Get[CallbackRoute] = _ =>
            {
                var providerKey = Request != null && Request.Query != null
                                    ? (string)Request.Query.providerkey
                                    : null;

                if (string.IsNullOrEmpty(providerKey))
                {
                    throw new ArgumentException("No provider key was supplied on the callback.");
                }

                var settings = authenticationService.GetAuthenticateServiceSettings(providerKey, Request.Url);

                settings.State = (Session[StateKey] as string) ?? string.Empty;

                var model = new AuthenticateCallbackData();

                try
                {
                    model.AuthenticatedClient = authenticationService.GetAuthenticatedClient(settings, Request.Query);
                    Session.Delete(StateKey); // Clean up :)
                }
                catch (Exception exception)
                {
                    model.Exception = exception;
                }

                return authenticationCallbackProvider.Process(this, model);
            };
        }

        private Response RedirectToAuthenticationProvider(IAuthenticationService authenticationService,
            string providerKey, Uri identifier = null)
        {
            if (authenticationService == null)
            {
                throw new ArgumentNullException();
            }

            if (string.IsNullOrEmpty(providerKey))
            {
                throw new ArgumentNullException("providerKey");
            }

            // Grab the required Provider settings.

            var settings = authenticationService.GetAuthenticateServiceSettings(providerKey, Request.Url);

            // An OpenId specific settings provided?
            if (identifier != null && 
                settings is IOpenIdAuthenticationServiceSettings)
            {
                ((IOpenIdAuthenticationServiceSettings) settings).Identifier = identifier;
            }

            // Remember the State value (for CSRF protection).
            Session[StateKey] = settings.State;

            // Determine the provider's end point Url we need to redirect to.
            var uri = authenticationService.RedirectToAuthenticationProvider(settings);

            // Kthxgo!
            return Response.AsRedirect(uri.AbsoluteUri);
        }
    }
}