using System;
using System.IO;
using System.Collections.Generic;
using HtmlAgilityPack;
using System.Threading;
using System.Text.Json;
using System.Globalization;

namespace IncursionScraper
{
    enum EventType
    {
        Established,
        Mobilizing,
        Withdrawing,
        Ended,
        Unkown
    }
    class Event
    {
        public string Date { get; set; }
        public string Time { get; set; }
        public string Constellation { get; set; }
        public string Region { get; set; }
        public string Staging { get; set; }
        public string Status { get; set; }
        public string Duration { get; set; }
        public float SecStatus { get; set; }
        public DateTime ToDateTime()
        {
            string pattern = "MM.dd.yyyy HH:mm";
            string timestr = Date.Substring(Date.IndexOf(", ")+2) + " " + Time;
            DateTime ret = DateTime.ParseExact(timestr, pattern, CultureInfo.InvariantCulture);
            return ret;
        }
        public EventType GetEventType()
        {
            if (Status == "Established")
                return EventType.Established;
            if (Status == "Mobilizing")
                return EventType.Mobilizing;
            if (Status == "Withdrawing")
                return EventType.Withdrawing;
            if (Status == "Ended")
                return EventType.Ended;
            return EventType.Unkown;
        }
    }
    class Incursion
    {
        public TimeSpan TimeAlive { get; set; }
        public DateTime Started { get; set; }
        public DateTime Mobilized { get; set; }
        public DateTime Withdrawing { get; set; }
        public DateTime Ended { get; set; }
        public Solarsystem Location { get; set; }
        public decimal SecurityStatus { get { return Location.SecStatus; } }
        public Incursion(Event established, Solarsystem location)
        {
            Started = established.ToDateTime();
            Location = location;
        }
        // Return true on update if the incursion ended
        public bool Update(Event ev)
        {
            switch(ev.GetEventType())
            {
                case EventType.Mobilizing:
                    Mobilized = ev.ToDateTime();
                    break;
                case EventType.Withdrawing:
                    Withdrawing = ev.ToDateTime();
                    break;
                case EventType.Ended:
                    Ended = ev.ToDateTime();
                    TimeAlive = Ended - Started;
                    return true;
                default:
                    return false;
            }
            return false;
        }
    }
    class Solarsystem
    {
        public string Name { get; set; }
        public decimal SecStatus { get; set; }
        public Solarsystem(string name, decimal sec)
        {
            Name = name;
            SecStatus = sec;
        }
    }
    class Program
    {
        static void Main(string[] args)
        {
            string fn = "dump.json";
            string fn_map = "mapSolarSystems.json";
            List<Event> events;

            events = ReadFromFile(fn);
            Dictionary<string, Solarsystem> systems = ReadSolarsystemMap(fn_map);

            List<Incursion> incursions = new List<Incursion>();
            Dictionary<string, Incursion> opened = new Dictionary<string, Incursion>();
            events.Reverse();
            foreach(Event ev in events)
            {
                if(!opened.ContainsKey(ev.Staging))
                    opened.Add(ev.Staging, new Incursion(ev, systems[ev.Staging]));
                if (opened[ev.Staging].Update(ev))
                {
                    incursions.Add(opened[ev.Staging]);
                    opened.Remove(ev.Staging);
                }
            }
            var AvgTime = AverageTime(incursions);
            Console.WriteLine("Number of incursions finished: {0}\nNumber of incursions still open: {1}\n", incursions.Count, opened.Count);
            Console.WriteLine("Average time incursions have been up: {0} days, {1} hours, {2} minutes", AvgTime.Days, AvgTime.Hours, AvgTime.Minutes);
        }

        public List<Event> ScrapeSite(string fn)
        {
            List<Event> events = new List<Event>();
            HtmlWeb req = new HtmlWeb();
            int pagecounter = 1;
            bool run = true;
            do
            {
                var doc = req.Load($"https://www.incursion.info/en/history?page={pagecounter}");
                var table = doc.DocumentNode.SelectSingleNode("//table");
                if (table.ChildNodes.Count > 5)
                {
                    Console.WriteLine($"Reading page number: {pagecounter}");
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
                    Thread.Sleep(50);
                }
                else
                    run = false;
            } while (run == true);
    
            string json = JsonSerializer.Serialize(events);
            File.WriteAllText(fn, json);
            return events;
        }
        static public List<Event> ReadFromFile(string fn)
        {
            List<Event> events;
            using (StreamReader f = new StreamReader(fn))
            {
                string json = f.ReadToEnd();
                events = JsonSerializer.Deserialize<List<Event>>(json);
            }
            return events;
        }
        static public TimeSpan AverageTime(List<Incursion> incursions)
        {
            //List<TimeSpan> dtl = new List<TimeSpan>();
            TimeSpan ts = TimeSpan.Zero;
            foreach(Incursion inc in incursions)
            {
                ts += inc.TimeAlive;
            }
            return new TimeSpan(ts.Ticks / incursions.Count);
        }
        static public TimeSpan AverageDowntime(List<Incursion> incursions)
        {
            return TimeSpan.Zero;
        }
        static public Dictionary<string, Solarsystem> ReadSolarsystemMap(string fn)
        {
            Dictionary<string, Solarsystem> ret = new Dictionary<string, Solarsystem>();
            using (StreamReader f = new StreamReader(fn))
            {
                string json = f.ReadToEnd();
                JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                var arr = root.EnumerateArray();
                while (arr.MoveNext())
                {
                    try
                    {
                        string name = arr.Current.GetProperty("solarSystemName").GetString();
                        decimal sec = arr.Current.GetProperty("security").GetDecimal();
                        ret.Add(name, new Solarsystem(name, sec));
                        //Console.WriteLine("name {0} sec {1}", arr.Current.GetProperty("solarSystemName"), arr.Current.GetProperty("security"));
                    }
                    catch
                    { }
                }
            }
            return ret;
        }
    }
}
