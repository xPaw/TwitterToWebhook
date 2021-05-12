using System;
using Newtonsoft.Json;

namespace TwitterStreaming
{
    class PayloadDiscord
    {
        [JsonProperty("username")]
        public string Username;

        [JsonProperty("avatar_url")]
        public string Avatar;

        [JsonProperty("content")]
        public object Content;

        public PayloadDiscord(PayloadGeneric payload)
        {
            Username = $"@{payload.Username}";
            Avatar = payload.Avatar;
            Content = payload.Url;
        }
    }
}
