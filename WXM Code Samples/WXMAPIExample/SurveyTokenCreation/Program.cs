using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SurveyTokenCreation
{
    class Program
    {
        public static IConfigurationRoot configuration;
        static async Task Main(string[] args)
        {
            // Initialize configuration variable.
            configuration = new ConfigurationBuilder()
             .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
             .AddJsonFile("appsettings.json", false)
             .Build();
            // Token returned from WXM API. 
            string token = await SurveyTokenCreation.CreateToken(configuration);
            Console.WriteLine(token);
            Console.Read();
        }
    }
}
