using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudCherry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace WXMCustomAPI.Controllers
{
    [ApiController]
    [Route("api")]

    public class AgeGroupPostFillController : ControllerBase
    {
        private readonly IConfiguration ConfigurationManager;
        private readonly APIClient CherryClient;

        public AgeGroupPostFillController(IConfiguration config)
        {
            ConfigurationManager = config;

            var CherryEndPoint = ConfigurationManager["CherryEndPoint"];
            var CherryAccount = ConfigurationManager["CherryAccount"];
            var CherryPassword = ConfigurationManager["CherryPassword"];
            CherryClient = new APIClient(CherryEndPoint, CherryAccount, CherryPassword);
        }

        [HttpPost]
        [Route("Postfill/AgeGroup")]
        public async Task<IActionResult> Postfill(NotificationBody notification)
        {
            return Ok(await UpdateResponse(notification));
        }

        private async Task<bool> UpdateResponse(NotificationBody notification)
        {
            if (notification == null)
                return false;

            var answer = notification.Answer;
            var dictionary = new Dictionary<string, Answer>();

            // Assign an age group based on customer's age
            UpdateAgeGroup(answer);

            dictionary.Add(answer.Id, answer);
            return await CherryClient.UpdateMultiAnswer(dictionary);
        }

        private void UpdateAgeGroup(Answer answer)
        {
            var CustomerAge = ConfigurationManager["CustomersAge"];
            var AgeGroupQuestion = ConfigurationManager["AgeGroup"];

            var aResponse = answer.Responses.FirstOrDefault(x => x.QuestionId == CustomerAge);
            if (aResponse != null && !string.IsNullOrEmpty(aResponse.TextInput))
            {
                double age = Convert.ToDouble(aResponse.TextInput);
                var ageGroupBucket = GetAgeGroup(age);
                answer.Responses.Add(new Response()
                {
                    QuestionId = AgeGroupQuestion,
                    TextInput = ageGroupBucket
                });
            }
        }

        // This function provides a hour slot based on DateTime
        private string GetAgeGroup(double Age)
        {
            if (Age > 0 && Age < 25)
                return AgeBracket.AgeGroup1;
            else if (Age >= 25 && Age < 30)
                return AgeBracket.AgeGroup2;
            else if (Age >= 30 && Age < 35)
                return AgeBracket.AgeGroup3;
            else if (Age >= 35)
                return AgeBracket.AgeGroup4;

            return string.Empty;
        }

        // Age group categorized in 5 slots
        public class AgeBracket
        {
            public static string AgeGroup1 = "Under 25";
            public static string AgeGroup2 = "25-30";
            public static string AgeGroup3 = "30-35";
            public static string AgeGroup4 = "Above 35";
        }

    }
}
