using CreateResponseExcel.Helper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ImportResponsesInCC
{
    class ReadExcel
    {
        private HttpClient httpClient;
        public ReadExcel()
        {
            httpClient = new HttpClient();
        }

        public async Task<HttpResponseMessage> ImportExcelAsync(string username, string password)
        {
            try
            {
                Logger.Info("Calling access token cc api.");
                string folderPath = ConfigurationManager.AppSettings["FilePath"];
                string access_token = await Authentication.AccessLoginToken(username, password);
                string apiendpoint = ConfigurationManager.AppSettings["CCAPIEndPoint"] + "api/importexcelresponses";
                var file_bytes = File.ReadAllBytes(folderPath);
                MultipartFormDataContent form = new MultipartFormDataContent
                {
                    { new ByteArrayContent(file_bytes, 0, file_bytes.Length), "file", "hello1.xlsx" }
                };
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, apiendpoint);
                request.Content = form;
                request.Headers.Add("Authorization", "Bearer " + access_token);
                Logger.Info("calling bulk import api.");
                httpClient.Timeout = TimeSpan.FromMinutes(30);
                var response = await httpClient.SendAsync(request);
                Console.WriteLine("bulk import api response = " + response.ReasonPhrase + " " + response.StatusCode);
                Logger.Info("bulk import api response = " + response.ReasonPhrase + " " + response.StatusCode) ;
                return response;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
