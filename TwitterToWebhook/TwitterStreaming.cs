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
using Tweetinvi.Streaming;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TwitterStreaming
{
    class TwitterStreaming
    {
        private readonly Dictionary<long, List<Uri>> TwitterToChannels = new();
        private readonly HashSet<long> AccountsToIgnoreRepliesFrom = new();
        private readonly HttpClient HttpClient;
        private IFilteredStream TwitterStream;

        public TwitterStreaming()
        {
            HttpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
            });
            HttpClient.DefaultRequestHeaders.Add("User-Agent", "TwitterToWebhook");
            HttpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task Initialize()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "twitter.json");

            var config = JsonSerializer.Deserialize<TwitterConfig>(await File.ReadAllTextAsync(path), new JsonSerializerOptions
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            var userClient = new TwitterClient(config.ConsumerKey, config.ConsumerSecret, config.AccessToken, config.AccessSecret);
            
            TwitterStream = userClient.Streams.CreateFilteredStream();

            foreach (var (_, channels) in config.AccountsToFollow)
            {
                foreach (var channel in channels)
                {
                    if (!config.WebhookUrls.ContainsKey(channel))
                    {
                        throw new KeyNotFoundException($"Channel \"{channel}\" does not exist in WebhookUrls.");
                    }
                }
            }

            var twitterUsers = await userClient.Users.GetUsersAsync(config.AccountsToFollow.Keys);

            foreach (var user in twitterUsers)
            {
                var channels = config.AccountsToFollow.First(u => u.Key.Equals(user.ScreenName, StringComparison.OrdinalIgnoreCase));

                Log.WriteInfo($"Following @{user.ScreenName}");

                TwitterToChannels.Add(user.Id, channels.Value.Select(x => config.WebhookUrls[x]).ToList());

                if (config.IgnoreReplies.Contains(user.ScreenName, StringComparer.InvariantCultureIgnoreCase))
                {
                    AccountsToIgnoreRepliesFrom.Add(user.Id);
                }

                TwitterStream.AddFollow(user);
            }
        }

        public async Task StartTwitterStream()
        {
            TwitterStream.MatchingTweetReceived += OnTweetReceived;

            TwitterStream.StallWarnings = true;
            TwitterStream.WarningFallingBehindDetected += (_, args) => Log.WriteWarn($"Stream falling behind: {args.WarningMessage.PercentFull} {args.WarningMessage.Code} {args.WarningMessage.Message}");

            TwitterStream.StreamStopped += async (sender, args) =>
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
                await TwitterStream.StartMatchingAnyConditionAsync();
            };

            await TwitterStream.StartMatchingAnyConditionAsync();
        }

        private async void OnTweetReceived(object sender, MatchedTweetReceivedEventArgs matchedTweetReceivedEventArgs)
        {
            var tweet = matchedTweetReceivedEventArgs.Tweet;

            if (!TwitterToChannels.ContainsKey(tweet.CreatedBy.Id))
            {
#if DEBUG
                Log.WriteInfo($"@{tweet.CreatedBy.ScreenName} (skipped): {tweet.Url}");
#endif
                return;
            }

            // Skip replies
            if (tweet.InReplyToUserId != null && AccountsToIgnoreRepliesFrom.Contains(tweet.CreatedBy.Id) && !TwitterToChannels.ContainsKey(tweet.InReplyToUserId.GetValueOrDefault()))
            {
                Log.WriteInfo($"@{tweet.CreatedBy.ScreenName} replied to @{tweet.InReplyToScreenName}: {tweet.Url}");
                return;
            }

            if (tweet.RetweetedTweet != null && TwitterToChannels.ContainsKey(tweet.RetweetedTweet.CreatedBy.Id))
            {
                Log.WriteInfo($"@{tweet.CreatedBy.ScreenName} retweeted @{tweet.RetweetedTweet.CreatedBy.ScreenName}: {tweet.Url}");
                return;
            }

            Log.WriteInfo($"@{tweet.CreatedBy.ScreenName} tweeted: {tweet.Url}");

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

        private async Task SendWebhook(Uri url, HookTweetObject payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

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
