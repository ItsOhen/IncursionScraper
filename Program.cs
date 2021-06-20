using System.IO;
using System.Collections.Generic;
using HtmlAgilityPack;
using System.Threading;
using System.Text.Json;

namespace IncursionScraper
{
    class Event
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public string Constellation { get; set; }
        public string Region { get; set; }
        public string Staging { get; set; }
        public string Status { get; set; }
        public string Duration { get; set; }
    }
    class Program
    {
        static void Main(string[] args)
        {
            List<Event> events = new List<Event>();
            HtmlWeb req = new HtmlWeb();
            string fn = "dump.json";
            int pagecounter = 1;
            bool run = true;
            do
            {
                var doc = req.Load($"https://www.incursion.info/en/history?page={pagecounter}");
                var table = doc.DocumentNode.SelectSingleNode("//table");
                if (table.ChildNodes.Count > 5)
                {
                    //remove first headers with useless info
                    table.ChildNodes[1].Remove();
                    int c = table.SelectNodes("tbody").Count;
                    string date;
                    foreach (var n in table.Descendants("thead"))
                    {
                        date = n.SelectSingleNode("./tr/td").InnerText;
                        //Console.WriteLine(n.SelectSingleNode("./tr/td").InnerText);
                        HtmlNode tb = n.NextSibling;
                        tb = tb.NextSibling;
                        foreach (var m in tb.SelectNodes("./tr"))
                        {
                            Event ev = new Event();
                            ev.Date = date;
                            ev.Time = m.SelectSingleNode("./td[1]").InnerText;
                            ev.Constellation = m.SelectSingleNode("./td[2]").InnerText;
                            ev.Region = m.SelectSingleNode("./td[3]").InnerText;
                            ev.Staging = m.SelectSingleNode("./td[4]").InnerText;
                            ev.Status = m.SelectSingleNode("./td[5]").InnerText;
                            ev.Duration = m.SelectSingleNode("./td[6]").InnerText;
                            events.Add(ev);
                        }
                    }
                    pagecounter++;
                    Thread.Sleep(100);
                }
                else
                    run = false;
            } while (run == true);

            string json = JsonSerializer.Serialize(events);
            File.WriteAllText(fn, json);
        }
    }
}
