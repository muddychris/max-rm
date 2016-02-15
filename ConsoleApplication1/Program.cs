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

            program.getClientList();

            IList<Client> clientsWithFailingChecks = program.getFailingChecks();

            program.showFailingchecks(clientsWithFailingChecks);

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
                             Id = (string)site.Element("siteid").Value
                          }).ToList();

            foreach (var site in sites)
            {
                Console.WriteLine(string.Format("    SITE [{0}] ID [{1}]", site.Name, site.Id));
            }

            return sites ;
        }

        private IList<Client> getFailingChecks()
        {
            XDocument xmlResponse = doAPIQuery("list_failing_checks");
            if (xmlResponse == null)
                return null;

            Console.WriteLine("**********");

            // now parse the returned failing check list

            IList<Client> clients = getClientListFromFailingChecks(xmlResponse);

            enrichFailingChecksWithOutages(clients);

            return clients;
        }

        private void enrichFailingChecksWithOutages (IList<Client> clients)
        {
            foreach (var client in clients)
            {
                foreach (var site in client.Sites)
                {
                    foreach (var device in site.getDevices())
                    {
                        // populate the dictionary of checks

                        device.ChecksDictionary = new Dictionary<string, Check>();

                        foreach (var check in device.Checks)
                        {
                            device.ChecksDictionary.Add(check.Id, check);
                        }

                        // ask the server for outages on this device

                        IList<Outage> outagesOnDevice = getOutagesOnDevice(device.Id);

                        foreach (var outage in outagesOnDevice)
                        {
                            Console.WriteLine(string.Format("ID [{0}] Outage [{1}]", outage.Id, outage.CheckId));

                            var check = device.ChecksDictionary[outage.CheckId];
                            if (check != null)
                            {
                                check.OutageUtcStartAsStr = outage.UtcStartAsStr;
                            }
                        }
                    }
                }
            }
        }

        private IList<Outage> getOutagesOnDevice(string deviceId)
        {
            string query = string.Format("list_outages&deviceid={0}", deviceId);
            XDocument xmlResponse = doAPIQuery(query);
            if (xmlResponse == null)
                return null;

            Console.WriteLine("================");

            var outages =
                from outage in xmlResponse.Descendants("outage")
                where outage.Element("state").Value == "OPEN"
                select new Outage
                {
                    Id = outage.Element("outage_id").Value,
                    CheckId = outage.Element("check_id").Value,
                    UtcStartAsStr = outage.Element("utc_start").Value
                };

            return outages.ToList();
        }

        private void showFailingchecks (IList<Client> clients)
        {
            foreach (var client in clients)
            {
                Console.WriteLine(string.Format("ID [{0}] Client [{1}]", client.Id, client.Name));

                foreach (var site in client.Sites)
                {
                    Console.WriteLine(string.Format("    ID [{0}] Site [{1}]", site.Id, site.Name)) ;
                    foreach (var device in site.getDevices())
                    {
                        Console.WriteLine(string.Format("        ID [{0}] Device [{1}] [{2}]", device.Id, device.Name, device.Type));

                        foreach (var check in device.Checks)
                        {
                            Console.WriteLine(string.Format("            ID [{0}] Check [{1}] [{2}] [{3}] COUNT = [{4}] [{5}] [{6}]",
                                check.Id, check.Type, check.Description, check.Message, check.ConsecutiveFails,
                                check.getPollTimestampAsStr(), check.OutageUtcStartAsStr));
                        }
                    }
                }
            }
        }

        private IList<Client> getClientListFromFailingChecks(XDocument xmlResponse)
        {
            var clients =
                from client in xmlResponse.Descendants("client")
                    select new Client
                    {
                        Id = client.Element("clientid").Value,
                        Name = client.Element("name").Value,
                        Sites = new List<Site> 
                        (
                            from site in client.Descendants("site")
                            select new Site
                            {
                                Id = site.Element("siteid").Value,
                                Name = site.Element("name").Value,
                                // construct a list of devices which are workstations
                                Workstations = new List<Device>
                                (
                                    from device in site.Descendants("workstations").Elements("workstation")
                                    select new Device
                                    {
                                        Id = device.Element("id").Value,
                                        Name = device.Element("name").Value,
                                        Type = "Workstation",
                                        Checks = new List<Check>
                                        (
                                            from check in device.Descendants("failed_checks").Elements("check")
                                            select new Check
                                            {
                                                Id = check.Element("checkid").Value,
                                                Type = check.Element("check_type").Value,
                                                PollDateAsStr = check.Element("date").Value,
                                                PollTimeAsStr = check.Element("time").Value,
                                                Description = check.Element("description").Value,
                                                Message = check.Element("formatted_output").Value
                                            }
                                        )
                                    }
                                ),
                                // construct a list of devices which are servers
                                Servers = new List<Device>
                                (
                                    from device in site.Descendants("servers").Elements("server")
                                    select new Device
                                    {
                                        Id = device.Element("id").Value,
                                        Name = device.Element("name").Value,
                                        Type = "Server",
                                        Checks = new List<Check>
                                        (
                                            from check in device.Descendants("failed_checks").Elements("check")
                                            select new Check
                                            {
                                                Id = check.Element("checkid").Value,
                                                Type = check.Element("check_type").Value,
                                                ConsecutiveFails = check.Element("consecutive_fails").Value,
                                                PollDateAsStr = check.Element("date").Value,
                                                PollTimeAsStr = check.Element("time").Value,
                                                Description = check.Element("description").Value,
                                                Message = check.Element("formatted_output").Value
                                            }
                                        )
                                    }
                                )
                            }
                        )
                    };

            return clients.ToList() ;
        }


        private static XDocument doAPIQuery(string strQuery)
        {
            string strAPIUrl = "https://wwwgermany1.systemmonitor.eu.com/api/?apikey=";
            string strAPIKey = "h7zWLXrXRK89xboboPFUZgfHxm66HlUM";
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
        public string Id { get; set; }
        public string Name { get; set; }

        public List<Site> Sites = new List<Site>();
    }

    class Site
    {
        public string Id { get; set; }
        public string Name { get; set; }

        private List<Device> Devices = new List<Device>();

        // return a combined list of workstations, servers etc
        internal List<Device> getDevices()
        {
            return Workstations.Concat(Servers).ToList() ;
        }

        public List<Device> Workstations = new List<Device>();
        public List<Device> Servers = new List<Device>();

    }
    class Device
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }

        public List<Check> Checks = new List<Check>() ;
        public Dictionary<string, Check> ChecksDictionary = new Dictionary<String, Check> () ;
    }

    class Check
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string ConsecutiveFails { get; set; }
        public string Description { get; set; }
        public string Message { get; set; }

        public String OutageUtcStartAsStr { get; set; }

        public string PollDateAsStr;
        public void setPollDateAsStr(String dateStr)
        {
            PollDateAsStr = dateStr;
        }
        public string PollTimeAsStr;
        public void setPollTimeAsStr(String timeStr)
        {
            PollTimeAsStr = timeStr;
        }

        public string getPollTimestampAsStr ()
        {
            return PollDateAsStr + " " + PollTimeAsStr;
        }
    }
    class Outage
    {
        public string Id { get; set; }
        public string CheckId { get; set; }
        public string CheckStatus { get; set; }
        public string UtcStartAsStr { get; set; }
    }

}
