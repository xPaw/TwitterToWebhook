using Tweetinvi.Models.V2;

namespace TwitterStreaming
{
    class PayloadGeneric
    {
        public string Type { get; } = "NewTweet";
        public string Url { get; init; }
        public string Username { get; init; }
        public string Avatar { get; init; }
        public object FullObject { get; init; }

        public PayloadGeneric(TweetV2 tweet, UserV2 author, string url)
        {
            Url = url;
            Username = author.Username;
            Avatar = author.ProfileImageUrl;
            FullObject = tweet;
        }
    }
}
