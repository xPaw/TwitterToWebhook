using System;

namespace TwitterStreaming
{
    class PayloadGeneric
    {
        public string Type { get; } = "NewTweet";
        public string Url { get; init; }
        public string Username { get; init; }
        public string Avatar { get; init; }
        public object FullObject { get; init; }
    }
}
