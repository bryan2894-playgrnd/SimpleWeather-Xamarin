﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleWeather.NWS
{
    public class AlertRootobject
    {
        [JsonProperty("@context")]
        public Context context { get; set; }
        [JsonProperty("@graph")]
        public Graph[] graph { get; set; }
        public string title { get; set; }
    }

    public class Context
    {
        public string wx { get; set; }
        [JsonProperty("@vocab")]
        public string vocab { get; set; }
    }

    public class Graph
    {
        [JsonProperty("@id")]
        public string atId { get; set; }
        [JsonProperty("@type")]
        public string type { get; set; }
        public string id { get; set; }
        public string areaDesc { get; set; }
        public object geometry { get; set; }
        public Geocode geocode { get; set; }
        //public object[] references { get; set; }
        public DateTimeOffset sent { get; set; }
        public DateTimeOffset effective { get; set; }
        public DateTimeOffset onset { get; set; }
        public DateTimeOffset expires { get; set; }
        //public object ends { get; set; }
        public string status { get; set; }
        public string messageType { get; set; }
        public string category { get; set; }
        public string severity { get; set; }
        public string certainty { get; set; }
        public string urgency { get; set; }
        [JsonProperty("event")]
        public string _event { get; set; }
        public string sender { get; set; }
        public string headline { get; set; }
        public string description { get; set; }
        public string instruction { get; set; }
        public string response { get; set; }
        public Parameters parameters { get; set; }
    }

    public class Geocode
    {
        public string[] UGC { get; set; }
        public string[] SAME { get; set; }
    }

    public class Parameters
    {
        public string[] NWSheadline { get; set; }
        public string[] EASORG { get; set; }
        public string[] PIL { get; set; }
        public string[] BLOCKCHANNEL { get; set; }
    }
}
