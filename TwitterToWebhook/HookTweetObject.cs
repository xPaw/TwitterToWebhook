using System;

namespace TwitterStreaming
{
    class HookTweetObject
    {
        public string Type = "NewTweet";
        public string Url;
        public string Username;
        public string Avatar;
        public object FullObject;
    }
}
