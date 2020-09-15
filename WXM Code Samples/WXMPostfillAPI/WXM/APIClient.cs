using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CloudCherry
{
    public class APIClient
    {

        #region Preset
        string UserAgent = "CloudCherry NuGet";
        string _url = null;
        //string _url = "http://localhost:53312";
        public event EventHandler<MyEventArgs> OnCloudCherryExceptionHandler;
        #endregion

        #region Auth
        public string username;
        string pass;
        public string token;
        public string email;
        public string role;
        public string managedBy;
        public string station;
        public DateTime? tokenvalid;

        #endregion

        HttpClient httpClient;
        HttpClientHandler httpConfig;

        HttpClient httpClientAPIStatus;

        public string errorLogin;
        public Stack<string> errorMessage;
        public Stack<Exception> errorReport;

        /// <summary>
        /// Initialize CloudCherry API Client ( API Documentation at https://www.getcloudcherry.com/api/ )
        /// </summary>
        /// <param name="apiEndPoint">Endpoint for CloudCherry API; usually https://api.getcloudcherry.com</param>
        /// <param name="login">Valid CloudCherry Username</param>
        /// <param name="password">Valid CloudCherry Password</param>
        /// <param name="nativeHandler">Optional Faster Native Http Client For Mobile Use in Android & iOS</param>
        public APIClient(string apiEndPoint, string login, string password, HttpClientHandler nativeHandler = null)
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

            errorReport = new Stack<Exception>();
        }
        async Task<String> SendAsync(String uri, string jsonBody = null, int count = 0)
        {
            bool isConnected = await Login();

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
                    request = new HttpRequestMessage(HttpMethod.Get, _url + uri);

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
                if (e != null)
                    Trace(e.Message);

                return null;
            }
            catch (AggregateException aggEx)
            {
                //TaskCanceledException tcex = ex as TaskCanceledException;
                if (aggEx != null && aggEx.InnerException != null && aggEx.InnerException is TaskCanceledException)
                {
                    Trace("Request Timed Out: " + uri);
                    return null;
                }

            }
            catch (Exception ex)
            {
                if (ex.Message != null && !ex.Message.Contains("task was canceled"))
                    Trace(ex);
                else
                    Trace("Request Timed Out: " + uri);

                return null;
            }

            if (responseBodyAsText == null || string.IsNullOrEmpty(responseBodyAsText))
                return null;

            return responseBodyAsText;
        }
        async Task<String> SendStateLessAsync(String uri, string jsonBody = null, int count = 0)
        {
            //bool isConnected = await Login();

            //if (!isConnected)
            //    return null;

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
                    request = new HttpRequestMessage(HttpMethod.Get, _url + uri);

                //request.Headers.Add("Authorization", "Bearer " + token);

                response = await httpClient.SendAsync(request);

                if (response != null && response.IsSuccessStatusCode)
                    responseBodyAsText = await response.Content.ReadAsStringAsync();
                else if (response != null && response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    //Logout();

                    //Retry Request Once
                    //if (count < 1)
                    //    return await SendAsync(uri, jsonBody, count + 1);
                    //else
                    return null;
                }
            }
            catch (HttpRequestException e)
            {
                if (e != null)
                    Trace(e.Message);

                return null;
            }
            catch (AggregateException aggEx)
            {
                //TaskCanceledException tcex = ex as TaskCanceledException;
                if (aggEx != null && aggEx.InnerException != null && aggEx.InnerException is TaskCanceledException)
                {
                    Trace("Request Timed Out: " + uri);
                    return null;
                }

            }
            catch (Exception ex)
            {
                if (ex.Message != null && !ex.Message.Contains("task was canceled"))
                    Trace(ex);
                else
                    Trace("Request Timed Out: " + uri);

                return null;
            }

            if (responseBodyAsText == null || string.IsNullOrEmpty(responseBodyAsText))
                return null;

            return responseBodyAsText;
        }

        #region Login
        /// <summary>
        /// Login to API
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
                        var e = JsonConvert.DeserializeObject<LoginError>(error);
                        errorLogin = e.error_description;
                        return false;
                    }
                }
                BearerToken logintoken = JsonConvert.DeserializeObject<BearerToken>(responseBodyAsText);


                if (logintoken != null && !string.IsNullOrEmpty(logintoken.access_token))
                {
                    token = logintoken.access_token;
                    tokenvalid = DateTime.Now.AddSeconds(logintoken.expires_in);
                    email = logintoken.email;
                    role = logintoken.primaryRole;
                    managedBy = logintoken.managedBy;
                    station = logintoken.station;

                    return true;
                }

                return false;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }
        }
        bool isConnected()
        {
            if (tokenvalid.HasValue && DateTime.Now.CompareTo(tokenvalid.Value) < 0)
                return true;
            else
                return false;

        }
        /// <summary>
        /// Logout of API
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
                Trace(ex);
            }

            token = null;
            tokenvalid = null;
            return false;

        }
        #endregion

        #region Questions
        /// <summary>
        /// Get List of All Questions For User
        /// </summary>
        /// <returns>List of Questions</returns>
        /// <param name="active">Only Current Generation Questions Presented</param>
        public async Task<List<Question>> GetQuestions(bool active = true)
        {
            try
            {
                string uri = "/api/questions/";

                if (active)
                    uri += "active/"; // get active questions

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<Question>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        /// <summary>
        /// Create a new Question
        /// </summary>
        /// <returns>Complete question with API assigned unique question id for further reference</returns>
        /// <param name="q">Question to be created</param>
        public async Task<Question> AddQuestion(Question q)
        {
            try
            {
                string uri = "/api/questions/add";
                string json = JsonConvert.SerializeObject(q);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<Question>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        /// <summary>
        /// Delete a exisitng question, Answers to this Question will be lost, Consider Retiring by Question Update Instead
        /// </summary>
        /// <param name="id">unqiue question id</param>
        /// <returns>True when successful</returns>
        public async Task<bool> DeleteQuestion(string id)
        {
            try
            {
                string uri = "/api/questions/delete/" + id;

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText) && responseBodyAsText == "true")
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }
        }
        /// <summary>
        /// Update a existing question
        /// </summary>
        /// <param name="q">Updated question</param>
        /// <returns>Updated question for reference</returns>
        public async Task<Question> UpdateQuestion(Question q)
        {
            if (string.IsNullOrEmpty(q.Id))
                return null;

            try
            {
                string uri = "/api/questions/update/" + q.Id;
                string json = JsonConvert.SerializeObject(q);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<Question>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }


        public async Task<List<bool>> BatchQuestionUpdate(List<Question> questions)
        {
            if (questions == null)
                return null;

            try
            {
                string uri = "/api/questions/BatchUpdate";
                string json = JsonConvert.SerializeObject(questions);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<bool>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        public async Task<List<string>> RenameQuestionOption(string id, Rename values)
        {
            if (string.IsNullOrEmpty(id) || values == null)
                return null;

            try
            {
                string uri = $"/api/QuestionOption/Rename/{id}";
                string json = JsonConvert.SerializeObject(values);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<string>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        #endregion

        #region Settings
        /// <summary>
        /// Setting to Provision Theme, Colors & Logo, Add Locations, Add Notifications & Set Other Preferences
        /// </summary>
        /// <returns></returns>
        public async Task<UserSettings> GetSettings()
        {
            try
            {
                string uri = "/api/settings";
                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<UserSettings>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        /// <summary>
        /// Update Settings
        /// </summary>
        /// <returns>Updated Settings</returns>
        public async Task<UserSettings> UpdateSettings(UserSettings q)
        {
            try
            {
                string uri = "/api/settings/update";
                string json = JsonConvert.SerializeObject(q);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<UserSettings>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        #endregion

        #region Answers
        /// <summary>
        /// Get All Responses Submitted
        /// </summary>
        /// <returns>List of Complete Responses with Answers</returns>
        /// <param name="filter">Filter for Location, Date or by Response</param>
        public async Task<List<Answer>> GetAnswers(FilterBy filter)
        {
            try
            {
                string filterJSON = null;

                if (filter != null)
                    filterJSON = JsonConvert.SerializeObject(filter);

                string uri = "/api/answers";

                string responseBodyAsText = await SendAsync(uri, filterJSON);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<Answer>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        public async Task<List<string>> GetSummaryResponseById(string questionId, FilterBy filter)
        {
            try
            {
                string uri = "/api/summary/responsebyquestion/" + questionId;
                string json = JsonConvert.SerializeObject(filter);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<string>>(responseBodyAsText);
                else
                    return null;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Get a Answer By responseId
        /// </summary>
        /// <returnsSingle Answer</returns>
        /// <param name="responseId">Id of the response</param>
        public async Task<Answer> GetAnswer(string responseId)
        {
            try
            {
                string uri = "/api/answer/" + responseId;

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<Answer>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Get a Answer By responseId
        /// </summary>
        /// <returnsSingle Answer</returns>
        /// <param name="responseId">Id of the response</param>
        public async Task<int> GetAnswerCount(FilterBy filter)
        {
            try
            {
                string filterJSON = null;

                if (filter != null)
                    filterJSON = JsonConvert.SerializeObject(filter);

                string uri = "/api/answers/count";

                string responseBodyAsText = await SendAsync(uri, filterJSON);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return int.Parse(responseBodyAsText);
                else
                    return -1;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return -1;
            }
        }

        /// <summary>
        /// Get All Responses Submitted
        /// </summary>
        /// <returns>List of Complete Responses with Answers PageWise</returns>
        /// <param name="filter">Filter for Location, Date or by Response</param>
        /// <param name="page">Page No</param>
        public async Task<PageWiseAnswer> GetPageWiseAnswers(FilterBy filter, int page = 0, int rows = 25)
        {
            try
            {
                string filterJSON = null;

                if (filter != null)
                    filterJSON = JsonConvert.SerializeObject(filter);

                string uri = $"/api/answers/page/{page}/{rows}";

                string responseBodyAsText = await SendAsync(uri, filterJSON);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<PageWiseAnswer>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Get Summary of open ended questions
        /// </summary>
        /// <returns>List of Complete Responses with Answers KeyWise</returns>
        /// <param name="filter">Filter for Location, Date or by Response</param>
        /// <param name="questionId">QuestionID</param>
        /// <param name="questionId">Number of reponses</param>
        /// <param name="page">Page No</param>
        public async Task<List<SummaryKeyValue>> GetKeyWiseAnswers(FilterBy filter, string questionId, int noOfResponses = 14, int page = 0)
        {
            try
            {
                string filterJSON = null;

                if (filter != null)
                    filterJSON = JsonConvert.SerializeObject(filter);

                string uri = "/api/answers/keys/" + questionId + "/true/" + noOfResponses + "/" + page;

                string responseBodyAsText = await SendAsync(uri, filterJSON);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<SummaryKeyValue>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Submit New Response with Answers Set
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public async Task<Answer> PostAnswer(Answer a)
        {
            try
            {
                string uri = "/api/answers/add";
                string json = JsonConvert.SerializeObject(a);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<Answer>(responseBodyAsText);
                else
                    return null;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Update Many Responses
        /// </summary>
        /// <param name="multipleAnswers"></param>
        /// <returns></returns>
        public async Task<bool> UpdateMultiAnswer(Dictionary<string, Answer> multipleAnswers)
        {
            try
            {
                string uri = "/api/answers/update";
                string json = JsonConvert.SerializeObject(multipleAnswers);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText) && responseBodyAsText == "true")
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }
        }

        /// <summary>
        /// Save Persistent UserData
        /// </summary>
        /// <param name="Key">Key To Be Saved Against</param>
        /// <param name="value">Persistent Value To Be Saved</param>
        /// <returns></returns>
        public async Task<bool> SaveUserData(string Key, UserData value)
        {
            try
            {
                string uri = $"/api/UserData/{Key}";

                string json = JsonConvert.SerializeObject(value);

                string responseBodyAsText = await SendAsync(uri, json);
                return true;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }
        }

        /// <summary>
        /// Get Persistent UserData
        /// </summary>
        /// <param name="Key">Key, for which value should be fetched</param>
        /// <returns></returns>
        public async Task<string> GetUserData(string Key)
        {
            try
            {
                string uri = $"/api/UserData/{Key}";

                string responseBodyAsText = await SendAsync(uri);
                return responseBodyAsText;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        #endregion

        #region Analytics & Insight
        /// <summary>
        /// Analytics Summary For Collected Responses
        /// </summary>
        /// <param name="filter">Filter for location, date or by answer to a specific question</param>
        /// <returns>Sorted Answers For Each Question</returns>
        public async Task<ResponseSummary> GetSummary(FilterBy filter)
        {
            try
            {
                string uri = "/api/summary";


                string filterJSON = null;

                if (filter != null)
                    filterJSON = JsonConvert.SerializeObject(filter);

                string responseBodyAsText = await SendAsync(uri, filterJSON);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<ResponseSummary>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        /// <summary>
        /// Analytics for multi-locations with liked/disliked, delight score, nps & more
        /// </summary>
        /// <param name="filter">Filter for location, date or by answer to a specific question</param>
        /// <returns></returns>
        public async Task<Dictionary<String, AnalyticsSummary>> GetAnalyticsByLocation(FilterBy filter)
        {
            try
            {
                string uri = "/api/analyticsbylocation/1";


                string filterJSON = null;

                if (filter != null)
                    filterJSON = JsonConvert.SerializeObject(filter);

                string responseBodyAsText = await SendAsync(uri, filterJSON);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<Dictionary<String, AnalyticsSummary>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Intersect two questions to obtain a two dimensional matrix of responses received
        /// </summary>
        /// <param name="filter">Filter for location, date or by answer to a specific question</param>
        /// <param name="questionid1">First Question ID</param>
        /// <param name="questionid2">Second Question ID</param>
        /// <returns></returns>
        public async Task<Dictionary<String, SummaryLite>> IntersectQuestions(FilterBy filter, string questionid1, string questionid2)
        {
            try
            {
                string uri = "/api/answers/compare/" + questionid1 + "/" + questionid2;

                string filterJSON = null;

                if (filter != null)
                    filterJSON = JsonConvert.SerializeObject(filter);

                string responseBodyAsText = await SendAsync(uri, filterJSON);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<Dictionary<String, SummaryLite>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        #endregion

        #region Utils
        /// <summary>
        /// Async Download Images Or Other Supporting Content as Byte Array
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<byte[]> DownloadContent(string url)
        { // Download Content W/ Retry
            try
            {

                //Retry thrice
                int retrycount = 1;
                while (retrycount < 5)
                { // Retry Thrice
                    HttpResponseMessage response = null;
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                    try
                    {
                        response = await httpClient.SendAsync(request);
                    }
                    catch (Exception ex)
                    {
                        Trace(ex);
                    }

                    if (response != null && response.IsSuccessStatusCode)
                    {
                        byte[] content = await response.Content.ReadAsByteArrayAsync();

                        if (content.Length != 0)
                            return content;
                    }
                    else
                        retrycount++;
                }

                //if (response != null && response.IsSuccessStatusCode)
                //    return await response.Content.ReadAsByteArrayAsync();
                //else
                return null;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        /// <summary>
        /// Check Status of API Connection
        /// </summary>
        /// <returns>True when API endpoint is reachable</returns>
        public async Task<bool> CheckAPIStatus()
        {
            try
            {
                CancellationTokenSource cts = new CancellationTokenSource(3200);
                HttpResponseMessage response;
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, _url + "/api/status");
                response = await httpClientAPIStatus.SendAsync(request, cts.Token);

                if (response != null && response.IsSuccessStatusCode)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }

        }

        /// <summary>
        /// Get Demo Account to Show Case
        /// </summary>
        /// <returns></returns>
        public async Task<List<DemoShowCaseAccount>> GetDemoAccounts()
        {
            try
            {
                HttpResponseMessage response;
                string uri = "/api/demoaccounts";
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, _url + uri);
                response = await httpClient.SendAsync(request);

                if (response != null && response.IsSuccessStatusCode)
                {
                    string responseBodyAsText = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<DemoShowCaseAccount>>(responseBodyAsText);
                }
                else
                    return null;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        void Trace(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                string text = DateTime.Now.ToString("HH:mm:ss") + " " + message;
                errorMessage.Push(text);
            }
        }
        void Trace(Exception ex)
        {
            OnCloudCherryException(new MyEventArgs(ex));
            errorReport.Push(ex);
        }

        protected virtual void OnCloudCherryException(MyEventArgs e)
        {
            OnCloudCherryExceptionHandler?.Invoke(this, e);
        }
        #endregion

        #region SurveyToken
        /// <summary>
        /// Get List of Multi Channel Survey Token Ids
        /// </summary>
        /// <param name="filterByLocation">Optional Filter with Location Name</param>
        /// <param name="filterByNote">Optional Filter with Token Note</param>
        /// <returns>List of ID of Survey Tokens</returns>
        public async Task<List<string>> GetSurveyTokenIds(string filterByLocation = null, string filterByNote = null)
        {
            try
            {
                string uri = "/api/surveytoken/searchid";

                if (!string.IsNullOrEmpty(filterByLocation))
                {
                    uri += "/" + filterByLocation; // By Location

                    if (!string.IsNullOrEmpty(filterByNote))
                        uri += "/" + filterByNote; // With Note
                }
                else if (!string.IsNullOrEmpty(filterByNote))
                    uri += "/all/" + filterByNote; // Only Note

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<string>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Get List of Multi Channel Survey Tokens
        /// </summary>
        /// <param name="filterByLocation">Optional Filter with Location Name</param>
        /// <param name="filterByNote">Optional Filter with Token Note</param>
        /// <returns>List of Survey Tokens</returns>
        public async Task<List<SurveyToken>> GetSurveyTokens(string filterByLocation = null, string filterByNote = null)
        {
            try
            {
                string uri = "/api/surveytoken";

                if (!string.IsNullOrEmpty(filterByLocation))
                {
                    uri += "/search/" + filterByLocation; // By Location

                    if (!string.IsNullOrEmpty(filterByNote))
                        uri += "/" + filterByNote; // With Note
                }
                else if (!string.IsNullOrEmpty(filterByNote))
                    uri += "/all/" + filterByNote; // Only Note

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<SurveyToken>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Get List of Multi Channel Survey Tokens By Page
        /// </summary>
        /// <param name="page">Page No.</param>
        /// <returns>List of Survey Tokens</returns>
        public async Task<PageWiseSurveyTokens> GetPageWiseSurveyTokens(int page = 0, string location = null, string note = null)
        {
            try
            {
                string uri = $"/api/surveytoken/page/{page}";

                if (string.IsNullOrEmpty(location))
                    uri = $"{uri}/all";

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<PageWiseSurveyTokens>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Create a new Survey Token
        /// </summary>
        /// <param name="token">API Created Token with unique ID</param>
        /// <returns>Token with ID</returns>
        public async Task<SurveyToken> AddSurveyToken(SurveyToken token)
        {
            try
            {
                string uri = "/api/surveytoken";
                string json = JsonConvert.SerializeObject(token);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<SurveyToken>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        /// <summary>
        /// Delete a Existing Survey Token
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<bool> DeleteSurveyToken(string id)
        {
            try
            {
                string uri = "/api/surveytoken/delete/" + id;

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText) && responseBodyAsText == "true")
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }
        }
        /// <summary>
        /// Bulk Delete Survey Tokens
        /// </summary>
        /// <param name="idlist">List of Token ID's</param>
        /// <returns></returns>
        public async Task<bool> DeleteSurveyTokens(List<string> idlist)
        {
            try
            {
                string uri = "/api/surveytoken/delete/";
                string json = JsonConvert.SerializeObject(idlist);
                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText) && responseBodyAsText == "true")
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }
        }
        /// <summary>
        /// Update a existing Survey Token
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<SurveyToken> UpdateSurveyToken(SurveyToken token)
        {
            if (string.IsNullOrEmpty(token.Id))
                return null;

            try
            {
                string uri = "/api/surveytoken/update/" + token.Id;
                string json = JsonConvert.SerializeObject(token);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<SurveyToken>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Get SurveyToken By Id
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<SurveyToken> GetSurveyToken(string token)
        {
            try
            {
                string uri = "/api/SurveyToken/Id/" + token;

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<SurveyToken>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Get Survey using Multi Channel Token
        /// </summary>
        /// <param name="token">Unique Token ID(ex: DM-23456)</param>
        /// <returns></returns>
        public async Task<TokenSurvey> GetTokenSurvey(string token)
        {
            try
            {
                string uri = "/api/SurveyByToken/" + token;

                string responseBodyAsText = await SendStateLessAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<TokenSurvey>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        /// <summary>
        /// Post a Response Collected for Multi Channel Survey Token
        /// </summary>
        /// <param name="token">Token ID</param>
        /// <param name="a">Complete Response</param>
        /// <returns>Response with unqiue ID</returns>
        public async Task<Answer> PostSurveyByTokenResponse(string token, Answer a)
        {
            try
            {
                string uri = "/api/surveybytoken/" + token;
                string json = JsonConvert.SerializeObject(a);

                string responseBodyAsText = await SendStateLessAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<Answer>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        /// <summary>
        /// Download QR Token 200x200 PNG Image to Print as Byte Array
        /// </summary>
        /// <param name="token">Unique Token ID(ex: DM-23456)</param>
        /// <returns></returns>
        public async Task<byte[]> DownloadQRToken(string token)
        { // Download QR Image for Token
            return await DownloadContent(_url + "/api/SurveyByToken/QR/" + token);
        }
        /// <summary>
        /// Create bulk one-time use Survey Tokens Pre-filled using sample format from dashboard
        /// </summary>
        /// <param name="csvdata">Modified sample csv data as string</param>
        /// <param name="days">Validity Days</param>
        /// <param name="uses">Limit on Uses(1=OneTime)</param>
        /// <param name="location">Set to Location(Optional)</param>
        /// <returns></returns>
        public async Task<string> UploadBulkTokens(string csvdata, int days = 30, int uses = 1, string location = null)
        {
            if (string.IsNullOrEmpty(token))
                return null; // Need to be authenticated

            try
            {
                string uri = "/api/SurveyByToken/Import/" + days + "/" + uses + "/" + location;

                var fileContent = new StringContent(csvdata);
                string filename = "csvtokens.csv";
                fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "\"files\"",
                    FileName = "\"" + filename + "\""
                }; // the extra quotes are key here
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

                var content = new MultipartFormDataContent();
                content.Add(fileContent);

                HttpResponseMessage response = null;
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _url + uri);
                request.Content = content;
                request.Headers.Add("Authorization", "Bearer " + token);

                response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string apifile = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(apifile) && apifile.Contains("download/"))
                    { // Return Filled CSV
                        var bytestr = await DownloadContent(_url + "/api/" + apifile.Replace("\"", ""));
                        return Encoding.UTF8.GetString(bytestr, 0, bytestr.Length);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        #endregion

        #region Subuser
        /// <summary>
        /// Get ListOfSubusers
        /// </summary>
        /// <returns></returns>
        public async Task<List<SubUser>> GetSubusers()
        {
            try
            {
                string uri = "/api/account/getsubusers";

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<SubUser>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Get ListOfSubusers By Page
        /// </summary>
        /// <returns></returns>
        public async Task<PageSubUser> GetPageWiseSubusers(int page = 1, string search = null)
        {
            try
            {
                string uri = $"/api/account/getsubusers/page/{page}/{search}";

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<PageSubUser>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Create Subuser
        /// </summary>
        /// <returns></returns>
        public async Task<string> RegisterSubuser(IdentityUser identityUser)
        {
            try
            {
                string uri = "/api/account/register";
                string json = JsonConvert.SerializeObject(identityUser);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return responseBodyAsText;
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        /// <summary>
        /// Modify Subuser
        /// </summary>
        /// <returns></returns>
        public async Task<SubUser> ModifySubuser(SubUser subUser)
        {
            try
            {
                string uri = "/api/account/modifysubuser";
                string json = JsonConvert.SerializeObject(subUser);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<SubUser>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        #endregion

        #region User Preferences
        /// <summary>
        /// Get User's Preference For Views
        /// </summary>
        /// <param name="subuser">Optional Management Of Sub User</param>
        /// <returns></returns>
        public async Task<UserPreference> GetUserPreference(string subuser = null)
        {
            try
            {
                string uri = "/api/Preference";

                if (!string.IsNullOrEmpty(subuser))
                    uri += "/" + subuser;

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<UserPreference>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Update User's Preference for Views
        /// </summary>
        /// <param name="pref">Updated Preferences</param>
        /// <param name="subuser">Optional Management Of Sub User</param>
        /// <returns></returns>
        public async Task<UserPreference> UpdateUserPreference(UserPreference pref, string subuser = null)
        {
            try
            {
                string uri = "/api/Preference";
                if (!string.IsNullOrEmpty(subuser))
                    uri += "/" + subuser;

                string json = JsonConvert.SerializeObject(pref);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<UserPreference>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        #endregion

        #region LoggedNotifications
        /// <summary>
        /// Get Logged Notifications
        /// </summary>
        /// <param name="filter">Filter</param>
        /// <returns>List of Logged Notifications</returns>
        public async Task<List<LoggedNotification>> GetLoggedNotifications(FilterBy filter)
        {
            try
            {
                string uri = "/api/loggednotifications";

                string json = JsonConvert.SerializeObject(filter);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<LoggedNotification>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Update Logged Notification
        /// </summary>
        /// <param name="notificationId">Id of the Logged Notification</param>
        /// <param name="loggedNotification">Logged Notification to be updated</param>
        /// <returns>LoggedNotification</returns>
        public async Task<LoggedNotification> UpdateLoggedNotification(string notificationId, LoggedNotification loggedNotification)
        {
            try
            {
                string uri = "/api/loggednotifications/" + notificationId;

                string json = JsonConvert.SerializeObject(loggedNotification);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<LoggedNotification>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Delete Logged Notification
        /// </summary>
        /// <param name="notificationId">Id of the Logged Notification</param>
        /// <returns>True if deleted</returns>
        public async Task<bool> DeleteLoggedNotification(string notificationId)
        {
            try
            {
                string uri = "/api/loggednotifications/delete/" + notificationId;

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText) && responseBodyAsText == "true")
                    return true;
                else
                    return false;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }
        }

        #endregion

        #region Notification Devices

        /// <summary>
        /// Get List of Notification Devices
        /// </summary>
        /// <returns>List of Notification Devices</returns>
        public async Task<List<NotificationDevice>> GetNotificationDevices()
        {
            try
            {
                string uri = "/api/NotificationDevices";

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<NotificationDevice>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Register Or UpdateNotification Devices
        /// </summary>
        /// <param name="device">Device to modify or delete</param>
        /// <returns>True if success</returns>
        public async Task<bool> RegisterOrUpdateNotificationDevices(NotificationDevice device)
        {
            try
            {
                string uri = "/api/NotificationDevices";

                string json = JsonConvert.SerializeObject(device);

                string responseBodyAsText = await SendAsync(uri, json);
                if (!string.IsNullOrEmpty(responseBodyAsText) && responseBodyAsText == "true")
                    return true;
                else
                    return false;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }
        }

        #endregion

        #region UserAssetUploadService
        /// <summary>
        /// Upload the file to API to get the reference filename to be entered as answer to a question(DisplayType 'File').
        /// </summary>
        /// <param name="data">Byte Array of File</param>
        /// <param name="filename">User Provided File Name</param>
        /// <param name="contentType">Mime Content Type(ex:'image/png')</param>
        /// <param name="surveytoken">Survey Token When UPloading Anonymously</param>
        /// <returns>API File Name for Reference and Download</returns>
        public async Task<string> UploadUserAsset(byte[] data, string filename, string contentType, string surveytoken = null)
        {
            try
            {
                string uri = "/api/UploadUserAsset";

                if (!string.IsNullOrEmpty(surveytoken))
                    uri += "/" + surveytoken;

                var fileContent = new StreamContent(new MemoryStream(data));
                fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
                {
                    Name = "\"files\"",
                    FileName = "\"" + filename + "\""
                }; // the extra quotes are key here
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                var content = new MultipartFormDataContent();
                content.Add(fileContent);

                HttpResponseMessage response = null;
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _url + uri);
                request.Content = content;

                if (string.IsNullOrEmpty(surveytoken) && !string.IsNullOrEmpty(token))
                    request.Headers.Add("Authorization", "Bearer " + token);

                response = await httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string apifile = await response.Content.ReadAsStringAsync();
                    return apifile.Replace("\"", "");
                }
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        /// <summary>
        /// Download a Asset uploaded to the API
        /// </summary>
        /// <param name="filename">API Provided Filename</param>
        /// <returns>Byte Array</returns>
        public async Task<byte[]> DownloadUserAsset(string filename)
        { // Download Content W/ Retry

            try
            {
                filename = filename.Replace(".", "-");
                string uri = "/api/DownloadUserAsset/" + filename;

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                { // download file using token

                    string location = responseBodyAsText.Replace("\"", "");
                    return await DownloadContent(_url + "/api/" + location);
                }

                return null;
            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        #endregion

        #region Widgets
        /// <summary>
        /// Get List Of Widgets
        /// </summary>
        /// <returns></returns>
        public async Task<List<Widget>> GetWidgets()
        {
            try
            {
                string uri = "/api/widgets";

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<Widget>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        /// <summary>
        /// Get List Of WidgetUnit
        /// </summary>
        /// <returns></returns>
        public async Task<List<WidgetUnit>> GetWidgetUnits()
        {
            try
            {
                string uri = "/api/WidgetUnits";

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<WidgetUnit>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        public async Task<List<WidgetUnit>> GetWidgetUnits(List<string> ids)
        {
            try
            {
                string uri = "/api/WidgetUnits";


                string filterJSON = null;

                if (ids != null)
                    filterJSON = JsonConvert.SerializeObject(ids);

                string responseBodyAsText = await SendAsync(uri, filterJSON);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<List<WidgetUnit>>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }

        public async Task<WidgetSummaryVisualization> GetWidgetJSON(WidgetFilter filter)
        {
            try
            {
                string uri = "/api/WidgetsSummary/Visualize";


                string filterJSON = null;

                if (filter != null)
                    filterJSON = JsonConvert.SerializeObject(filter);

                string responseBodyAsText = await SendAsync(uri, filterJSON);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<WidgetSummaryVisualization>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }


        /// <summary>
        /// Delete Widgets
        /// </summary>
        /// <returns></returns>
        public async Task<bool> DeleteWidget(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                    return false;
                string uri = $"/api/Widgets/Delete/{id}";

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<bool>(responseBodyAsText);
                else
                    return false;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }
        }

        /// <summary>
        /// Delete Widget Uniy
        /// </summary>
        /// <returns></returns>
        public async Task<bool> DeleteWidgetUnit(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                    return false;
                string uri = $"/api/WidgetUnits/Delete/{id}";

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<bool>(responseBodyAsText);
                else
                    return false;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return false;
            }
        }
        #endregion

        #region QuestionExtraAttributes

        public async Task<QuestionExtraAttributes> GetQuestionExtraAttributes(string key)
        {
            try
            {
                string uri = $"/api/Questions/ExtraAttributes/{key}";

                string responseBodyAsText = await SendAsync(uri);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<QuestionExtraAttributes>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
        #endregion

        public async Task<FlashNPS> GetFlashNPS(FilterBy filter)
        {
            try
            {

                string filterJSON = null;

                if (filter != null)
                    filterJSON = JsonConvert.SerializeObject(filter);

                string uri = "/api/FlashNPS";
                string responseBodyAsText = await SendAsync(uri, filterJSON);
                if (!string.IsNullOrEmpty(responseBodyAsText))
                    return JsonConvert.DeserializeObject<FlashNPS>(responseBodyAsText);
                else
                    return null;

            }
            catch (Exception ex)
            {
                Trace(ex);
                return null;
            }
        }
    }

    public class MyEventArgs : EventArgs
    {
        public MyEventArgs(Exception ex)
        {
            MyException = ex;
        }

        public Exception MyException { get; private set; }
    }

    public class Question
    {
        /// <summary>
        /// Automatically Provided by API, Use as Ref to Update/Delete
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Automatically Set by API
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// Optional Group To Belong To
        /// </summary>
        public string SetName { get; set; }

        /// <summary>
        /// Order of Question Presentation(Lower is first)
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// Question Text Presented  (ex: Rate us on Service)
        /// </summary>
        public string Text { get; set; } // Public Question Text

        /// <summary>
        /// Optional Prefix Title to Start (ex: Group Title)
        /// </summary>
        public string TitleText { get; set; } // Public Question Title Text

        /// <summary>
        /// Optional Audio Version of Question For IVRS as URL(.wav)
        /// </summary>
        public string Audio { get; set; } // 

        /// <summary>
        /// Type :  Flag, Text, Number, Slider, Select, MultiSelect, OrderBy, File
        /// </summary>
        public string DisplayType { get; set; }

        /// <summary>
        /// Choices, Examples : (Yes, No) (Lunch, Dinner) (Unlikely, Likely, Very Likely)
        /// </summary>
        public List<string> MultiSelect { get; set; }

        /// <summary>
        /// Optional Legend, Example : 0;Unlikely-5;Very Likely
        /// </summary>
        public List<string> DisplayLegend { get; set; }

        /// <summary>
        /// Optional Tag For Choices
        /// </summary>
        public List<string> MultiSelectChoiceTag { get; set; } // Choice Tags for Multiselect in same positions

        /// <summary>
        /// Optional Set Texts using Option On Filter Matches
        /// </summary>
        public List<LeadingOption> LeadingDisplayTexts { get; set; }

        /// <summary>
        /// Set to mark this question to be presented before starting a survey(on a tablet) for staff to fill-in
        /// </summary>
        public bool StaffFill { get; set; } // for display to staff only

        /// <summary>
        /// Set to mark this question not to be presented during survey
        /// </summary>
        public bool ApiFill { get; set; } // Not for display to user and staff, but programmatically filled

        //[Obsolete("Replaced with DisplayLocation")]
        //public string LocationSpecific { get; set; }

        /// <summary>
        /// Optional For Display in Only These Locations
        /// </summary>
        public List<string> DisplayLocation { get; set; }

        /// <summary>
        /// Optional Lookup Display Locations using Set Location Tags And Automatically Set DisplayLocation Array
        /// </summary>
        public List<string> DisplayLocationByTag { get; set; }

        /// <summary>
        /// CDM Analytics Weight(1 = Normal Weight)
        /// </summary>
        public double UserWeight { get; set; } // Weight

        //[Obsolete("Replaced with NPS Indices", false)]
        //public double CDMWeight { get; set; } // Weight

        /// <summary>
        /// Optionally Set to over-ride auto decision making on orientation of display
        /// </summary>
        public string DisplayStyle { get; set; }

        [Obsolete("Replaced with ConditionalFilter", false)]
        public string ConditionalToQuestion { get; set; } // Depreceated(replaced by  id

        [Obsolete("Replaced with ConditionalFilter", false)]
        public List<String> ConditionalAnswerCheck { get; set; } // "gt", "lt", "eq", "Lunch", "Lunch,Dinner"

        [Obsolete("Replaced with ConditionalFilter", false)]
        public int ConditionalNumber { get; set; } // 1, 2

        /// <summary>
        /// Set True to enable displaying a alternate thank you message set by this question at end of survey
        /// </summary>
        public bool EndOfSurvey { get; set; } // True to provide submit button if this is answered

        /// <summary>
        /// Alternate Thank You Message
        /// </summary>
        public string EndOfSurveyMessage { get; set; } // Override the thank you message

        /// <summary>
        /// Optional Conditions Set For This Question To be Valid For Presentation in Survey 
        /// </summary>
        public FilterBy ConditionalFilter { get; set; }

        /// <summary>
        /// Optional - Randomize Choices to avoid survey fatigue
        /// </summary>
        public string PresentationMode { get; set; } // Invert, Random, Vertical

        [Obsolete]
        public string AnalyticsTag { get; set; }

        /// <summary>
        /// Optional make this question required for survey to be complete
        /// </summary>
        public bool IsRequired { get; set; }

        /// <summary>
        /// Optional List of Tags(Ex: Name, Email, Mobile, Comments)
        /// </summary>
        public List<string> QuestionTags { get; set; }

        /// <summary>
        /// Optional Questionnaire Topic
        /// </summary>
        public List<string> TopicTags { get; set; } // Part of these Topics(Questionnaires)

        //GoodAfter, GoodBefore
        /// <summary>
        /// Present question only after this date/time (Ex: During Promo Period)
        /// </summary>
        public DateTime? GoodAfter { get; set; }

        /// <summary>
        /// Present question only until this data/time (Ex: Seasonal Question)
        /// </summary>
        public DateTime? GoodBefore { get; set; }

        //Time of Day
        /// <summary>
        /// Present question only after this time of day(ex: Dinner Questions) 
        /// </summary>
        public DateTime? TimeOfDayAfter { get; set; }
        /// <summary>
        /// Present question only before this time of day(ex: Lunch Questions)
        /// </summary>
        public DateTime? TimeOfDayBefore { get; set; }

        //Retired ( Do not use anymore )
        /// <summary>
        /// Set 'True' to no-longer present this question, but hold it for historic analytics
        /// </summary>
        public bool IsRetired { get; set; }

        //Note
        /// <summary>
        /// Optional Note
        /// </summary>
        public string Note { get; set; }

        /// <summary>
        /// Optional Display Background Image URL
        /// </summary>
        public string BackgroundURL { get; set; } // Background Image Location

        /// <summary>
        /// Optional text to display while collecting response to this question(ex: privacy note on a question asking for mobile number)
        /// </summary>
        public string DisclaimerText { get; set; } // Question Specific "Information provided here is kept .."

        /// <summary>
        /// Accept input only if this Regex Pattern Matches(ex. Membership #)
        /// </summary>
        public string ValidationRegex { get; set; } // Validate when found with(if for) text entry
        /// <summary>
        /// Display hint text when the Regex Pattern does not Match(ex. Membership Numbers need to be 6 digit)
        /// </summary>
        public string ValidationHint { get; set; } // Hint Message to display on Validation Failure

        /// <summary>
        /// Enter international strings for multiLingual surveys
        /// </summary>
        public Dictionary<string, AltDisplay> Translated { get; set; }  // ISO 639-1 Code(ex: en = > english), Translated Display Item

        /// <summary>
        /// Optional Time This Question for gamification ( count down timer will be displayed )
        /// </summary>
        public int TimeLimit { get; set; } // Limit the on-screen time for question to this many seconds, display count-down on top right, that starts blinking as it goes less than 10

        /// <summary>
        /// On collecting this response, post to this url and get a pre-fill(ex: lookup CRM Db to find if the mobile number is a customer and customize the questionnaire)
        /// </summary>
        public string InteractiveLiveAPIPreFillUrl { get; set; } // If found, post to this url the response to this question and receive any pre-fill in the background to modify the question flow

        /// <summary>
        /// Protect responses collected by making it masked on dashboard/reports(ex: mobile number)
        /// </summary>
        public bool RestrictedData { get; set; } // Not visible entirly for Read-Users(does not apply to anybody who can edit this question itself)

        /// <summary>
        /// Encrypt using Customers Own Master Key Before Storage(ex: Set True for Classified Potential PII, Set False for False Positive Non-Classified Text Input, Set Null for Unclassified)
        /// </summary>
        public bool? SensitiveData { get; set; }

        /// <summary>
        /// Custom Attributes Per Question
        /// </summary>
        public Dictionary<string, string> Attributes { get; set; }

        /// <summary>
        /// Custom Formula For a Question
        /// </summary>
        public UserDefinedMetric CustomMetric { get; set; }

        public override string ToString()
        {
            return Id + ": " + Text + " (" + DisplayType + ") ";
        }

        /// <summary>
        /// Optional Overrides On Key Display Attributes ( Location = > Attributes )
        /// </summary>
        public List<QuestionOverrideAttributes> PerLocationOverride { get; set; }

        /// <summary>
        /// Redact Text Input On Finding Select Sensitive Words
        /// </summary>
        public List<string> RedactFullyOnFinding { get; set; }

        /// <summary>
        /// Redact All Numbers Found Within Text If Above(ex: 2 digits)
        /// </summary>
        public int RedactNumbersOver { get; set; }

        /// <summary>
        /// Redact All Email Addresses
        /// </summary>
        public bool RedactEmailAddresses { get; set; }

        /// <summary>
        /// Specifies which question's option should it Display
        /// </summary>
        public List<PipingLogic> PipingOptions { get; set; }


        /// <summary>
        /// Process Text Sentiment On Comment Collected To This Question
        /// </summary>
        public TextClassifier ProcessTextSentiment { get; set; }

        /// <summary>
        /// Process Text Theme On Comment Collected To This Question
        /// </summary>
        public TextClassifier ProcessTextTheme { get; set; }

    }

    /// <summary>
    /// Input to Rename
    /// </summary>
    public class Rename
    {
        /// <summary>
        /// Current Value
        /// </summary>
        public string OldValue { get; set; }
        /// <summary>
        /// New Value
        /// </summary>
        public string NewValue { get; set; }
    }

    public class TextClassifier
    {
        /// <summary>
        /// Valid Choices are : AzureML, Watson, DeepLearn or BoW(Cloudcherry)
        /// </summary>
        /// <remarks>Try Engine in Sequence As Fallbacks or During Training (ex: DeepLearn, AzureML)</remarks>
        public List<string> Engines { get; set; }

        /// <summary>
        /// Valid Choices are : Sentiment(selected from Settings.MoodClassifications), Theme(choice from Settings.ThemeClassifications)
        /// </summary>
        /// <remarks>AzureML is Open Theme(outside of Settings.ThemeClassifications), Watson Does not support Theme</remarks>
        public string ClassificationType { get; set; }

        /// <summary>
        /// Save the Engine Choice
        /// </summary>
        public string SaveResultToQuestionId { get; set; }
    }

    /// <summary>
    /// Overrides On Question Display
    /// </summary>
    public class QuestionOverrideAttributes
    {
        /// <summary>
        /// Location Name
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Optional Group To Belong To
        /// </summary>
        public string SetName { get; set; }

        /// <summary>
        /// Order of Question Presentation(Lower is first)
        /// </summary>
        public int? Sequence { get; set; }

        /// <summary>
        /// Question Text Presented  (ex: Rate us on Service)
        /// </summary>
        public string Text { get; set; } // Public Question Text

        /// <summary>
        /// Optional Prefix Title to Start (ex: Group Title)
        /// </summary>
        public string TitleText { get; set; } // Public Question Title Text

        /// <summary>
        /// Optional Legend, Example : 0;Unlikely-5;Very Likely
        /// </summary>
        public List<string> DisplayLegend { get; set; }

        /// <summary>
        /// Optional Audio Version of Question For IVRS as URL(.wav)
        /// </summary>
        public string Audio { get; set; } // 

        /// <summary>
        /// Set to mark this question to be presented before starting a survey(on a tablet) for staff to fill-in
        /// </summary>
        public bool? StaffFill { get; set; } // for display to staff only

        /// <summary>
        /// Set to mark this question not to be presented during survey
        /// </summary>
        public bool? ApiFill { get; set; } // Not for display to user and staff, but programmatically filled

        /// <summary>
        /// Set True to enable displaying a alternate thank you message set by this question at end of survey
        /// </summary>
        public bool? EndOfSurvey { get; set; } // True to provide submit button if this is answered

        /// <summary>
        /// Alternate Thank You Message
        /// </summary>
        public string EndOfSurveyMessage { get; set; } // Override the thank you message

        /// <summary>
        /// Optional Conditions Set For This Question To be Valid For Presentation in Survey 
        /// </summary>
        public FilterBy ConditionalFilter { get; set; }

        /// <summary>
        /// Optional make this question required for survey to be complete
        /// </summary>
        public bool? IsRequired { get; set; }

        /// <summary>
        /// Optional Display Background Image URL
        /// </summary>
        public string BackgroundURL { get; set; } // Background Image Location

        /// <summary>
        /// Optional text to display while collecting response to this question(ex: privacy note on a question asking for mobile number)
        /// </summary>
        public string DisclaimerText { get; set; } // Question Specific "Information provided here is kept .."

        /// <summary>
        /// Optional Time This Question for gamification ( count down timer will be displayed )
        /// </summary>
        public int? TimeLimit { get; set; } // Limit the on-screen time for question to this many seconds, display count-down on top right, that starts blinking as it goes less than 10

        /// <summary>
        /// On collecting this response, post to this url and get a pre-fill(ex: lookup CRM Db to find if the mobile number is a customer and customize the questionnaire)
        /// </summary>
        public string InteractiveLiveAPIPreFillUrl { get; set; } // If found, post to this url the response to this question and receive any pre-fill in the background to modify the question flow

        /// <summary>
        /// Enter international strings for multiLingual surveys
        /// </summary>
        public Dictionary<string, AltDisplay> Translated { get; set; }  // ISO 639-1 Code(ex: en = > english), Translated Display Item

        /// <summary>
        /// Optional Set Texts using Option On Filter Matches
        /// </summary>
        public List<LeadingOption> LeadingDisplayTexts { get; set; }

        /// <summary>
        /// Custom Attributes Per Question
        /// </summary>
        public Dictionary<string, string> Attributes { get; set; }

        /// <summary>
        /// Custom Formula For a Question
        /// </summary>
        public UserDefinedMetric CustomMetric { get; set; }

        /// <summary>
        /// Specifies which question's option should it Display
        /// </summary>
        public List<PipingLogic> PipingOptions { get; set; }
    }

    /// <summary>
    /// Change Question Text/Choices Based on Conditional Flow ( ex: display we're delighted to hear more on a comments question for users who rated NPS high)
    /// </summary>
    public class LeadingOption
    { // Leading Text/Options for Display Only On Condition Match
        /// <summary>
        /// Filter to match for this version of text to be displayed
        /// </summary>
        public FilterBy filter { get; set; }
        /// <summary>
        /// Question Text To Replace When Filter Matches
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// Optional Prefix Title to Start (ex: Group Title)
        /// </summary>
        public string TitleText { get; set; } // Public Question Title Text
        /// <summary>
        /// Audio Version of Text
        /// </summary>
        public string Audio { get; set; } // Optional Audio Question For IVRS
        /// <summary>
        /// Alternate Choices for a MultiSelect Question When Filter Matches
        /// </summary>
        public List<String> MultiSelect { get; set; }

        /// <summary>
        /// Optional Language Selection, ISO 639-1 Code(ex: en = > english)
        /// </summary>
        public string Language { get; set; }
    }

    /// <summary>
    /// Translated Version of Display Text/Audio for International Language Support
    /// </summary>
    public class AltDisplay
    { // Translated String for Display Only
        /// <summary>
        /// Translated Question Text(UTF-8)
        /// </summary>
        public string Text { get; set; }
        /// <summary>
        /// Optional Prefix Title to Start (ex: Group Title)
        /// </summary>
        public string TitleText { get; set; } // Public Question Title Text
        /// <summary>
        /// Translated Question Audio URL
        /// </summary>
        public string Audio { get; set; } // Optional Audio Question For IVRS
        /// <summary>
        /// Translated Legends To Be Shown
        /// </summary>
        public List<string> DisplayLegend { get; set; } // (Unlikely,Likely) ('Happy','Unhappy')
        /// <summary>
        /// Translated Choices To Pick
        /// </summary>
        public List<string> MultiSelect { get; set; } //(Yes, No) (Lunch, Dinner) (Unlikely, Likely, Very Likely)

        /// <summary>
        /// Translated Thank you message
        /// </summary>
        public string EndOfSurveyMessage { get; set; } // Override the thank you message
        /// <summary>
        /// Translated Audio Thank you
        /// </summary>
        public string EndOfSurveyAudio { get; set; } // Optional Audio Question For IVRS
        /// <summary>
        /// Translated On Screen Disclaimer Text 
        /// </summary>
        public string DisclaimerText { get; set; } // Question Specific "Information provided here is kept .."
        /// <summary>
        /// Optional Regex for validation
        /// </summary>
        public string ValidationRegex { get; set; } // Validate when found with(if for) text entry
    }

    /// <summary>
    /// Piping options from the parent question
    /// </summary>
    public class PipingLogic
    {
        /// <summary>
        /// Specifies from which question should it get the options
        /// </summary>
        public string QuestionId { get; set; }
        /// <summary>
        /// Specifies whether pipe using Selected values or Unselected values
        /// </summary>
        public bool ShouldPipeUnselected { get; set; }
        /// <summary>
        /// Whatever selected this would be your default options
        /// </summary>
        public List<string> DefaultOptions { get; set; }
        /// <summary>
        /// Mapping of selected options if the use case is dynamic
        /// </summary>
        public Dictionary<string, List<string>> DynamicOptions { get; set; }
    }

    public class UserDefinedMetric
    {
        /// <summary>
        /// Name of the identifier
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// i.e. CallExpression, BinaryExpression, Literal, Identifier
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// i.e. +, -, *, /
        /// </summary>
        public string Operator { get; set; }
        /// <summary>
        /// Value as number
        /// </summary>
        public double Value { get; set; }
        /// <summary>
        /// Value as string
        /// </summary>
        public string Raw { get; set; }
        /// <summary>
        /// List of Elements in an array
        /// </summary>
        public List<UserDefinedMetric> Elements { get; set; }
        /// <summary>
        /// List of Arguments
        /// </summary>
        public List<UserDefinedMetric> Arguments { get; set; }
        /// <summary>
        /// Functions if available
        /// </summary>
        public UserDefinedMetric Callee { get; set; }
        /// <summary>
        /// Left expression
        /// </summary>
        public UserDefinedMetric Left { get; set; }
        /// <summary>
        /// Right expression
        /// </summary>
        public UserDefinedMetric Right { get; set; }
    }

    /// <summary>
    /// Analytics Summary with Delight Score, NPS
    /// </summary>
    public class ResponseSummary
    {
        public List<Summary> Summary { get; set; }
        public int weightedScore { get; set; }
        public int ResponseCount { get; set; }
        public List<String> AssortedWordCloud { get; set; }
        public List<String> Tags { get; set; }
        public NPS NetPromoter { get; set; }
    }

    public class FlashNPS
    {
        public int TotalResponses { get; set; }
        public NPS NetPromoter { get; set; }
    }

    /// <summary>
    /// CDM Analytics Score Breakup
    /// </summary>
    public class AnalyticsOutputRATER
    {
        //Coefficients
        public double RealiabilityCoefficent { get; set; }
        public double AssuranceCoefficent { get; set; }
        public double TangibilityCoefficent { get; set; }
        public double EmpathyCoefficent { get; set; }
        public double ResponsivenessCoefficent { get; set; }
        public double InterceptCoefficent { get; set; }

        //Mean
        public double RealiabilityMean { get; set; }
        public double AssuranceMean { get; set; }
        public double TangibilityMean { get; set; }
        public double EmpathyMean { get; set; }
        public double ResponsivenessMean { get; set; }
        public double OverallMean { get; set; }

        public double CDMScore { get; set; }
        public List<double> Score { get; set; }

        public List<double> NormalizedWeights { get; set; }
        public List<double> NormalizedPerformance { get; set; }
        public List<double> NormalizedScore { get; set; }

        public void CalcCDM()
        {

            //CDM Score Breakup
            Score = new List<double> { (RealiabilityCoefficent * RealiabilityMean), (AssuranceCoefficent * AssuranceMean), (TangibilityCoefficent * TangibilityMean),
                (EmpathyCoefficent * EmpathyMean), (ResponsivenessCoefficent * ResponsivenessMean) };

            //Intercept + (Coefficent * Average)
            CDMScore = InterceptCoefficent + Score.Sum();

            if (CDMScore != 0)
            {
                double CoefficentSum = RealiabilityCoefficent + AssuranceCoefficent + TangibilityCoefficent + EmpathyCoefficent + ResponsivenessCoefficent;

                //Normalize Weights
                NormalizedWeights = new List<double> { (RealiabilityCoefficent / CoefficentSum), (AssuranceCoefficent / CoefficentSum), (TangibilityCoefficent / CoefficentSum), (EmpathyCoefficent / CoefficentSum), (ResponsivenessCoefficent / CoefficentSum) };

                //Normalize Performance
                NormalizedPerformance = new List<double> { RealiabilityMean / 5, AssuranceMean / 5, TangibilityMean / 5, EmpathyMean / 5, ResponsivenessMean / 5, OverallMean / 5 };

                //Normalize Score
                NormalizedScore = new List<double>();
                foreach (var s in Score)
                    NormalizedScore.Add(s / Score.Sum());
            }
        }
    }

    /// <summary>
    /// Analytics Summary with Delight Score, Liked, Disliked, NPS
    /// </summary>
    public class AnalyticsSummary
    {
        public string LocationName { get; set; }
        public List<int> PerDayResponseCount { get; set; }
        public int MDM { get; set; }
        public string CDMToken { get; set; } // Retrive CDM using another call

        public int TotalResponses { get; set; }
        public List<Summary> LocationSummary { get; set; }
        public List<String> LocationTags { get; set; }
        public List<String> WordCloud { get; set; }

        public String Liked { get; set; }
        public String Disliked { get; set; }
        public NPS NetPromoter { get; set; }
        public Dictionary<String, String> NPSAnalytics { get; set; }

        public Dictionary<string, NPS> NPSBasedIndex { get; set; } // Optional Custom NPS Based Index, Add NetPromoter Score to Delight Meter, and Default display when set

        public List<Comment> LatestComments { get; set; }
    }
    public class Comment
    {
        public string Id { get; set; }
        public DateTime TimeStamp { get; set; }
        public string Location { get; set; }
        public string Comments { get; set; }
        public int Rating { get; set; }
    }
    /// <summary>
    /// NPS Break Up For Question Tagged "NPS"
    /// </summary>
    public class NPS
    {
        public string QuestionID { get; set; }
        public string QuestionText { get; set; }
        public int Promoters { get; set; }
        public int Passive { get; set; }
        public int Detractors { get; set; }

        public int NetPromoters { get; set; }

        public bool DefaultIndex { get; set; }
    }

    public class NPSComposite
    {
        public string IndexName { get; set; }
        public bool DefaultIndex { get; set; }
        public List<FilterBy> Segments { get; set; } // Different Filter Segments to build a NPS Composite(ex: buyers + non-buyers)
    }

    /// <summary>
    /// CDM Analytics
    /// </summary>
    public class AnalyticsSummaryCDM
    {
        public string LocationName { get; set; }
        public int CDM { get; set; }
        public AnalyticsOutputRATER CDMBreakup { get; set; }
    }

    /// <summary>
    /// Analytics Summary
    /// </summary>
    public class Summary
    {
        public Question AskedQuestion { get; set; }
        public Dictionary<string, int> Response { get; set; }
        public Dictionary<DateTime, Double> RatingTrend { get; set; }
    }

    /// <summary>
    /// Analytics Summary by Key Value
    /// </summary>
    public class SummaryKeyValue
    {
        public string Key { get; set; }
        public int Value { get; set; }
    }

    /// <summary>
    /// Analytics Intersect Questions Summary
    /// </summary>
    public class SummaryLite
    {
        public String AskedQuestion { get; set; }
        public Dictionary<string, int> Response { get; set; }
    }

    /// <summary>
    /// Collection of all responses to a survey PageWise
    /// </summary>
    public class PageWiseAnswer
    {
        public int currentPage { get; set; } // Page No
        public List<Answer> Responses { get; set; } // List of Answers
        public int totalCount { get; set; } // Page No
        public int totalPages { get; set; } // Page No
    }

    /// <summary>
    /// Response Collected Per Survey
    /// </summary>
    public class Answer
    {
        /// <summary>
        /// Provided by API
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// Account
        /// </summary>
        public string User { get; set; } // Business User

        /// <summary>
        /// Where
        /// </summary>
        public string LocationId { get; set; } // Where

        /// <summary>
        /// When
        /// </summary>
        public DateTime ResponseDateTime { get; set; } // When

        /// <summary>
        /// Response Collection Duration in Seconds(Start to Finish)
        /// </summary>
        public int ResponseDuration { get; set; }

        /// <summary>
        /// Survey Client ID
        /// </summary>
        public string SurveyClient { get; set; }

        /// <summary>
        /// Responses to Questions Presented
        /// </summary>
        public List<Response> Responses { get; set; } //Answers

        /// <summary>
        /// True When Archived
        /// </summary>
        public bool Archived { get; set; }

        //public bool ContainsAttachment { get; set; } // Set True When It Contains File Attachment

        /// <summary>
        /// Notes(Reply) Added by viewers
        /// </summary>
        public List<MessageNote> Notes { get; set; } // Notes(Reply) Added by viewers

        /// <summary>
        /// Optional Ticket Opened
        /// </summary>
        public Ticket OpenTicket { get; set; } // Optional Ticket Opened Data

        public string DeviceId { get; set; }
    }

    /// <summary>
    /// CEM Tickets To Enable NPS Loop WorkFlow
    /// </summary>
    public class Ticket
    {
        /// <summary>
        /// Open, Resolved, Declined, Closed
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// Ticket Opener
        /// </summary>
        public string AssignedBy { get; set; }

        /// <summary>
        /// Call, Info, Action, Respond
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Ticket Assigned to Department
        /// </summary>
        public string Department { get; set; }

        /// <summary>
        /// Language(Assigned or looked-up from Language Q)
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Ticket First Responder (FAR)
        /// </summary>
        public string OrginalRoutedTo { get; set; }

        /// <summary>
        /// Created TimeStamp
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// Defer Till Time/Date
        /// </summary>
        public DateTime? After { get; set; }

        /// <summary>
        /// Count of times this has been escalated ( 0 on arrival/first action responder )
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// If not Closed, Move to Next In line
        /// </summary>
        public DateTime? NextEscalationIn { get; set; }

        /// <summary>
        /// If not Closed, Assign ticket to this user
        /// </summary>
        public string NextEscalationUser { get; set; }

        /// <summary>
        /// Ticket Closed TimeStamp
        /// </summary>
        public DateTime? Closed { get; set; }

        /// <summary>
        /// Ticket Rating On Close By Dept Admin on  5 Point Scale
        /// </summary>
        public int Rating { get; set; }

        /// <summary>
        /// All Users Assigned to
        /// </summary>
        public List<String> RouteHistory { get; set; }

        /// <summary>
        /// Ticket Assigned To 
        /// </summary>
        public string CurrentAssignedTo { get; set; }

        /// <summary>
        /// Ticket CC'ed To (For Information)
        /// </summary>
        public List<string> CCedTo { get; set; }

        /// <summary>
        /// Higher Priority for Larger Numbers
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Is Escalated (FAR did not respond in time)
        /// </summary>
        public bool IsEscalated { get; set; }

        /// <summary>
        /// Description for Ticket
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Response Collected From TroubleShooting Using Checklist
        /// </summary>
        public List<string> DiagnosticSurveyResponses { get; set; }

        /// <summary>
        /// Ticket is sticky, within Department
        /// </summary>
        public bool IsShowcased { get; set; }

        /// <summary>
        /// Ticket is stick, within Enterprise
        /// </summary>
        public bool IsGlobalShowcased { get; set; }

        /// <summary>
        /// Title for the showcase(ex: the customer who had tears of happiness!)
        /// </summary>
        public string ShowcaseTitle { get; set; }

        /// <summary>
        /// Admin Settings the Showcase Action
        /// </summary>
        public string ShowcasedBy { get; set; }

        /// <summary>
        /// Comments on Ticket
        /// </summary>
        public string Comments { get; set; }
    }

    /// <summary>
    /// Notes to add to a Collected Response
    /// </summary>
    public class MessageNote
    {
        public string User { get; set; }
        public string AuthorName { get; set; } // Required/Prompt While Adding Note
        public DateTime NoteTime { get; set; }
        public string Note { get; set; }

        public string NoteSurveyID { get; set; } // Response ID of Completed Note Survey Using Token

        public Dictionary<string, string> Attachments { get; set; } // User Filename, API Filename
    }

    /// <summary>
    /// Answer to a Single Question
    /// </summary>
    public class Response
    {
        public string QuestionId { get; set; }
        public string QuestionText { get; set; }
        public string TextInput { get; set; }
        public int NumberInput { get; set; }
    }


    /// <summary>
    /// Translated String for Display Only
    /// </summary>
    public class AltDisplaySettings
    {
        /// <summary>
        /// Optional Welcome Intro/Title
        /// </summary>
        public string WelcomeTitle { get; set; }

        /// <summary>
        /// Ex: "Please help us understand .."
        /// </summary>
        public string WelcomeText { get; set; }
        /// <summary>
        /// Optional Audio For IVRS
        /// </summary>
        public string WelcomeAudio { get; set; }

        /// <summary>
        /// Thank you message title
        /// </summary>
        public string ThankyouTitle { get; set; }
        /// <summary>
        /// Thank you message text
        /// </summary>
        public string ThankyouText { get; set; }
        /// <summary>
        /// Optional Audio For IVRS
        /// </summary>
        public string ThankyouAudio { get; set; }
        /// <summary>
        /// ex: "Information provided here is kept .."
        /// </summary>
        public string DisclaimerText { get; set; }
    }

    /// <summary>
    /// Location Detail with Address, Logo, and Tags
    /// </summary>
    public class Location
    {
        public string Name { get; set; } // Location Name
        public string Address { get; set; } // Address for map
        public string LogoURL { get; set; } // Logo Set
        public string BackgroundURL { get; set; } // Background URL set
        public string Group { get; set; } // South
        public List<string> Tags { get; set; }

        public string ColorCode1 { get; set; } // Hex Color Code
        public string ColorCode2 { get; set; } // Hex Color Code
        public string ColorCode3 { get; set; } // Hex Color Code
        public string WelcomeText { get; set; } // "Please help us understand .."
        public string ThankyouText { get; set; } // "Please help us understand .."
    }


    public class TicketFilterBy
    {
        public string Status { get; set; }
        public string AssignedBy { get; set; }
        public string Action { get; set; }
        public string Department { get; set; }
        public string Language { get; set; }
        public string OrginalRoutedTo { get; set; }
        public string NextEscalationUser { get; set; }
        public string CurrentAssignedTo { get; set; }
        public int Priority { get; set; }
        public bool IsEscalated { get; set; }
        public bool IsDeferred { get; set; }
        public bool IsPendingRating { get; set; }
        public bool IsRated { get; set; }
        public bool IsShowcased { get; set; }
        public bool IsGlobalShowcased { get; set; }

        public List<string> Location { get; set; }
        public DateTime? Afterdate { get; set; }
        public DateTime? Beforedate { get; set; }
        public List<FilterByQuestions> Filterquestions { get; set; }
        public bool Archived { get; set; } // Only Archived

        public bool WithNotes { get; set; } // Contains Notes
        public string NotesWithAttachmentType { get; set; } // Contains Notes
        public string NotesMediaTheme { get; set; } // Contains Notes
        public string NotesMediaMood { get; set; } // Contains Notes
        public bool OnlyWithAttachments { get; set; } // Contains Attachments

        public List<DayOfWeek> Days { get; set; }
        public DateTime? Aftertime { get; set; }
        public DateTime? Beforetime { get; set; }
    }

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

    class LoginError
    {
        public string error { get; set; }
        public string error_description { get; set; }
    }

    /// <summary>
    /// Survey Definition Using Token Set To Segment for Location and More
    /// </summary>
    public class SurveyToken
    {
        /// <summary>
        /// Auto assigned by API(ex: Two Alphabets-6 Digits)
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// Account
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// Users note on token purpose ( Ex: For Printed Survey )
        /// </summary>
        public string Note { get; set; }

        /// <summary>
        /// Pre-fill these responses for Conditional Routing(Replacement for Staff-fill for use in non-tablet channels)
        /// </summary>
        public List<Response> PreFill { get; set; }

        /// <summary>
        /// API Endpoing URL Prefill by posting data received and receiving Prefills for Conditional Routing
        /// </summary>
        public string PreFillViaAPICallBack { get; set; }

        /// <summary>
        /// Valid Till Date/Time
        /// </summary>
        public DateTime? ValidTill { get; set; }
        /// <summary>
        /// Number of Valid Uses(Once = 1)
        /// </summary>
        public int ValidUses { get; set; }

        /// <summary>
        /// Select Questionnaire Targeting Location
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// Indicates if this was emailed
        /// </summary>
        public bool IsEmailed { get; set; }
        /// <summary>
        /// Indicates if this was printed
        /// </summary>
        public bool IsPrinted { get; set; }

        /// <summary>
        /// Verify Captcha On Submit
        /// </summary>
        public bool RequireCaptcha { get; set; }

        /// <summary>
        /// Downgrade to v1 Captcha(Not Recommended)
        /// </summary>
        public bool ClassicCaptcha { get; set; }

        /// <summary>
        /// Skip Welcome Screen
        /// </summary>
        public bool SkipWelcome { get; set; }

        /// <summary>
        /// Display/Email This Text At End of Survey(ex: Here is your ebook, thank you for completing this survey)
        /// </summary>
        public string RewardCode { get; set; }
        /// <summary>
        /// Question Id that is collected email for use in after survey communication using RewardCode
        /// </summary>
        public string EmailQuestion { get; set; }

        //Location
        /// <summary>
        /// Prefill City to this question
        /// </summary>
        public string CityQuestion { get; set; }

        /// <summary>
        /// Prefill State to this question
        /// </summary>
        public string StateQuestion { get; set; }

        /// <summary>
        /// Prefill Country to this question
        /// </summary>
        public string CountryQuestion { get; set; }

        /// <summary>
        /// Prefill Region to this question
        /// </summary>
        public string RegionQuestion { get; set; }

        /// <summary>
        /// Prefill IP Address to this question
        /// </summary>
        public string IPAddressQuestion { get; set; }

        /// <summary>
        /// Prefill UA(UserAgent) to this question
        /// </summary>
        public string UAQuestion { get; set; }

        /// <summary>
        /// Prefill Browser to this question ex: Firefox, Chrome, Safari, Opera, IE
        /// </summary>
        public string BrowserQuestion { get; set; }

        /// <summary>
        /// Prefill Browser Version to this question ex: 10, 47
        /// </summary>
        public string BrowserVersionQuestion { get; set; }

        /// <summary>
        /// Prefill OS to this question ex: iOS, Android
        /// </summary>
        public string OSQuestion { get; set; }

        /// <summary>
        /// Prefill Device to this question ex: iPhone
        /// </summary>
        public string DeviceQuestion { get; set; }

        /// <summary>
        /// Prefill Device's Brand to this question ex: Apple
        /// </summary>
        public string DeviceBrandQuestion { get; set; }

        /// <summary>
        /// Enable Random Sampling of Questions(per survey)
        /// </summary>
        public bool SamplingMode { get; set; }

        /// <summary>
        /// Present this many questions per sample(survey)
        /// </summary>
        public int PerSamplePresent { get; set; }

        /// <summary>
        /// Enable Micro Web Surveys
        /// </summary>
        public MicroCampaign Campaign { get; set; }

        /// <summary>
        /// Enable Social Listening (Twitter @mentions or #hashtags)
        /// </summary>
        public string ListenFromTwitter { get; set; }

        /// <summary>
        /// Optionally Opt-out when Set 'true' Partial Submisson of Responses Due when User TimesOut
        /// </summary>
        public bool DoNotPartialSubmit { get; set; }

        /// <summary>
        /// Email Open UserAgent When SurveyToken Sent as Email w/ SurveyTokenOpen Image
        /// </summary>
        public string EmailOpenUA { get; set; }

        /// <summary>
        /// Set to turn off social Sharing 
        /// </summary>
        public string DoNotSocialShare { get; set; }
    }

    /// <summary>
    /// Micro Web Surveys(MicroCherry)
    /// </summary>
    public class MicroCampaign
    {
        /// <summary>
        /// Is good to present
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Logo for Background
        /// </summary>
        public string LogoURL { get; set; }

        /// <summary>
        /// Color Code(ex: #FFFFFF for white)
        /// </summary>
        public string BackgroundColor { get; set; }

        /// <summary>
        /// 0% = > Solid, 100% = > Fully Transparent
        /// </summary>
        public int BackgroundOpacity { get; set; }

        /// <summary>
        /// Offset %, Default is 75% Where it display on bottom left
        /// </summary>
        public int HorizontalOffset { get; set; }

        /// <summary>
        /// Font Type
        /// </summary>
        public string PreferredFont { get; set; }

        /// <summary>
        /// Hex Code
        /// </summary>
        public string FontColor { get; set; }

        /// <summary>
        /// Italics, Bold, Normal
        /// </summary>
        public string FontStyle { get; set; }

        /// <summary>
        /// Question for You
        /// </summary>
        public string TitleBarText { get; set; }

        /// <summary>
        /// Randomization Algo
        /// </summary>
        [Obsolete]
        public string SelectionAlgo { get; set; }

        /// <summary>
        /// Keep Question in View for (Seconds)
        /// </summary>
        public int KeepAlive { get; set; }

        /// <summary>
        /// Before Presenting Next (Seconds)
        /// </summary>
        public int CoolDownPeriod { get; set; }

        //Trigger Spec
        /// <summary>
        /// Trigger After This Many Clicks
        /// </summary>
        public int Click { get; set; }
        /// <summary>
        /// Trigger After Wait For This May Seconds
        /// </summary>
        public int WaitSeconds { get; set; }

        /// <summary>
        /// URL Grep(IndexOf) - Trigger only on this page for which URL Contains
        /// </summary>
        public string GrepURL { get; set; }

        /// <summary>
        /// URL Grep(IndexOf) - Trigger only on this page for which URL does not contain
        /// </summary>
        public string GrepInvertURL { get; set; }

        /// <summary>
        /// Trigger After Page Scroll Percent (0-100)
        /// </summary>
        public int ScrollPercent { get; set; }

        /// <summary>
        /// Trigger When Potential Exit Is Detected
        /// </summary>
        public bool OnExitDetect { get; set; }

        /// <summary>
        /// On Exit Blocking Modal Style
        /// </summary>
        public bool OnExitDetectModal { get; set; }

        /// <summary>
        /// Only for visitors from this City(Name) ex: Chicago, Bangalore, Chennai
        /// </summary>
        public List<string> City { get; set; }

        /// <summary>
        /// Only for visitors from this State(ISO Code) ex: CA, WA, KA, TN
        /// </summary>
        public List<string> State { get; set; }

        /// <summary>
        /// Only for visitors from this Country(ISO Code) ex: US, SG, IN
        /// </summary>
        public List<string> Country { get; set; }

        /// <summary>
        /// Only for visitors from this Region(Name) ex: North America, Asia
        /// </summary>
        public List<string> Region { get; set; }

        /// <summary>
        /// Prefill Last Seen to this question
        /// </summary>
        public String LastSeenQuestion { get; set; }

        /// <summary>
        /// Prefill URL to this question on response submit
        /// </summary>
        public String URLQuestion { get; set; }

        /// <summary>
        /// Prefill Browser to this question ex: Firefox, Chrome, Safari, Opera, IE
        /// </summary>
        public String BrowserQuestion { get; set; }
    }

    /// <summary>
    /// Collection of all responses to a survey PageWise
    /// </summary>
    public class PageWiseSurveyTokens
    {
        public int currentPage { get; set; } // Page No
        public List<SurveyToken> Tokens { get; set; } // List of Answers
        public int totalCount { get; set; } // Total Count
        public int totalPages { get; set; } // Total Pages
    }

    /// <summary>
    /// Multi Channel Survey Token - See API Whitepaper For Details https://www.getcloudcherry.com/api/
    /// </summary>
    public class TokenSurvey
    {
        public string LogoURL { get; set; } // Image Location
        public string BackgroundURL { get; set; } // Background Image Location
        public string BusinessName { get; set; } // Cafe Town
        public string BusinessCountry { get; set; } // Background Country
        public string BusinessTagline { get; set; } // Brewing Since 1924
        public string ColorCode1 { get; set; } // Hex Color Code
        public string ColorCode2 { get; set; } // Hex Color Code
        public string ColorCode3 { get; set; } // Hex Color Code
        public string WelcomeText { get; set; } // "Please help us understand .."
        public string WelcomeImage { get; set; } // Image
        public string ThankyouText { get; set; } // "Thank you for feedback .."
        public string ThankyouImage { get; set; } // Image
        public string DisclaimerText { get; set; }

        public List<Question> Questions { get; set; }
        public List<Response> PreFill { get; set; }

        public string Ack { get; set; } // Reward Code

        public Dictionary<string, AltDisplaySettings> Translated { get; set; }  // ISO 639-1 Code(ex: en = > english), Translated Display Item

        public string ReCaptchaSiteKey { get; set; } // Display Recaptcha using this Key Before Submit if Set 
    }

    /// <summary>
    /// Subuser Details
    /// </summary>
    public class SubUser
    {
        public string Id { get; set; }

        public FilterBy ConditionalFilter { get; set; }
        public string Department { get; set; }
        public string EnterpriseRole { get; set; }

        public string UserName { get; set; }
        public string Name { get; set; }
        public string PrimaryRole { get; set; }
        public string Email { get; set; }
        public string MobilePhone { get; set; }
        public List<string> Languages { get; set; }
        public List<string> LocationFilter { get; set; }
        public List<MyView> MyViews { get; set; }
        public string Password { get; set; }
        public List<string> Regions { get; set; }
        public string ReportsTo { get; set; }
        public bool IsEmailVerified { get; set; }
        public bool IsMobilePhoneVerified { get; set; }
    }

    public class PageSubUser
    {
        public int currentPage { get; set; } // Page No
        public List<SubUser> Users { get; set; } // List of users
        public int totalCount { get; set; } // Total Count
        public int totalPages { get; set; } // Total Pages
    }

    public class ReportRecipient
    {

        public string Email { get; set; }
        public List<string> Locations { get; set; }

        //To Display
        [Newtonsoft.Json.JsonIgnore]
        public string LocationString
        {
            get
            {
                if (Locations == null)
                    return null;
                else if (Locations.Count < 5)
                    return String.Join(", ", Locations);
                else
                {
                    return Locations[0] + ", " + Locations[1] + " and " + (Locations.Count - 2) + " more Locations";
                }
            }
        }

    }

    /// <summary>
    /// Notifications Push Device ( Android, iOS, Windows )
    /// </summary>
    public class NotificationDevice
    {
        /// <summary>
        /// User Provided Device Name(ex: iPhone 7)
        /// </summary>
        public string DeviceName { get; set; }

        /// <summary>
        /// Push Network Provider(ex: FCM)
        /// </summary>
        public string Provider { get; set; } // FCM ( Only Valid at this time )

        /// <summary>
        /// Device Type ( Android, iOS or Web )
        /// </summary>
        public string DeviceType { get; set; }

        /// <summary>
        /// FCM Token
        /// </summary>
        public string FCMToken { get; set; }

        /// <summary>
        /// Turn On/Off Notifications On This Device
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Last Update TimeStamp
        /// </summary>
        public DateTime Updated { get; set; }
    }

    public class LocationOrTag
    {
        public string Type { get; set; }
        public List<string> Places { get; set; }
    }

    public class DemoShowCaseAccount
    {
        public string Id { get; set; }
        public string AccountName { get; set; }
        public string Login { get; set; }
        public string Password { get; set; }
    }

    /// <summary>
    /// Account Settings
    /// </summary>
    public class UserSettings
    {
        public string Id { get; set; }

        public string User { get; set; } // Business Login

        /// <summary>
        /// Logo URL For Account(Displayed on footer of questionnaire)
        /// </summary>
        public string LogoURL { get; set; } // Image Location

        /// <summary>
        /// Background Image URL for Questionnaire(.png/.jpg/.gif)
        /// </summary>
        public string BackgroundURL { get; set; } // Background Image Location

        /// <summary>
        /// Name of the Business(Displayed on Questionnaire Welcome Screen)
        /// </summary>
        public string BusinessName { get; set; } // Cafe Town

        /// <summary>
        /// Tag Line for Business(Displayed on Questionnaire Welcome Screen)
        /// </summary>
        public string BusinessTagline { get; set; } // Brewing Since 1924

        /// <summary>
        /// Primary Theme Color Code(HEX Value, ex: #FFFFFF for white)
        /// </summary>
        public string ColorCode1 { get; set; } // Hex Color Code

        /// <summary>
        /// Secondary Theme Color Code(HEX Value, ex: #FFFFFF for white)
        /// </summary>
        public string ColorCode2 { get; set; } // Hex Color Code

        /// <summary>
        /// Additional Theme Color Code(HEX Value, ex: #FFFFFF for white)
        /// </summary>
        public string ColorCode3 { get; set; } // Hex Color Code

        /// <summary>
        /// Custom Style To Apply For Various Elements
        /// </summary>
        public List<TextStyle> TextStyles { get; set; } // List of Text Display Styles

        /// <summary>
        /// Locations Defined
        /// </summary>
        public List<String> Locations { get; set; } // LocationID if any

        [Obsolete]
        public List<String> LocationLogo { get; set; } // LocationLogo if any

        /// <summary>
        /// Detail Personalization Per Location(Ex: Logo/Color and More)
        /// </summary>
        public List<Location> LocationList { get; set; } // LocationID if any

        /// <summary>
        /// Business Type - Used in Analytics and Benchmarks
        /// </summary>
        public string BusinessType { get; set; } // Cafe

        /// <summary>
        /// Primary Country of Business(ex: Two Letter - US/SG/DE/IN)
        /// </summary>
        public string BusinessCountry { get; set; } // SG

        /// <summary>
        /// Primary City of Business(Ex: Singapore)
        /// </summary>
        public string BusinessCity { get; set; } // Singapore

        /// <summary>
        /// Timezone offset in minutes from UTC (ex: 330 = +5:30 )
        /// </summary>
        public int TimeZoneOffset { get; set; } // Time Zone offset in minutes 0 = UTC, 330 = India(+5:30)

        /// <summary>
        /// Business Website(ex: http://www.yoursite.com)
        /// </summary>
        public string BusinessURL { get; set; } // CafeTown.com

        /// <summary>
        /// Business Phone In International Format ( +1-555-123-1234)
        /// </summary>
        public string BusinessPhone { get; set; } // +65-1234-234

        /// <summary>
        /// Welcome Title On Questionnaire
        /// </summary>
        public string WelcomeTitle { get; set; }

        /// <summary>
        /// Welcome Text On Questionnaire
        /// </summary>
        public string WelcomeText { get; set; } // "Please help us understand .."

        /// <summary>
        /// Audio Welcome Version for Voice Survey
        /// </summary>
        public string WelcomeAudio { get; set; } // Optional Audio For IVRS

        /// <summary>
        /// Welcome Image For Questionnaire
        /// </summary>
        public string WelcomeImage { get; set; } // Image

        /// <summary>
        /// Thank you title at end of questionnaire
        /// </summary>
        public string ThankyouTitle { get; set; } // "Please help us understand .."

        /// <summary>
        /// Thank you text at end of questionnaire
        /// </summary>
        public string ThankyouText { get; set; } // "Please help us understand .."
        /// <summary>
        /// Thank you audio at end of questionnaire
        /// </summary>
        public string ThankyouAudio { get; set; } // Optional Audio For IVRS

        /// <summary>
        /// Thank you screen image at end of questionnaire
        /// </summary>
        public string ThankyouImage { get; set; } // Image

        /// <summary>
        /// Questionnaire wide/Site-wide Disclaimer (ex: "Information provided here is kept ..")
        /// </summary>
        public string DisclaimerText { get; set; }

        /// <summary>
        /// Custom NPS Range (Format : LowerRange,HigerRange ex: 7,8)
        /// </summary>
        public string NpsRange { get; set; }

        /// <summary>
        /// Public RSS Feed Key To Pull Comments To Website/Page
        /// </summary>
        public string PublicFeedKey { get; set; }

        /// <summary>
        /// Set to receive daily reports
        /// </summary>
        public bool DailySummary { get; set; }
        /// <summary>
        /// Set to receive monthly reports
        /// </summary>
        public bool MonthlySummary { get; set; }
        /// <summary>
        /// Set to receive weekly reports
        /// </summary>
        public bool WeeklySummary { get; set; }
        /// <summary>
        /// Set to receive monthly outlier reports
        /// </summary>
        public bool MonthlyOutlierSummary { get; set; }

        /// <summary>
        /// Additional Email Recipients
        /// </summary>
        public List<String> ReportRecipients { get; set; } // Additional Email Addressess for Summary Email

        /// <summary>
        /// Account Wide Notifications Set
        /// </summary>
        public List<Notification> Notifications { get; set; }

        /// <summary>
        /// Exclude these words from word cloud and analytics(ex: brand's own name)
        /// </summary>
        public List<string> WordCloudExclude { get; set; } // Exclude these words from word cloud

        #region Integrations
        /// <summary>
        /// MailChimp API Key for Email Address Push To a Mail Chimp List
        /// </summary>
        public string MailChimpAPIKey { get; set; } // Email Campaign Management
        /// <summary>
        /// MailChimp List Name for Email Address Push
        /// </summary>
        public string MailChimpListName { get; set; }

        /// <summary>
        /// International SMS Routing API Key For Nexmo
        /// </summary>
        public string NexmoAPIKey { get; set; } // SMS Campaign Management
        /// <summary>
        /// International SMS Routing API Secret For Nemo
        /// </summary>
        public string NexmoAPISecret { get; set; }

        /// <summary>
        /// Regional SMS Routing Sender ID for Exotel
        /// </summary>
        public string ExotelSenderId { get; set; } // SMS Campaign Management
        /// <summary>
        /// Regional SMS Routing API Key for Exotel
        /// </summary>
        public string ExotelAPIKey { get; set; } // SMS Campaign Management
        /// <summary>
        /// Regional SMS Routing API Secret for Exotel
        /// </summary>
        public string ExotelAPISecret { get; set; }

        /// <summary>
        /// Regional SMS/voice Routing API Key for KooKoo/OzoneTel
        /// </summary>
        public string KooKooAPIKey { get; set; } // SMS/Voice Gateway
        /// <summary>
        /// Regional SMS/Voice Sender ID Key for KooKoo/OzoneTel
        /// </summary>
        public string KooKooSenderId { get; set; } // SMS/Voice Gateway

        /// <summary>
        /// International SMS/Voice Sender ID Key for Twilio
        /// </summary>
        public string TwilioSenderId { get; set; } // SMS/Voice Campaign Management
        /// <summary>
        /// International SMS/Voice API Key for Twilio
        /// </summary>
        public string TwilioAPIKey { get; set; } // SMS/Voice Campaign Management
        /// <summary>
        /// International SMS/Voice API Secret for Twilio
        /// </summary>
        public string TwilioAPISecret { get; set; }
        /// <summary>
        /// International Twilio App ID for Call Center Routing
        /// </summary>
        public string TwilioAppId { get; set; } // For Call Center Routing

        /// <summary>
        /// Fresh Desk Ticketing API Key
        /// </summary>
        public string FreshDeskAPIKey { get; set; } // FreshDesk API Key for Tickets
        /// <summary>
        /// Fresh Desk Ticketing Domain
        /// </summary>
        public string FreshDeskDomain { get; set; } // FreshDesk API Key for Tickets

        /// <summary>
        /// Custom SMTP Server To Boost Email Deliverability
        /// </summary>
        public SMTPServer CustomMailServer { get; set; } // Set Custom SMTP to Send Mail
        #endregion

        #region Sentiment
        /// <summary>
        /// Themes Available To Tag Message/Tickets/Calls For Sorting (ex: Product, Range)
        /// </summary>
        public List<string> ThemeClassifications { get; set; }  // Theme - Product, Range
        /// <summary>
        /// Outcomes Available To Tag Message/Tickets/Calls For Sorting(ex: Happy, Neutral)
        /// </summary>
        public List<string> MoodClassifications { get; set; } // Mood - Happy, Neutral
        #endregion

        #region Enterprise
        /// <summary>
        /// Enterprise Departments In The Organization ( ex: Marketing, Product Development )
        /// </summary>
        public List<Department> Departments { get; set; }
        /// <summary>
        /// Enterprise Departments In The Organization ( ex: ARM, RM, Store Manager, VP, CEO)
        /// </summary>
        public List<EnterpriseRole> Roles { get; set; }
        /// <summary>
        /// Optional  auto-fill selection of actions for Tickets
        /// </summary>
        public List<string> TicketActions { get; set; }
        /// <summary>
        /// Optional  additional auto-fill selection of status for Tickets
        /// </summary>
        public List<string> TicketStatus { get; set; }

        /// <summary>
        /// SubUser Security Policy
        /// </summary>
        public SubUserSecurityPolicy SecurityPolicy { get; set; }
        #endregion

        /// <summary>
        /// Single Sign On Key Set for SSO Into CloudCherry
        /// </summary>
        public string SsoAPIKey { get; set; } // Single Sign-On Key Set By User

        /// <summary>
        /// Translated Version of Display Items, Key : ISO 639-1 Code(ex: en = > english), Value : Translated Display Item
        /// </summary>
        public Dictionary<string, AltDisplaySettings> Translated { get; set; }

        /// <summary>
        /// Survey Tokens for Taking Survey on 'Responses' or 'TeamMessages' or 'TeamMessages-Tag', open modal to complete the Token survey as normal and add to MessageNote the Response ID
        /// </summary>
        public Dictionary<string, string> MetaSurveyTokens { get; set; }

        /// <summary>
        /// Custom Index Based on NPS Segments
        /// </summary>
        public Dictionary<string, NPSComposite> NPSCompositeIndices { get; set; }

        /// <summary>
        /// Post To This URL On Questions Change(via Dashboard/API)
        /// </summary>
        public string OnChangeQuestionsURL { get; set; }

        /// <summary>
        /// Post To This URL On Ticket Change(via Dashboard/API)
        /// </summary>
        public string OnChangeTicketsURL { get; set; }

        /// <summary>
        /// Account Settings
        /// </summary>
        public ProgramSettings Account { get; set; }
    }

    /// <summary>
    /// Account Settings
    /// </summary>
    public class ProgramSettings
    {
        /// <summary>
        /// List of Locations
        /// </summary>
        public List<string> Locations { get; set; }
        /// <summary>
        /// TouchPoints mapped against Locations
        /// </summary>
        public List<KeyValuePair<string, List<string>>> TouchPoints { get; set; }
        /// <summary>
        /// Zones mapped against Locations
        /// </summary>
        public List<KeyValuePair<string, List<string>>> Zones { get; set; }
        /// <summary>
        /// List of Questions to be Retire/Enable By Tag
        /// </summary>
        public List<KeyValuePair<string, bool>> RetireQuestionsByTag { get; set; }
        /// <summary>
        /// Tags for the Sentiment Question
        /// </summary>
        public List<string> SentimentQuestionTags { get; set; }
        /// <summary>
        /// Tags for the Theme Question
        /// </summary>
        public List<string> ThemeQuestionTags { get; set; }
        /// <summary>
        /// List Custom Key and Values
        /// </summary>
        public List<KeyValuePair<string, string>> KeyValues { get; set; }
        /// <summary>
        /// List of Feature Flags
        /// </summary>
        public List<KeyValuePair<string, bool>> FeatureFlags { get; set; }
        /// <summary>
        /// Customer Journeys
        /// </summary>
        public List<Journey> Journeys { get; set; }
        /// <summary>
        /// List of Analytics Group
        /// </summary>
        public List<AnalyticsGroup> AnalyticsGroups { get; set; }
        /// <summary>
        /// List of Analytics Group Mapped against Analytics Type Name eg: "CDM": ["group1","group2"]
        /// </summary>
        public List<KeyValuePair<string, List<string>>> AnalyticsGroupMapping { get; set; }
    }

    /// <summary>
    /// Customer Journey Maps
    /// </summary>
    public class Journey
    {
        /// <summary>
        /// Display Id for Sequence or Display Selection
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Journey name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// DataSet template(ex: time, location) for applying onto all stage filter
        /// </summary>
        public FilterBy DataSetTemplate { get; set; } // Apply DaysAgo Offset From MyView

        /// <summary>
        /// Various Stages in the Journey
        /// </summary>
        public List<JourneyStage> Stages { get; set; }
    }

    /// <summary>
    /// Stage within a Journey
    /// </summary>
    public class JourneyStage
    {
        /// <summary>
        /// Stage Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Description of Stage
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Combine Filters w/ Same Group
        /// </summary>
        public string Group { get; set; }

        /// <summary>
        /// Display Order Sequence
        /// </summary>
        public int Sequence { get; set; }

        /// <summary>
        /// Date slice filter for analytics, use Parent Template to override 
        /// </summary>
        public FilterBy DataSet { get; set; } // Apply DaysAgo Offset From MyView
    }

    /// <summary>
    /// Analytics Group
    /// </summary>
    public class AnalyticsGroup
    {
        /// <summary>
        /// Identifier
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Criterion QuestionId or Tag
        /// </summary>
        public List<string> CriterionQuestionIdsOrTag { get; set; }
        /// <summary>
        /// List of PredictionId or Tag
        /// </summary>
        public List<string> PredictorQuestionIdsOrTags { get; set; }
        /// <summary>
        /// Question Id with it's weight age
        /// </summary>
        public Dictionary<string, double> Weightage { get; set; }
        /// <summary>
        /// User Defined Metric for analytics group
        /// </summary>
        public UserDefinedMetric CustomMetric { get; set; }
    }

    /// <summary>
    /// Enterprise Role Definition
    /// </summary>
    public class EnterpriseRole
    {
        /// <summary>
        /// Role Name (ex: ARM, RM, Store Manager, VP, CEO)
        /// </summary>
        public string Name { get; set; } // 

        /// <summary>
        /// Permissions Set Provisioned
        /// </summary>
        public EnterprisePermission Permissions { get; set; }

        /// <summary>
        /// Marked as CallCenter Agent for Call Center API
        /// </summary>
        public bool IsCallAgent { get; set; } // 

        /// <summary>
        /// Role is marked as First Action User to automatically receive tickets sent to this department without a specific user set
        /// </summary>
        public bool IsFirstActionUser { get; set; }

        /// <summary>
        /// Marked as Admin for Department(Manage Subusers within department)
        /// </summary>
        public bool IsDepartmentAdmin { get; set; } // 
    }

    /// <summary>
    /// Department Definition In a Enterprise
    /// </summary>
    public class Department
    {
        /// <summary>
        /// Department Name(ex: Marketing, Product Development)
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Permissions Set Provisioned
        /// </summary>
        public EnterprisePermission Permissions { get; set; }
        /// <summary>
        /// Marked as CallCenter Agent for Call Center API
        /// </summary>
        public bool IsCallCenter { get; set; } // Shortcut for settings permissions for outsourced call center

        /// <summary>
        /// Focus Area for Department (ex: Product, Range, Branding)
        /// </summary>
        public string Theme { get; set; }

        /// <summary>
        /// Sequence Of Ticket Turn Around Times in Hours 3 Days = 72hrs
        /// </summary>
        public List<int> TicketTATHours { get; set; }

        /// <summary>
        /// Detail TroubleShooting/Checklist Survey Token
        /// </summary>
        public string DiagnosticSurveyToken { get; set; }
    }

    /// <summary>
    /// Enterprise Permission Definition
    /// </summary>
    public class EnterprisePermission
    {
        /// <summary>
        /// Apply Below Restrictions Within the Accessible Locations
        /// </summary>
        public bool isRestricted { get; set; } // 

        /// <summary>
        /// Excel, PDF, None
        /// </summary>
        public List<string> AllowedDownloads { get; set; }

        /// <summary>
        /// // Can not Compose Team Message(View Only)
        /// </summary>
        public bool CanNotComposeTeamMessages { get; set; }

        /// <summary>
        /// Can not edit Views
        /// </summary>
        public bool CanNotEditViews { get; set; }

        /// <summary>
        /// Latest Days Visible ( 30 Days = Latest 1 Month Filter)
        /// </summary>
        public int MaxDataDays { get; set; }

        //Tabs
        /// <summary>
        /// Tabs List Ex: Dashboard, Insights, Compare, Questionnaire, Responses, Settings, Notifications, Account, Users, Tokens, Billing, None
        /// </summary>
        public List<string> AllowedTabs { get; set; }

        //Dashboard
        /// <summary>
        /// List Ex: NPS, Delight, Like, MyViews, Historic, Locations, Comments
        /// </summary>
        public List<string> AllowedInDashboard { get; set; }

        /// <summary>
        /// Response for Questions(Allowed to be Seen)
        /// </summary>
        public List<string> AllowedQuestions { get; set; } // QuestionIDs allowed to be seen

        /// <summary>
        /// When Set, will restrict users in this department to these locations
        /// </summary>
        public List<string> AllowedLocations { get; set; } // 

        /// <summary>
        /// When Set, will restrict users in this department to these locations(by tag)
        /// </summary>
        public List<string> AllowedLocationsByTag { get; set; } // By Tag
    }

    /// <summary>
    /// Custom SMTP(Mail) Server To Improve Email Deliverability
    /// </summary>
    public class SMTPServer
    {
        /// <summary>
        /// ex: Your Company Name
        /// </summary>
        public string FromName { get; set; }

        /// <summary>
        /// ex: address@yourserver.net
        /// </summary>
        public string FromAddress { get; set; }

        /// <summary>
        /// ex: smtp.yoursever.net
        /// </summary>
        public string Server { get; set; }

        /// <summary>
        /// Usually address@yourserver.net
        /// </summary>
        public string Login { get; set; }

        /// <summary>
        /// Password to send email
        /// </summary>
        public string Password { get; set; }

        /// <summary>
        /// Ex: 587(Submission), 25(Classic SMTP)
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Set to require Secure SSL Connection
        /// </summary>
        public bool EnableSSL { get; set; }

        public override string ToString()
        {
            string text = Server + ":" + Port + " SSL:" + EnableSSL + " Login:" + Login + " From:" + FromAddress;
            return text;
        }
    }

    /// <summary>
    /// Custom Questionnaire Element Styles
    /// </summary>
    public class TextStyle
    {
        /// <summary>
        /// Element Name (ex: Welcome, Thankyou, Question, Choice, Disclaimer)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Font Name (ex: Ariel)
        /// </summary>
        public string FontName { get; set; }

        /// <summary>
        /// Ex: 10, 11, 12
        /// </summary>
        public string FontSize { get; set; }

        /// <summary>
        /// Ex: Italics, Bold, Underline
        /// </summary>
        public string FontStyle { get; set; }
        /// <summary>
        /// Color Code in Hex ( ex: #FFFFFF for White)
        /// </summary>
        public string FontColor { get; set; }
    }

    /// <summary>
    /// SubUser Enterprise Security Policy 
    /// </summary>
    public class SubUserSecurityPolicy
    {

        #region Session
        /// <summary>
        /// UI hint for logout in minutes
        /// </summary>
        public int LogoutAfterInactiveMinutes { get; set; }
        /// <summary>
        /// Lock account after failed login attempts
        /// </summary>
        public int LockAfterFailedLoginAttempts { get; set; }
        #endregion

        #region Valid Password Pattern
        /// <summary>
        /// Min Length of Password
        /// </summary>
        public int PasswordMinLength { get; set; }
        /// <summary>
        /// # of numbers needed
        /// </summary>
        public int PasswordMinNumbers { get; set; }
        /// <summary>
        /// # of symbols(non-alphabets non-numbers) needed
        /// </summary>
        public int PasswordMinSymbols { get; set; }
        /// <summary>
        /// # of UPPER CASE chars needed
        /// </summary>
        public int PasswordMinUppercase { get; set; }
        /// <summary>
        /// # of lower case chars needed
        /// </summary>
        public int PasswordMinLowercase { get; set; }
        #endregion
    }

    // <summary>
    /// Real-time Targeted Notification On Responses Collected
    /// </summary>
    public class Notification
    {
        /// <summary>
        /// Short Name for this match ( ex: Promoters When Filter Set to Match NPS > 8)
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Filter w/ Full Conditional Matching Including Or/And
        /// </summary>
        public FilterBy ConditionalFilter { get; set; }

        /// <summary>
        /// staff@outlet.com or advanced ticket:// freshdesk:// and many more
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Multiple Email Addresses
        /// </summary>
        public List<string> AdditionalRecipients { get; set; }

        /// <summary>
        /// RealTime or Digest
        /// </summary>
        public string Mode { get; set; }

        /// <summary>
        /// Set to match only on these Days(ex: Mon, Tue, Wed, Thu, Fri, Sat, Sun)
        /// </summary>
        public List<DayOfWeek> Days { get; set; }


        /// <summary>
        /// Match only for these Locations
        /// </summary>
        public List<String> Location { get; set; }

        /// <summary>
        /// Send only these question in email/sms
        /// </summary>
        public List<String> OnlyQuestions { get; set; }

        /// <summary>
        /// Email Customer with this subject
        /// </summary>
        public string MessageSubject { get; set; } // Email Subject
        /// <summary>
        /// Email Customer with this Body
        /// </summary>
        public string MessageContent { get; set; } // Email/SMS Body

        /// <summary>
        /// Log this Notification for Display in a My View
        /// </summary>
        public bool LogNotification { get; set; } // Save for Dashboard Display 

    }

    /// <summary>
    /// Logged Notification 
    /// </summary>
    public class LoggedNotification
    {
        public string Id { get; set; } // API Assigned
        /// <summary>
        /// Account
        /// </summary>
        public string User { get; set; } // User

        /// <summary>
        /// Triggered When
        /// </summary>
        public DateTime DateTime { get; set; }

        /// <summary>
        /// Trigger Match
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Location
        /// </summary>
        public string ResponseLocation { get; set; }

        /// <summary>
        /// Trigered for Response, On click call /api/Answer/{id} for complete response
        /// </summary>
        public string ResponseId { get; set; }

        /// <summary>
        /// Set if Archived
        /// </summary>
        public bool Archived { get; set; }
    }

    public class NotificationBody
    {
        public string Notification { get; set; }
        public Answer Answer { get; set; }
    }

    /// <summary>
    /// Drill Down Filter to slice by location/date/time/answer and more
    /// </summary>
    public class FilterBy
    {
        /// <summary>
        /// Location to slice data set with
        /// </summary>
        public List<string> location { get; set; }

        /// <summary>
        /// Responses collected after this date/time
        /// </summary>
        public DateTime? afterdate { get; set; }
        /// <summary>
        /// Responses collected before this data/time
        /// </summary>
        public DateTime? beforedate { get; set; }

        /// <summary>
        /// Match answers to these questions
        /// </summary>
        public List<FilterByQuestions> filterquestions { get; set; }

        /// <summary>
        /// Only Archived
        /// </summary>
        public bool archived { get; set; }

        /// <summary>
        /// Optional Channel Survey Client Agent Signature(ex: JS-Web, 'voice', 'email', 'tab' or 'none' for none specified)
        /// </summary>
        public string channel { get; set; }

        /// <summary>
        /// Contains Ticket
        /// </summary>
        public bool withTickets { get; set; }

        /// <summary>
        /// Contains Ticket w/ Status
        /// </summary>
        public string withTicketStatus { get; set; }

        /// <summary>
        /// Contains Notes
        /// </summary>
        public bool withNotes { get; set; }

        /// <summary>
        /// Audio Video, Document
        /// </summary>
        public string notesWithAttachmentType { get; set; }

        /// <summary>
        /// Media is set to theme
        /// </summary>
        public string notesMediaTheme { get; set; }

        /// <summary>
        /// Media is set to outcome(mood)
        /// </summary>
        public string notesMediaMood { get; set; }

        /// <summary>
        /// Contains Attachments
        /// </summary>
        public bool onlyWithAttachments { get; set; }

        /// <summary>
        /// Match only for these days ( ex: Sun)
        /// </summary>
        public List<DayOfWeek> days { get; set; }

        /// <summary>
        /// Match only after this time of day
        /// </summary>
        public DateTime? aftertime { get; set; }
        /// <summary>
        /// Match only before this time of day
        /// </summary>
        public DateTime? beforetime { get; set; }

        public override string ToString()
        {
            string formatted = archived + "|" + (days != null ? string.Join("-", days) : "") + "|" + (aftertime.HasValue ? aftertime.Value.ToString("HH:mm") : "") + "-" + (beforetime.HasValue ? beforetime.Value.ToString("HH:mm") : "")
                + "|" + (location != null ? string.Join("-", location) : "") + "|"
                + (filterquestions != null && filterquestions.Count > 0 ? string.Join("-", filterquestions) : "") + "|"
                + (afterdate.HasValue ? afterdate.Value.ToString("ddMMyy") : "") + "-" + (beforedate.HasValue ? beforedate.Value.ToString("ddMMyy") : "") + "|"
                + withNotes + "|" + notesMediaMood + "|" + notesMediaTheme + "|" + onlyWithAttachments + "|"
                + (aftertime.HasValue ? aftertime.Value.ToString("HH:mm") : "") + "-" + (beforetime.HasValue ? beforetime.Value.ToString("HH:mm") : "") + "-" + channel;

            if (withTickets && Ticket != null)
                formatted += "-" + Ticket.Status + "|" + Ticket.AssignedBy + "|" + Ticket.Action + "|" + Ticket.Department + "|" + Ticket.OrginalRoutedTo + "|" + Ticket.CurrentAssignedTo + "|" + Ticket.IsEscalated + "|" + Ticket.IsDeferred + "|" + Ticket.IsPendingRating;

            return formatted;
        }

        /// <summary>
        /// Optionally Ticket Filter
        /// </summary>
        public TicketFilter Ticket { get; set; }

        /// <summary>
        /// Optional Reference Name for This Filter
        /// </summary>
        public string FilterName { get; set; }
        /// <summary>
        /// filterout possible fake responses
        /// only applies to the responses which has deviceid in it
        /// </summary>
        public bool IgnoreOutliers { get; set; }

        /// <summary>
        /// combination of filters from the existing filters
        /// Ex. both Archive and unarchived
        /// </summary>

        public MashupFilter Mashup { get; set; }

    }

    public class MashupFilter
    {
        public bool withArchived { get; set; }
        public bool onlyOutliers { get; set; }
    }

    /// <summary>
    /// Match Response to a Question
    /// </summary>
    public class FilterByQuestions
    {
        /// <summary>
        /// Question Id to Match
        /// </summary>
        public string QuestionId { get; set; }

        /// <summary>
        /// Answer to Match, Number("gt", "lt", "eq") or Text("Lunch", "Lunch,Dinner")
        /// </summary>
        public List<String> AnswerCheck { get; set; }

        /// <summary>
        /// Number Input to Match
        /// </summary>
        public int Number { get; set; }

        public override string ToString()
        {
            string formatted = QuestionId + "|" + (AnswerCheck != null && AnswerCheck.Count != 0 ? string.Join("-", AnswerCheck) : "") + "|" + Number;
            return formatted;
        }

        /// <summary>
        /// "AND"(match all group) / "OR"(match any group)
        /// </summary>
        public String GroupBy { get; set; }
    }

    /// <summary>
    /// Drill Down Ticket Filter
    /// </summary>
    public class TicketFilter
    {
        /// <summary>
        /// Open, Resolved, Declined, Closed
        /// </summary>
        public string Status { get; set; }
        /// <summary>
        /// Ticket Opener
        /// </summary>
        public string AssignedBy { get; set; }
        /// <summary>
        /// ex: Call, Info, Action, Respond
        /// </summary>
        public string Action { get; set; }

        /// <summary>
        /// Ticket Assigned to Department
        /// </summary>
        public string Department { get; set; }

        /// <summary>
        /// Language(Assigned or looked-up from Language Q)
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Ticket First Responder (FAR)
        /// </summary>
        public string OrginalRoutedTo { get; set; }

        /// <summary>
        /// If not Closed, Assign ticket to this user
        /// </summary>
        public string NextEscalationUser { get; set; }

        /// <summary>
        /// Ticket Assignee 
        /// </summary>
        public string CurrentAssignedTo { get; set; }

        /// <summary>
        /// Higher Priority for Larger Numbers
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Is Escalated(FAR has not responded)
        /// </summary>
        public bool IsEscalated { get; set; }

        /// <summary>
        /// Is Rescheduled/Scheduled/Deferred for Later
        /// </summary>
        public bool IsDeferred { get; set; }

        /// <summary>
        /// Closed Ticket, Is Pending Review and Rating
        /// </summary>
        public bool IsPendingRating { get; set; }

        /// <summary>
        /// Has Been Reviewed and Rated
        /// </summary>
        public bool IsRated { get; set; }

        /// <summary>
        /// Is Set to be showcased(sticky) within department
        /// </summary>
        public bool IsShowcased { get; set; }

        /// <summary>
        /// Is Set to be showcased(sticky) across enterprise
        /// </summary>
        public bool IsGlobalShowcased { get; set; }

        //[Obsolete]
        public override string ToString()
        {
            string filterstr = base.ToString();
            string filterticketstr = Status + "|" + AssignedBy + "|" + Action + "|" + Department + "|" + OrginalRoutedTo + "|" + CurrentAssignedTo + "|" + IsEscalated + "|" + IsDeferred + "|" + IsPendingRating;
            return filterticketstr + "-" + filterstr;
        }
    }

    /// <summary>
    /// User Preference Including My Views, My Reports and more
    /// </summary>
    public class UserPreference
    {
        public string Id { get; set; } // username

        /// <summary>
        /// In Days, ex: 30 Days, 7 Days
        /// </summary>
        public int DefaultFilterDuration { get; set; }
        /// <summary>
        /// Set when filter required
        /// </summary>
        public int DefaultFilterLocation { get; set; }

        /// <summary>
        /// Set when filter required
        /// </summary>
        public int DefaultFilterLocationTag { get; set; }

        /// <summary>
        /// Custom Views Per User(Dashboard), Custom Report Per User(PDF)
        /// </summary>
        public List<MyView> Views { get; set; }

        /// <summary>
        /// Per User Notifications
        /// </summary>
        public List<Notification> Notifications { get; set; }

        /// <summary>
        /// Account Level User Data
        /// </summary>
        public Dictionary<string, string> PreferenceKeyValue { get; set; }

        /// <summary>
        /// Notify When Ticket is Assigned/Changes
        /// </summary>
        public bool NotifyOnMyTicketChanges { get; set; }
    }

    /// <summary>
    /// Personalized View for Each User of CEM
    /// </summary>
    public class MyView
    {
        /// <summary>
        /// ex: Marketing, Level 1, Level 2
        /// </summary>
        public string ViewName { get; set; } // ex: Marketing, Level 1, Level 2
        /// <summary>
        /// Only Users who rated 1 on overall exp
        /// </summary>
        public FilterBy ViewFilter { get; set; }

        /// <summary>
        /// Display Ticket Comment Cloud when set
        /// </summary>
        public bool DisplayTicketCommentCloud { get; set; }

        /// <summary>
        /// Display this URL as iFrame
        /// </summary>
        public string IFrameURL { get; set; }
        /// <summary>
        /// Display iFrame Height w/o Scroll
        /// </summary>
        public string IFrameHeight { get; set; }

        /// <summary>
        /// For Active Display
        /// </summary>
        public bool IsDisplayed { get; set; }

        /// <summary>
        /// Mini version of sections where possible
        /// </summary>
        public bool Minify { get; set; }

        /// <summary>
        /// Desktop with rest of views as a section
        /// </summary>
        public bool ViewsInView { get; set; }

        /// <summary>
        /// Sort lower # to first to display
        /// </summary>
        public int DisplaySequence { get; set; }

        /// <summary>
        /// For Scheduled Reports
        /// </summary>
        public bool IsReported { get; set; }

        /// <summary>
        /// For One-Time Requested For Report
        /// </summary>
        public bool IsOneTimeReported { get; set; }

        /// <summary>
        /// Optionally Mark Exports and Reports are PDF Only
        /// </summary>
        public bool PDFOnly { get; set; }

        /// <summary>
        /// Question ID to display as donuts
        /// </summary>
        public List<string> SelectedQuestion { get; set; }

        /// <summary>
        /// Question ID Pair to Compare
        /// </summary>
        public List<KeyValuePair<string, string>> SelectedCompareQuestion { get; set; }

        /// <summary>
        /// Compare across these locations/tags
        /// </summary>
        public List<LocationOrTag> SelectedCompareLocationOrTag { get; set; }

        /// <summary>
        /// Multiple Day Ago time frames
        /// </summary>
        public List<int> DaysAgoOffsets { get; set; }

        /// <summary>
        /// Display NPS Box
        /// </summary>
        public bool DisplayNPS { get; set; }

        /// <summary>
        /// Display NPS Benchmark in short format
        /// </summary>
        public bool DisplayNPSBenchmark { get; set; }

        /// <summary>
        /// Display MDM in small format
        /// </summary>
        public bool DisplayMDM { get; set; }

        /// <summary>
        /// Display WordCloud in small format
        /// </summary>
        public bool DisplayWordCloud { get; set; }

        /// <summary>
        /// Display Responses # Stat
        /// </summary>
        public bool DisplayRespStat { get; set; }

        /// <summary>
        /// Display Calendar Heat Map
        /// </summary>
        public bool DisplayCalHeatMap { get; set; }

        /// <summary>
        /// Display Liked/Disliked in small format
        /// </summary>
        public bool DisplayLiked { get; set; }

        /// <summary>
        /// Display the GeoMap
        /// </summary>
        public bool DisplayMap { get; set; }

        /// <summary>
        /// Display this many comments(zero = do not display this section)
        /// </summary>
        public int DisplayComments { get; set; }

        /// <summary>
        /// Display tickets
        /// </summary>
        public bool DisplayTickets { get; set; }

        /// <summary>
        /// Display tickets marked showcase
        /// </summary>
        public bool DisplayTicketShowCase { get; set; }

        /// <summary>
        /// Display Media Cloud when set to Audio/Video/Documents
        /// </summary>
        public string DisplayMediaCloud { get; set; }

        /// <summary>
        /// View Data, Loaded Efficiently Once
        /// </summary>
        public Dictionary<string, string> UserData { get; set; }

        /// <summary>
        /// Display MessageBox
        /// </summary>
        public bool DisplayMessageBox { get; set; }

        /// <summary>
        /// Tags to Filter Messages from MessageBox
        /// </summary>
        public List<string> MessageBoxFilterTags { get; set; }

        /// <summary>
        /// Shown modal with this token survey when set
        /// </summary>
        public string onLoadTokenSurvey { get; set; }

        /// <summary>
        /// Display Logged Notifications
        /// </summary>
        public bool DisplayLoggedNotifications { get; set; }

        /// <summary>
        /// Display raw responses paged
        /// </summary>
        public bool DisplayRawResponses { get; set; }

        /// <summary>
        /// Display historic view
        /// </summary>
        public int DisplayHistoricView { get; set; }


        /// <summary>
        ///  Report Filter Duration For My Reports (Last x days)
        /// </summary>
        public int ReportFilterDuration { get; set; }

        /// <summary>
        /// Note/Intro from user printed on top of report
        /// </summary>
        public string ReportNote { get; set; }

        /// <summary>
        /// Optional Report Style
        /// </summary>
        public string ReportStyle { get; set; }

        /// <summary>
        /// Hour, Day, DayOfWeek, Month, Quarter
        /// </summary>
        public string Every { get; set; }

        /// <summary>
        /// 0-23 Preferred Hour in UTC for Report Trigger, ex: 23 for 11 PM
        /// </summary>
        public int HourlyTrigger { get; set; }

        /// <summary>
        /// Mon, Tue, Web, Thu, Fri, Sat, Sun
        /// </summary>
        public string DayTrigger { get; set; }

        /// <summary>
        /// Optionally Post Report to MessageBox with this Tag for all team to view
        /// </summary>
        public string PostReportToMessageBoxTag { get; set; }

        /// <summary>
        /// Optional Locations to set for Posting Report to MessageBox
        /// </summary>
        public List<string> PostReportToMessageBoxLocations { get; set; }

        /// <summary>
        /// Additional Reports for Team Members ( email => optional location filter)
        /// </summary>
        public List<ReportRecipent> TeamReportRecipients { get; set; }

        /// <summary>
        /// View Level User Data
        /// </summary>
        public Dictionary<string, string> PreferenceKeyValue { get; set; }

        /// <summary>
        /// Account Wide Global Syndicated Views Syndication By Primary Account
        /// </summary>
        public SyndicatedView GlobalSyndicated { get; set; }

        public override string ToString()
        {
            string formatted = ViewName + "-" + ViewFilter?.ToString() + "-" + ReportFilterDuration + "-" + ReportNote + "-" + HourlyTrigger + "-" + DayTrigger;
            return formatted;
        }
    }

    /// <summary>
    /// View is Syndicated to a large group, sliced by user or department or role
    /// </summary>
    public class SyndicatedView
    {
        /// <summary>
        /// Revision # from 1 to 100000, existing views syndicated are refreshed upon increment to latest
        /// </summary>
        public int Revision { get; set; }

        /// <summary>
        /// Optional List of users who can access this view (or any)
        /// </summary>
        public List<string> Users { get; set; }

        /// <summary>
        /// Optional list of enterprise departments
        /// </summary>
        public List<string> Departments { get; set; }

        /// <summary>
        /// Optional list of enterprise roles 
        /// </summary>
        public List<string> EnterpriseRoles { get; set; }

        /// <summary>
        /// Optional list of enterprise regions
        /// </summary>
        public List<string> EnterpriseRegions { get; set; }
    }

    /// <summary>
    /// Additional Report Recipients
    /// </summary>
    public class ReportRecipent
    {
        /// <summary>
        /// Email Address
        /// </summary>
        public string Email { get; set; }

        /// <summary>
        /// Optional Location
        /// </summary>
        public List<string> Locations { get; set; }
    }

    /// <summary>
    /// Widget Configuration
    /// </summary>
    public class Widget
    {
        /// <summary>
        /// Id for the widget
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// Name of the account
        /// </summary>
        public string User { get; set; }
        /// <summary>
        /// Name of the widget
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// Description of the widget
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// Type for this widget (eg: basic, composite)
        /// </summary>
        public string Type { get; set; }
        /// <summary>
        /// Layout Type for this widget fro Display (eg: Horizontal)
        /// </summary>
        public string LayoutType { get; set; }
        /// <summary>
        /// Widget units that belong to this widget
        /// </summary>
        public List<string> WidgetUnitIds { get; set; }
        /// <summary>
        /// When was it modified last time
        /// </summary>
        public DateTime? LastModified { get; set; }
        /// <summary>
        /// Height of widget in Percentage
        /// </summary>
        public string WidgetHeight { get; set; }
        /// <summary>
        /// Specifies whether is enabled or disabled
        /// </summary>
        public bool IsRetired { get; set; }
        /// <summary>
        /// Readable Identifiers
        /// </summary>
        public List<string> Tags { get; set; }
    }

    /// <summary>
    /// Widget Unit Configuration
    /// </summary>
    public class WidgetUnit
    {
        /// <summary>
        /// ID for the widget unit
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        ///Group ID for the widget unit
        /// </summary>
        public string GroupId { get; set; }
        /// <summary>
        /// Name of the account
        /// </summary>
        public string User { get; set; }
        /// <summary>
        /// Name of the widget unit
        /// </summary>
        public string Title { get; set; }
        /// <summary>
        /// Description of the WidgetUnit
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// List of question ids
        /// </summary>
        public List<string> Rows { get; set; }
        /// <summary>
        /// List of question ids
        /// </summary>
        public List<string> Columns { get; set; }
        /// <summary>
        /// Visualization type
        /// </summary>
        public string RendererName { get; set; }
        /// <summary>
        /// Aggregator type
        /// </summary>
        public string Aggregator { get; set; }
        /// <summary>
        /// Options to exclude
        /// </summary>
        public Dictionary<string, List<string>> OptionsToExclude { get; set; }
        /// <summary>
        /// Question Ids for Aggregation Input
        /// </summary>
        public List<string> AggregatorQuestionIDs { get; set; }
        /// <summary>
        /// Values for Aggregation Input
        /// </summary>
        public List<int> AggregatorValues { get; set; }
        /// <summary>
        /// Specifies whether to format number with decimal
        /// </summary>
        public bool ShouldFormatDecimalPlaces { get; set; }
        /// <summary>
        /// Number of decimal places to be returned if ShouldFormatDecimalPlaces is enabled
        /// </summary>
        public int DecimalPlacesCount { get; set; }
        /// <summary>
        /// When was it modified last time
        /// </summary>
        public DateTime? LastModified { get; set; }
        /// <summary>
        /// In Days, ex: 30 Days, 7 Days
        /// Overrides filter for Widget Units which are based on time comparision
        /// </summary>
        public int DefaultFilterDuration { get; set; }
    }

    /// <summary>
    /// Filter for Widget Summary
    /// </summary>
    public class WidgetFilter
    {
        /// <summary>
        /// List of Widget Configurations
        /// </summary>
        public List<WidgetUnit> Widgets { get; set; }
        /// <summary>
        /// Input Filter
        /// </summary>
        public FilterBy Filter { get; set; }
    }

    /// <summary>
    /// Summary with Rows and Columns in tree structure
    /// </summary>
    public class SummaryTreeNode
    {
        [JsonIgnore]
        public double? Index { get; set; }
        /// <summary>
        /// Unique Identifier for the node
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// Data to be displayed
        /// </summary>
        public string Data { get; set; }
        /// <summary>
        /// PVal if 1 Sample TTest available
        /// </summary>
        public double? PValue { get; set; }
        /// <summary>
        /// TStat if 1 Sample TTest available
        /// </summary>
        public double? TStat { get; set; }
        /// <summary>
        /// PVals if 2 Sample TTest available
        /// </summary>
        public Dictionary<string, double> PValues { get; set; }
        /// <summary>
        /// TStats if 2 Sample TTest available
        /// </summary>
        public Dictionary<string, double> TStats { get; set; }
        /// <summary>
        /// Specifies whether it's a Row
        /// </summary>
        public bool IsRow { get; set; }
        /// <summary>
        /// Specifies whether it's a Column
        /// </summary>
        public bool IsColumn { get; set; }
        /// <summary>
        /// Children of the Node
        /// </summary>
        public List<SummaryTreeNode> Children;

        /// <summary>
        /// Add Child to the node if it exists
        /// </summary>
        /// <param name="childNode">Node to be added</param>
        /// <param name="ignoreExisting">Don't Check for existing child, just add it</param>
        /// <returns>Reference for the Added Child</returns>
        public SummaryTreeNode AddChild(SummaryTreeNode childNode, bool ignoreExisting = false)
        {
            SummaryTreeNode existingChild = null;
            if (!ignoreExisting)
                existingChild = Children?.FirstOrDefault(x => x.Data == childNode?.Data);
            if (existingChild == null) // checking if the child is already present
            {
                if (Children == null)
                    Children = new List<SummaryTreeNode>();
                Children.Add(childNode);
                return childNode;
            }
            else
                return existingChild;
        }
    }

    /// <summary>
    /// Visualization summary for a widgets
    /// </summary>
    public class VisualizationSummary
    {
        /// <summary>
        /// Summary by Key and Value
        /// </summary>
        public List<SummaryByOption> Summary { get; set; }
        /// <summary>
        /// Summary in tree structure
        /// </summary>
        public List<SummaryTreeNode> SummaryTree { get; set; }
    }

    /// <summary>
    /// Summary for all requested widgets
    /// </summary>
    public class WidgetSummaryVisualization
    {
        /// <summary>
        /// Visualization Summary By Widget
        /// </summary>
        public Dictionary<string, VisualizationSummary> VisualizationSummary { get; set; }
        /// <summary>
        /// Total Count of Answers
        /// </summary>
        public int TotalCount { get; set; }
        /// <summary>
        /// SampleSize if used
        /// </summary>
        public int SampleSize { get; set; }
        /// <summary>
        /// Additional messages by widget
        /// </summary>
        public Dictionary<string, string> Messages { get; set; }
        /// <summary>
        /// Answers count by widget
        /// </summary>
        public Dictionary<string, int> TotalCountByWidget { get; set; }
    }

    public class WidgetSummary
    {
        public List<SummaryByOption> Summary { get; set; }
        public int TotalCount { get; set; }
        public int SampleSize { get; set; }
        public Dictionary<string, string> Messages { get; set; }
        public Dictionary<string, int> TotalCountByWidget { get; set; }
    }

    public class SummaryByOption
    {
        public SummaryByOptionID Id { get; set; }
        public SummaryByValue value { get; set; }
    }

    public class SummaryByValue
    {
        public double? Score { get; set; }
        public int Count { get; set; }
        public Dictionary<int, int> DistributedCount { get; set; }
        //One-Sample Test Details
        public double? PValue { get; set; }
        public double? TStat { get; set; }
        //Two-Sample Test Details
        public Dictionary<string, double> PValues { get; set; }
        public Dictionary<string, double> TStats { get; set; }
    }

    public class SummaryByOptionID
    {
        public string Key { get; set; }
        public string GroupKey { get; set; }
        public List<string> Headers { get; set; }
        public List<Response> Responses { get; set; }
        public string Row { get; set; }
        public string Column { get; set; }
        public string RowValues { get; set; }
        public string ColumnValues { get; set; }
        public bool IsGrandTotal { get; set; }
        public bool IsRowTotal { get; set; }
        public bool IsColumnTotal { get; set; }
    }

    /// <summary>
    /// Question Extra Attributes
    /// </summary>
    public class QuestionExtraAttributes
    {
        /// <summary>
        /// Object Id
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// Main User
        /// </summary>
        public string User { get; set; }
        /// <summary>
        /// Key to save attributes against (Eg: SalesForce, Marketo)
        /// </summary>
        public string Key { get; set; }
        /// <summary>
        /// Display Name for the user
        /// </summary>
        public string DisplayName { get; set; }
        /// <summary>
        /// Optional additional description
        /// </summary>
        public string Description { get; set; }
        /// <summary>
        /// QuestionId Mappings of CC --> External
        /// </summary>
        public Dictionary<string, string> QuestionIdMappings { get; set; }
    }

    public class UserData
    {
        public string Value { get; set; }
    }

    public class IdentityUser
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string ConfirmPassword { get; set; }
        public string Name { get; set; }
    }
}