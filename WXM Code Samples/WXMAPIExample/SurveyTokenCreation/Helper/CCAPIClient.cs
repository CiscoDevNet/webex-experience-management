using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace SurveyTokenCreation.Helper
{
    class CCAPIClient
    {
        string token; // WXM Access Token
        HttpClient httpClient; // Http Client
        HttpClientHandler httpConfig;
        HttpClient httpClientAPIStatus; // Http Client
        string _url = string.Empty; // WXM base URL
        string username = string.Empty; // WXM Username
        string pass = string.Empty; // WXM Password
        public DateTime? tokenvalid; // WXM Token validity
        string UserAgent = "CloudCherry NuGet";

        /// <summary>
        /// Initialize WXM API Client ( API Documentation at https://www.getcloudcherry.com/api/ )
        /// </summary>
        /// <param name="apiEndPoint">Endpoint for CloudCherry API; usually https://api.getcloudcherry.com</param>
        /// <param name="login">Valid CloudCherry Username</param>
        /// <param name="password">Valid CloudCherry Password</param>
        /// <param name="nativeHandler">Optional Faster Native Http Client For Mobile Use in Android & iOS</param>
        public CCAPIClient(string apiEndPoint, string login, string password, HttpClientHandler nativeHandler = null)
        {
            if (!string.IsNullOrEmpty(apiEndPoint))
                _url = apiEndPoint;

            username = login;
            pass = password;

            if (nativeHandler != null)
                httpClient = new HttpClient(nativeHandler);
            else
            {
                //.NET http client handler
                httpConfig = new HttpClientHandler();
                httpConfig.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;
                httpConfig.AllowAutoRedirect = true;
                httpConfig.UseCookies = false;
                httpClient = new HttpClient(httpConfig);
            }

            httpClient.Timeout = new TimeSpan(0, 0, 360); // 360 Seconds
            httpClient.DefaultRequestHeaders.AcceptEncoding.Add(System.Net.Http.Headers.StringWithQualityHeaderValue.Parse("deflate"));

            if (nativeHandler == null)
                httpClient.DefaultRequestHeaders.AcceptEncoding.Add(System.Net.Http.Headers.StringWithQualityHeaderValue.Parse("gzip"));

            httpClient.DefaultRequestHeaders.ExpectContinue = false;
            httpClient.DefaultRequestHeaders.AcceptLanguage.Add(System.Net.Http.Headers.StringWithQualityHeaderValue.Parse("en-gb"));

            //httpClient.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("*/*"));

            // Add an Accept header for JSON format.
            httpClient.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            //httpClient.MaxResponseContentBufferSize = 1024000;

            //Status Check Httpclient
            httpClientAPIStatus = new HttpClient();
            httpClientAPIStatus.Timeout = new TimeSpan(0, 0, 3); // 3 Seconds
            httpClientAPIStatus.DefaultRequestHeaders.Accept.Add(MediaTypeWithQualityHeaderValue.Parse("application/json"));
            httpClientAPIStatus.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            httpClientAPIStatus.MaxResponseContentBufferSize = 64000;

            //errorReport = new Stack<Exception>();
        }
        public async Task<String> SendAsync(String uri, string jsonBody = null, int count = 0)
        {
            bool isConnected = await Login();
            // Check if are logined in WXM or not.
            if (!isConnected)
                return null;

            string responseBodyAsText = null;
            HttpRequestMessage request;
            HttpResponseMessage response;
            try
            {
                if (!string.IsNullOrEmpty(jsonBody))
                {
                    request = new HttpRequestMessage(HttpMethod.Post, _url + uri);
                    request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
                }
                else
                    request = new HttpRequestMessage(HttpMethod.Get, uri);

                // Adding authentication token in request header.
                request.Headers.Add("Authorization", "Bearer " + token);

                response = await httpClient.SendAsync(request);

                if (response != null && response.IsSuccessStatusCode)
                    responseBodyAsText = await response.Content.ReadAsStringAsync();
                else if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    #pragma warning disable  CS4014
                    Logout();   
                    #pragma warning restore CS4014
                    //Retry Request Once
                    if (count < 1)
                        return await SendAsync(uri, jsonBody, count + 1);
                    else
                        return null;
                }
                else if (response != null && response.StatusCode == (HttpStatusCode)429)
                {
                    if (count < 1)
                    {
                        await Task.Delay(1000);
                        return await SendAsync(uri, jsonBody, count + 1);
                    }
                    else if (count < 6)
                    {
                        await Task.Delay(15000);
                        return await SendAsync(uri, jsonBody, count + 1);
                    }
                    else
                        return null;
                }
            }
            catch (HttpRequestException e)
            {
                return null;
            }
            catch (AggregateException aggEx)
            {
                //TaskCanceledException tcex = ex as TaskCanceledException;
                if (aggEx != null && aggEx.InnerException != null && aggEx.InnerException is TaskCanceledException)
                {
                    return null;
                }

            }
            catch (Exception ex)
            {
                return null;
            }

            // return null in case response is empty.
            if (responseBodyAsText == null || string.IsNullOrEmpty(responseBodyAsText))
                return null;

            return responseBodyAsText;
        }

        #region Login
        /// <summary>
        /// WXM Login API
        /// </summary>
        /// <returns>True if Successful</returns>
        public async Task<bool> Login()
        {
            try
            {
                //Already Logged In
                if (isConnected())
                    return true;

                string responseBodyAsText = null;
                HttpResponseMessage response;

                string uri = _url + "/api/logintoken";

                //Enable SSL
                if (_url.StartsWith("http://") && !_url.Contains("localhost"))
                    uri = _url.Replace("http://", "https://") + "/api/logintoken";

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, uri);
                var postvalues = new[] {
                    new KeyValuePair<string, string>("grant_type", "password"),
                    new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", pass)
                };
                request.Content = new FormUrlEncodedContent(postvalues);

                response = await httpClient.SendAsync(request);

                if (response != null && response.IsSuccessStatusCode)
                    responseBodyAsText = await response.Content.ReadAsStringAsync();
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (error.Contains("error_description"))
                    {
                        return false;
                    }
                }
                BearerToken logintoken = JsonConvert.DeserializeObject<BearerToken>(responseBodyAsText);


                if (logintoken != null && !string.IsNullOrEmpty(logintoken.access_token))
                {
                    token = logintoken.access_token;
                    tokenvalid = DateTime.Now.AddSeconds(logintoken.expires_in);

                    return true;
                }

                return false;

            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// Check token validity.
        /// </summary>
        /// <returns>True if it is valid.</returns>
        bool isConnected()
        {
            if (tokenvalid.HasValue && DateTime.Now.CompareTo(tokenvalid.Value) < 0)
                return true;
            else
                return false;

        }
        /// <summary>
        /// WXM Logout API
        /// </summary>
        /// <returns>True if Successful</returns>
        public async Task<bool> Logout()
        {
            //Already Logged In
            if (!isConnected())
                return true;

            try
            {
                string uri = "/api/account/logout";
                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                {
                    token = null;
                    tokenvalid = null;
                    return true;
                }

            }
            catch (Exception ex)
            {
                //Log exception.
            }

            token = null;
            tokenvalid = null;
            return false;

        }
        #endregion
    }

    /// <summary>
    /// WXM Access token details.
    /// </summary>
    class BearerToken
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
        public int expires_in { get; set; }
        public string userName { get; set; }
        public string email { get; set; }
        public string primaryRole { get; set; }
        public string managedBy { get; set; }
        public string station { get; set; }
    }
}
