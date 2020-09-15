using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CloudCherry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace WXMPostfillAPI.Controllers
{
    [ApiController]
    [Route("api")]

    public class AirlinePostFillController : ControllerBase
    {
        private readonly IConfiguration ConfigurationManager;
        private readonly APIClient CherryClient;

        public AirlinePostFillController(IConfiguration config)
        {
            ConfigurationManager = config;

            var CherryEndPoint = ConfigurationManager["CherryEndPoint"];
            var CherryAccount = ConfigurationManager["CherryAccount"];
            var CherryPassword = ConfigurationManager["CherryPassword"];
            CherryClient = new APIClient(CherryEndPoint, CherryAccount, CherryPassword);
        }

        [HttpPost]
        [Route("Postfill/airlines")]
        public async Task<IActionResult> Postfill(NotificationBody notification)
        {
            try
            {
                return Ok(await UpdateResponse(notification));
            }
            catch (Exception)
            {
                return Ok(false);
            }
        }

        private async Task<bool> UpdateResponse(NotificationBody notification)
        {
            try
            {
                if (notification == null)
                    return false;

                var answer = notification.Answer;
                var dictionary = new Dictionary<string, Answer>();

                // 6 Hour Time bucket range for Departure Time
                UpdateDepartureTimeBucket(answer);

                //6 Hour Time bucket range for Arrival Time
                UpdateArrivalTimeBucket(answer);

                //Update if flight is 15 min delay 
                UpdateDelay15Flag(answer);

                dictionary.Add(answer.Id, answer);
                return await CherryClient.UpdateMultiAnswer(dictionary);
            }
            catch(Exception)
            {
                return false;
            }
        }

        private void UpdateArrivalTimeBucket(Answer answer)
        {
            var actualArrivalTime = ConfigurationManager["ActualArrivalTime"];
            var arrivalTimeBucket = ConfigurationManager["ArrivalBucket"];

            var aResponse = answer.Responses.FirstOrDefault(x => x.QuestionId == actualArrivalTime);
            if (aResponse != null && !string.IsNullOrEmpty(aResponse.TextInput))
            {
                DateTime.TryParse(aResponse.TextInput, out DateTime dateTime);

                if (dateTime != DateTime.MinValue)
                {
                    var departureBucket = GetBucketSlot(dateTime);
                    answer.Responses.Add(new Response()
                    {
                        QuestionId = arrivalTimeBucket,
                        TextInput = departureBucket
                    });
                }
            }
        }

        private void UpdateDepartureTimeBucket(Answer answer)
        {
            var actualDepartureTime = ConfigurationManager["DepartureDateTime"];
            var departureTimeBucket = ConfigurationManager["DepartureBucket"];

            //Departure Bucket
            var dResponse = answer.Responses.FirstOrDefault(x => x.QuestionId == actualDepartureTime);
            if (dResponse != null && !string.IsNullOrEmpty(dResponse.TextInput))
            {
                DateTime.TryParse(dResponse.TextInput, out DateTime dateTime);

                if (dateTime != DateTime.MinValue)
                {
                    var departureBucket = GetBucketSlot(dateTime);
                    answer.Responses.Add(new Response()
                    {
                        QuestionId = departureTimeBucket,
                        TextInput = departureBucket
                    });
                }
            }
        }

        private void UpdateDelay15Flag(Answer answer)
        {
            var departureDateTime = ConfigurationManager["DepartureDateTime"];
            var actualOffBlockTimeText = ConfigurationManager["ActualOffBlockTimeText"];
            var delay_15 = ConfigurationManager["Delay_15"];

            var depResponse = answer.Responses.FirstOrDefault(x => x.QuestionId == departureDateTime);
            var delayResponses = answer.Responses.FirstOrDefault(x => x.QuestionId == delay_15);
            var actResponse = answer.Responses.FirstOrDefault(x => x.QuestionId == actualOffBlockTimeText);


            if (depResponse != null && !string.IsNullOrEmpty(depResponse.TextInput) && actResponse != null && !string.IsNullOrEmpty(actResponse.TextInput))
            {
                DateTime.TryParse(depResponse.TextInput, out DateTime depdateTime);
                DateTime.TryParse(actResponse.TextInput, out DateTime actdateTime);

                if (depdateTime != DateTime.MinValue && actdateTime != DateTime.MinValue)
                {
                    var value = actdateTime.Subtract(depdateTime).TotalMinutes;
                    if (delayResponses == null)
                    {
                        if (value > 15)
                            answer.Responses.Add(new Response()
                            {
                                QuestionId = delay_15,
                                TextInput = "Yes"
                            });
                        else if (value > 0.1 && value <= 15)
                            answer.Responses.Add(new Response()
                            {
                                QuestionId = delay_15,
                                TextInput = "Partial"
                            });

                        else
                            answer.Responses.Add(new Response()
                            {
                                QuestionId = delay_15,
                                TextInput = "No"
                            });
                    }
                    else
                    {
                        int index = answer.Responses.FindIndex(x => x.QuestionId == delay_15);
                        if (value > 15)
                        {
                            answer.Responses[index].TextInput = "Yes";
                        }
                        else if (value > 0.1 && value < 15)
                        {
                            answer.Responses[index].TextInput = "Partial";
                        }
                        else
                        {
                            answer.Responses[index].TextInput = "No";
                        }
                    }

                }
            }

        }

        // This function provides a hour slot based on DateTime
        private string GetBucketSlot(DateTime dateTime)
        {
            if (dateTime.Hour >= 0 && dateTime.Hour < 6)
                return TimeBucket.Slot1;
            else if (dateTime.Hour >= 6 && dateTime.Hour < 12)
                return TimeBucket.Slot2;
            else if (dateTime.Hour >= 12 && dateTime.Hour < 18)
                return TimeBucket.Slot3;
            else if (dateTime.Hour >= 18 && dateTime.Hour < 24)
                return TimeBucket.Slot4;

            return string.Empty;
        }

        // Four slots are defined for a day
        public class TimeBucket
        {
            public static string Slot1 = "12 AM - 6 AM";
            public static string Slot2 = "6 AM - 12 PM";
            public static string Slot3 = "12 PM - 6 PM";
            public static string Slot4 = "6 PM - 12 AM";
        }

        // Only for testing purpose
        [HttpGet]
        [Route("help")]
        public IActionResult HelpPage()
        {
            return Ok("If you are seeing this text, that means your API is hosted successfully. Kudos!");
        }

    }
}
