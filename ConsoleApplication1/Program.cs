using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;


namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            Program program = new Program();

            Console.WriteLine("aha");

            //program.getClientList();

            program.getFailingChecks();

            Console.WriteLine();
        }

        private IList<Client> getClientList()
        {
            XDocument xmlResponse = doAPIQuery("list_clients");
            if (xmlResponse == null)
                return null ;

            // now parse the returned client list

            var clients = (from client in xmlResponse.Descendants()
                           where client.Name == "client"
                           select new Client
                           {
                               Name = (string)client.Element ("name").Value,
                               Id = (string)client.Element("clientid").Value
                           }).ToList();

            foreach (var client in clients)
            {
                Console.WriteLine(string.Format("Client [{0}] ID [{1}]", client.Name, client.Id));

                getSiteList(client.Id);
            }

            return clients;
        }
        private IList<Site> getSiteList(string clientId)
        {
            string query = string.Format("list_sites&clientid={0}", clientId);
            XDocument xmlResponse = doAPIQuery(query);
            if (xmlResponse == null)
                return null;

            // now parse the returned site list

            var sites = (from site in xmlResponse.Descendants()
                         where site.Name == "site"
                         select new Site
                         {
                             Name = (string)site.Element("name").Value,
                             Id = (string)site.Element("siteid").Value,
                             ClientId = clientId
                          }).ToList();

            foreach (var site in sites)
            {
                Console.WriteLine(string.Format("    SITE [{0}] ID [{1}] ClientId [{2}]", site.Name, site.Id, site.ClientId));
            }

            return sites ;
        }

        private void getFailingChecks ()
        {
            XDocument xmlResponse = doAPIQuery("list_failing_checks");
            if (xmlResponse == null)
                return;

            // now parse the returned failing check list

            var clients = (from client in xmlResponse.Descendants()
                           where client.Name == "client"
                           select new Client
                           {
                               Name = (string)client.Element("name").Value,
                               Id = (string)client.Element("clientid").Value
                           }).ToList();

            foreach (var client in clients)
            {
                Console.WriteLine(string.Format("Client [{0}] ID [{1}]", client.Name, client.Id));

                var siteList = getSiteList(client.Id);
            }
        }
        private static XDocument doAPIQuery(string strQuery)
        {
            string strAPIUrl = "https://wwwgermany1.systemmonitor.eu.com/api/?apikey=";
            string strAPIKey = "AMsUVXC0UkblWryCu1yllPdbMwG6pjku";
            string strService = "&service=";

            string strQueryUrl = strAPIUrl + strAPIKey + strService + strQuery ;

            XDocument xDoc = null ;

            try
            {
                do
                {
                    xDoc = XDocument.Load(strQueryUrl);
                }
                while (xDoc == null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                //MessageBox.Show(ex.ToString(), "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // now check for an error response

            var errors = (from site in xDoc.Descendants()
                         where site.Name == "error"
                         select new ErrorResponse
                         {
                             ErrorCode = (string)site.Element("errorcode").Value,
                             Message = (string)site.Element("message").Value
                          }).ToList();

            if (errors.Count > 0)
            {
                Console.WriteLine("URL [" + strQueryUrl + "]");

                foreach (var error in errors)
                {
                    Console.WriteLine(string.Format("ERROR [{0}] ID [{1}]", error.ErrorCode, error.Message));
                }

                xDoc = null;
            }

            return xDoc;
        }
    }

    class ErrorResponse
    {
        public string ErrorCode { get; set; }
        public string Message { get; set; }
    }

    class Client
    {
        public string Name { get; set; }
        public string Id { get; set; }
    }
    class Site
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string ClientId { get; set; }
    }
    class Device
    {
        public string Name { get; set; }
        public string Id { get; set; }
        public string SiteId { get; set; }
    }
}
