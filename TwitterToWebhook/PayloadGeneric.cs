using System;

namespace TwitterStreaming
{
    class PayloadGeneric
    {
        public string Type = "NewTweet";
        public string Url;
        public string Username;
        public string Avatar;
        public object FullObject;
    }
}
