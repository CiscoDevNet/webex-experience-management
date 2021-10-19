using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace CreateResponseExcel.Helper
{
    public class Authentication
    {
        internal static async Task<string> AccessLoginToken(string username, string password)
        {
            try
            {
                string apiendpoint = "https://api.getcloudcherry.com";
                // Create a Instance of Async HttpClient(System.Net.Http)
                var client = new HttpClient();
                HttpRequestMessage authrequest = new HttpRequestMessage(HttpMethod.Post, apiendpoint + "/api/LoginToken");
                var postvalues = new[] {
                    new KeyValuePair<string, string> ("grant_type", "password"),
                    new KeyValuePair<string, string> ("username", username),
                    new KeyValuePair<string, string> ("password", password)
                };
                authrequest.Content = new FormUrlEncodedContent(postvalues);
                HttpResponseMessage authresponse = await client.SendAsync(authrequest);
                string responseBodyAsText = await authresponse.Content.ReadAsStringAsync();
                var tokenStructure = new { access_token = string.Empty, expires_in = 0 };
                var token = JsonConvert.DeserializeAnonymousType(responseBodyAsText, tokenStructure);
                //Console.WriteLine("Authenticated : " + !string.IsNullOrEmpty(token.access_token));
                Logger.Info("access token successfully received.");
                return token.access_token;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}