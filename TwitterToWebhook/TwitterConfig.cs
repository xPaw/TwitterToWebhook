using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TwitterStreaming
{
    class TwitterConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string AccessToken { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string AccessSecret { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string ConsumerKey { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string ConsumerSecret { get; set; }

        [JsonProperty(Required = Required.Always)]
        public Dictionary<string, List<string>> AccountsToFollow { get; set; }

        [JsonProperty(Required = Required.Always)]
        public Dictionary<string, string> WebhookUrls { get; set; }
    }
}
