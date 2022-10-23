using Tweetinvi.Models;

namespace TwitterStreaming
{
    class PayloadGeneric
    {
        public string Type { get; } = "NewTweet";
        public string Url { get; init; }
        public string Username { get; init; }
        public string Avatar { get; init; }
        public object FullObject { get; init; }

        public PayloadGeneric(ITweet tweet)
        {
            Url = tweet.Url;
            Username = tweet.CreatedBy.ScreenName;
            Avatar = tweet.CreatedBy.ProfileImageUrl;
            FullObject = tweet.TweetDTO;
        }
    }
}
