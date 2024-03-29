﻿using System;
using Newtonsoft.Json;

namespace TwitterStreaming
{
    class PayloadDiscord
    {
        [JsonProperty("username")]
        public string Username { get; }

        [JsonProperty("avatar_url")]
        public string Avatar { get; }

        [JsonProperty("content")]
        public string Content { get; }

        public PayloadDiscord(PayloadGeneric payload)
        {
            Username = $"@{payload.Username}";
            Avatar = payload.Avatar;
            Content = payload.Url;
        }
    }
}
