using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tweetinvi;
using Tweetinvi.Events;
using Tweetinvi.Models;
using Tweetinvi.Streaming;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TwitterStreaming
{
    class TwitterStreaming
    {
        class HookTweetObject
        {
            public string Type = "NewTweet";
            public string Url;
            public string Username;
            public string Avatar;
            public object FullObject;
        }

        private readonly Dictionary<long, List<string>> TwitterToChannels;
        private readonly IFilteredStream TwitterStream;
        private readonly HttpClient HttpClient;

        public TwitterStreaming()
        {
            TwitterToChannels = new Dictionary<long, List<string>>();

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "twitter.json");

            if (!File.Exists(path))
            {
                Log.WriteError("File twitter.json doesn't exist");

                return;
            }

            var config = JsonSerializer.Deserialize<TwitterConfig>(File.ReadAllText(path), new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            //TweetinviConfig.ApplicationSettings.TweetMode = TweetMode.Extended;

            // lul per thread or application credentials
            Auth.ApplicationCredentials = new TwitterCredentials(
                config.ConsumerKey,
                config.ConsumerSecret,
                config.AccessToken,
                config.AccessSecret
            );
            
            TwitterStream = Tweetinvi.Stream.CreateFilteredStream();

            foreach (var (_, channels) in config.AccountsToFollow)
            {
                foreach (var channel in channels)
                {
                    if (!config.WebhookUrls.ContainsKey(channel))
                    {
                        throw new Exception($"Channel \"{channel}\" does not exist in WebhookUrls.");
                    }
                }
            }

            var twitterUsers = User.GetUsersFromScreenNames(config.AccountsToFollow.Keys);

            foreach (var user in twitterUsers)
            {
                var channels = config.AccountsToFollow.First(u => u.Key.Equals(user.ScreenName, StringComparison.InvariantCultureIgnoreCase));

                Log.WriteInfo($"Following @{user.ScreenName}");

                TwitterToChannels.Add(user.Id, channels.Value.Select(x => config.WebhookUrls[x]).ToList());

                TwitterStream.AddFollow(user);
            }

            HttpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
            });
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "TwitterToWebhook");
            HttpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public void StartTwitterStream()
        {
            TwitterStream.MatchingTweetReceived += OnTweetReceived;

            TwitterStream.StallWarnings = true;
            TwitterStream.WarningFallingBehindDetected += (_, args) => Log.WriteWarn($"Stream falling behind: {args.WarningMessage.PercentFull} {args.WarningMessage.Code} {args.WarningMessage.Message}");

            TwitterStream.StreamStopped += (sender, args) =>
            {
                var ex = args.Exception;
                var twitterDisconnectMessage = args.DisconnectMessage;

                if (ex != null)
                {
                    Log.WriteError(ex.ToString());
                }

                if (twitterDisconnectMessage != null)
                {
                    Log.WriteError($"Stream stopped: {twitterDisconnectMessage.Code} {twitterDisconnectMessage.Reason}");
                }

                Thread.Sleep(5000);
                Log.WriteInfo("Restarting stream");
                TwitterStream.StartStreamMatchingAnyCondition();
            };

            TwitterStream.StartStreamMatchingAnyCondition();
        }

        private async void OnTweetReceived(object sender, MatchedTweetReceivedEventArgs matchedTweetReceivedEventArgs)
        {
            var tweet = matchedTweetReceivedEventArgs.Tweet;

            // Skip replies
            if (tweet.InReplyToUserId != null && !TwitterToChannels.ContainsKey(tweet.InReplyToUserId.GetValueOrDefault()))
            {
                Log.WriteInfo($"@{tweet.CreatedBy.ScreenName} replied to @{tweet.InReplyToScreenName}: {tweet.Url}");
                return;
            }

            if (!TwitterToChannels.ContainsKey(tweet.CreatedBy.Id))
            {
                return;
            }

            if (tweet.RetweetedTweet != null && TwitterToChannels.ContainsKey(tweet.RetweetedTweet.CreatedBy.Id))
            {
                Log.WriteInfo($"@{tweet.CreatedBy.ScreenName} retweeted @{tweet.RetweetedTweet.CreatedBy.ScreenName}: {tweet.Url}");
                return;
            }

            Log.WriteInfo($"Streamed {tweet.Url}: {tweet.FullText}");

            var payload = new HookTweetObject
            {
                Url = tweet.Url,
                Username = tweet.CreatedBy.ScreenName,
                Avatar = tweet.CreatedBy.ProfileImageUrl,
                FullObject = tweet.TweetDTO,
            };

            foreach (var hookUrl in TwitterToChannels[tweet.CreatedBy.Id])
            {
                await SendWebhook(hookUrl, payload);
            }
        }

        private async Task SendWebhook(string url, HookTweetObject payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var result = await HttpClient.PostAsync(url, content);
                var output = await result.Content.ReadAsStringAsync();

                Log.WriteInfo($"Webhook result: {output}");
            }
            catch (Exception e)
            {
                Log.WriteError($"Webhook exception: {e}");
            }
        }
    }
}
