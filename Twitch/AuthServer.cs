﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TwitchBot.Twitch
{
    internal class Authorization
    {
        public string Code { get; }

        public Authorization(string code)
        {
            Code = code;
        }
    }
    internal class AuthServer
    {
        private HttpListener listener;

        public AuthServer(string uri)
        {
            listener = new HttpListener();
            listener.Prefixes.Add(uri);
        }

        public async Task<Authorization> Listen()
        {
            listener.Start();
            return await onRequest();
        }

        private async Task<Authorization> onRequest()
        {
            while (listener.IsListening)
            {
                var ctx = await listener.GetContextAsync();
                var req = ctx.Request;
                var resp = ctx.Response;

                using (var writer = new StreamWriter(resp.OutputStream))
                {
                    if (req.QueryString.AllKeys.Any("code".Contains))
                    {
                        writer.WriteLine("Authorization started! Check your application!");
                        writer.Flush();
                        return new Authorization(req.QueryString["code"]);
                    }
                    else
                    {
                        writer.WriteLine("No code found in query string!");
                        writer.Flush();
                    }
                }
            }
            return null;
        }

        public static string getAuthorizationCodeUrl(string clientId, string redirectUri, List<string> scopes)
        {
            var scopesStr = String.Join('+', scopes);

            return "https://id.twitch.tv/oauth2/authorize?" +
                   $"client_id={clientId}&" +
                   $"redirect_uri={System.Web.HttpUtility.UrlEncode(redirectUri)}&" +
                   "response_type=code&" +
                   $"scope={scopesStr}";
        }
    }
}
