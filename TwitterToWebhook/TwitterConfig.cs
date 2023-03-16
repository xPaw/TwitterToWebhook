using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TwitterStreaming
{
    class TwitterConfig
    {
        [JsonProperty(Required = Required.Always)]
        public string ConsumerKey { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string ConsumerSecret { get; set; }

        [JsonProperty(Required = Required.Always)]
        public string BearerToken { get; set; }

        [JsonProperty(Required = Required.Always)]
        public Dictionary<string, List<string>> AccountsToFollow { get; set; }

        [JsonProperty(Required = Required.Always)]
        public HashSet<string> IgnoreReplies { get; set; }

        [JsonProperty(Required = Required.Always)]
        public Dictionary<string, Uri> WebhookUrls { get; set; }
    }
}
