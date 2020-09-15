using CreateSurveyTokenAndSend.Helper;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CreateSurveyTokenAndSend
{
    class TokenCreationAndSend
    {
        // <summary>
        /// Create WXM Survey Token
        /// </summary>
        /// <returns>Token ID</returns>
        public static async Task<string> CreateTokenAndSendSurvey(IConfigurationRoot configuration)
        {
            try
            {
                string url = configuration["CCAPIEndpoint"]; // WXM URL
                string username = configuration["CCAccount"]; // WXM Username
                string password = configuration["CCPassword"]; // WXM Password
                string uri = "/api/RequestInvitation";

                //Adding Prefills
                var prefills = new List<Dictionary<string, object>>()
                {
                    // Values mentioned below are Dummy
                    // Adding Prefills
                    // Should include all the prefiils required to be filled dynamically
                    new Dictionary<string,object>()
                    {
                        { "numberInput" , 0 },
                        {  "questionId" , "5cd155511cdbc51308b1c6c0" },
                        { "questionText" , "Name" },
                        { "textInput" , "John Doe" }
                    },
                    new Dictionary<string,object>()
                    {
                        { "numberInput" , 0 },
                        {  "questionId" , "5cd155511cdbc51308b1c6be" },
                        { "questionText" , "Email" },
                        { "textInput" , "johndoe@dummy.com" }
                    },
                    new Dictionary<string,object>()
                    {
                        { "numberInput" , 0 },
                        {  "questionId" , "5cd155511cdbc51308b1c6bf" },
                        { "questionText" , "Mobile Number" },
                        { "textInput" , "99999999" }
                    }
                };
                // Declaring varaible for survey valid till date.
                DateTime validTill = DateTime.UtcNow.AddDays(int.Parse(configuration["SurveyValidity"]));
                string uniqueQuestionTag = string.Empty;
                string prefillQuestionID = string.Empty;
                string questionTagValue = string.Empty;
                if (bool.Parse(configuration["IsSMS"]))
                {
                    uniqueQuestionTag = "mobile";
                    prefillQuestionID = configuration["MobileNumberQuestionID"];
                    questionTagValue = configuration["CustomerPhone"];
                }
                else
                {
                    uniqueQuestionTag = "email";
                    prefillQuestionID = configuration["EmailQuestionID"];
                    questionTagValue = configuration["CustomerEmail"];
                }
                // Composing Body required to create survey Token. 
                var param = new Dictionary<string, object>
                {
                    { "customerPhone", configuration["CustomerPhone"] }, // Provide customer mobile number
                    { "subject",  configuration["EmailSubject"] }, // Provide Email subject
                    { "customerName", configuration["CustomerName"] },  // Customer Name
                    { "customerEmail", configuration["CustomerEmail"] }, // Provide the CC questionnaire name
                    { "template", configuration["Template"] }, // SMS or email template to be sent.
                    { "isSMS", configuration["IsSMS"] }, // whether SMS needs to be sent or email.
                    { "nonRepeatByUniquieQuestionTag", uniqueQuestionTag }, // Question tag to check throttling.
                    { "nonRepeatWithinLastDays", configuration["ThrottledDays"] }, // Number of throttling days.
                    { "preFillByQuestionId", prefillQuestionID }, // Mobile or email question id based on what is being sent
                    { "preFillByQuestionTag", new Dictionary<string, string> { { uniqueQuestionTag, questionTagValue } } },
                    { "tokenDetails", new Dictionary<string, object>
                        {
                            { "validTill", validTill.ToString() }, // Provide the Survey Expiry Time in UTC format
                            { "validUses", configuration["ValidUses"] },  // Number of times the survey can be submitted
                            { "location", configuration["QuestionnaireID"] }, // Provide the CC questionnaire name
                            { "note", configuration["Note"] }, // Information about the survey creation. Not Mandatory.
                            { "preFill", prefills },
                            { "preferredLanguage", configuration["PreferredLanguage"] }
                        } 
                    }
                };
                string jsonBody = JsonConvert.SerializeObject(param);
                CCAPIClient client = new CCAPIClient(url, username, password);
                var response = await client.SendAsync(uri, jsonBody);
                if (!string.IsNullOrEmpty(response))
                {
                    return response;
                }
                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
    }
}
