using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SurveyTokenCreation.Helper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SurveyTokenCreation
{
    class SurveyTokenCreation
    {
        /// <summary>
        /// Create WXM Survey Token
        /// </summary>
        /// <returns>Token ID</returns>
        public static async Task<string> CreateToken(IConfigurationRoot configuration)
        {
            try
            {
                string url = configuration["CCAPIEndpoint"]; // WXM URL
                string username = configuration["CCAccount"]; // WXM Username
                string password = configuration["CCPassword"]; // WXM Password

                CCAPIClient client = new CCAPIClient(url, username, password);

                string uri = "/api/surveytoken";

                //Adding Prefills
                var prefills = new List<Dictionary<string, object>>()
                {
                    // Values mentioned below are Dummy
                    // Adding Prefills
                    // Should include all the prefiils required to be filled dynamically
                    new Dictionary<string,object>()
                    {
                        { "numberInput" , 0 },
                        { "questionId" , "5cd155511cdbc51308b1c6c0" },
                        { "questionText" , "Name" },
                        { "textInput" , "John Doe" }
                    },
                    new Dictionary<string,object>()
                    {
                        { "numberInput" , 0 },
                        { "questionId" , "5cd155511cdbc51308b1c6be" },
                        { "questionText" , "Email" },
                        { "textInput" , "johndoe@gmail.com" }
                    }
                };
                // Declaring varaible for survey valid till date.
                DateTime validTill = DateTime.UtcNow.AddDays(int.Parse(configuration["SurveyValidity"]));
                // Composing Body required to create survey Token. 
                var param = new Dictionary<string, object>
                {
                    { "validTill", validTill.ToString() }, // Provide the Survey Expiry Time in UTC format
                    { "validUses", configuration["ValidUses"] },  // Number of times the survey can be submitted
                    { "location", configuration["QuestionnaireID"] }, // Provide the Experience Management questionnaire name
                    { "note", configuration["Note"] }, // Information about the survey creation. Not Mandatory.
                    { "preFill", prefills },
                    { "preferredLanguage", configuration["PreferredLanguage"] } // Prefferred language for survey.
                };
                string jsonBody = JsonConvert.SerializeObject(param);
                
                var response = await client.SendAsync(uri, jsonBody);
                if (!string.IsNullOrEmpty(response))
                {
                    var tokenDetails = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                    tokenDetails.TryGetValue("id", out object token);
                    return token?.ToString();
                }
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }
    }
}
