using CreateResponseExcel.Helper;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImportResponsesInCC
{
    class Program
    {
        public static async Task Main(string[] args)
        {
			try
			{
                string username = ConfigurationManager.AppSettings["CCUsername"];
                string password = ConfigurationManager.AppSettings["CCPassword"];
                ReadExcel readExcel = new ReadExcel();
                Logger.Info("Import started");
                await readExcel.ImportExcelAsync(username, password);
                Logger.Info("Import Successful");
			}
			catch (Exception ex)
			{
                Logger.Error(ex.ToString());
			}
        }
    }
}
