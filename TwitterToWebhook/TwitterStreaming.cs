using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Tweetinvi;
using Tweetinvi.Events;
using Tweetinvi.Models;
using Tweetinvi.Streaming;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TwitterStreaming
{
    class TwitterStreaming : IDisposable
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

        public void Dispose()
        {
            HttpClient?.Dispose();
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
            TwitterStream.StreamStopped += (sender, args) =>
            {
                var ex = args.Exception;
                var twitterDisconnectMessage = args.DisconnectMessage;

                if (twitterDisconnectMessage != null)
                {
                    Log.WriteError($"Stream stopped: {twitterDisconnectMessage.Code} {twitterDisconnectMessage.Reason}");
                }

                if (ex != null)
                {
                    Log.WriteError($"Stream stopped exception: {ex}");
                }
            };

            do
            {
                try
                {
                    Log.WriteInfo("Connecting to stream");
                    await TwitterStream.StartMatchingAnyConditionAsync();
                }
                catch (Exception ex)
                {
                    Log.WriteError($"Exception caught: {ex}");

                    if (TwitterStream.StreamState != StreamState.Stop)
                    {
                        Log.WriteInfo("Stream is not stopped, stopping");
                        TwitterStream.Stop();
                    }

                    await Task.Delay(5000);
                }
            }
            while (true);
        }

        private async void OnTweetReceived(object sender, MatchedTweetReceivedEventArgs matchedTweetReceivedEventArgs)
        {
            var tweet = matchedTweetReceivedEventArgs.Tweet;

            // Skip tweets from accounts that are not monitored (quirk of how twitter streaming works)
            if (!TwitterToChannels.TryGetValue(tweet.CreatedBy.Id, out var endpoints))
            {
#if DEBUG
                Log.WriteInfo($"@{tweet.CreatedBy.ScreenName} (skipped): {tweet.Url}");
#endif
                return;
            }

            // Skip replies unless replying to another monitored account
            if (tweet.InReplyToUserId != null && AccountsToIgnoreRepliesFrom.Contains(tweet.CreatedBy.Id) && !TwitterToChannels.ContainsKey(tweet.InReplyToUserId.GetValueOrDefault()))
            {
                Log.WriteInfo($"@{tweet.CreatedBy.ScreenName} replied to @{tweet.InReplyToScreenName}: {tweet.Url}");
                return;
            }

            // When retweeting a monitored account, do not send retweets to channels that original tweeter also sends to
            if (tweet.RetweetedTweet != null && TwitterToChannels.TryGetValue(tweet.RetweetedTweet.CreatedBy.Id, out var retweetEndpoints))
            {
                endpoints = endpoints.Except(retweetEndpoints).ToList();

                if (!endpoints.Any())
                {
                    Log.WriteInfo($"@{tweet.CreatedBy.ScreenName} retweeted @{tweet.RetweetedTweet.CreatedBy.ScreenName}: {tweet.Url}");
                    return;
                }
            }

            Log.WriteInfo($"@{tweet.CreatedBy.ScreenName} tweeted: {tweet.Url}");

            foreach (var hookUrl in endpoints)
            {
                await SendWebhook(hookUrl, tweet);
            }
        }

        private async Task SendWebhook(Uri url, ITweet tweet)
        {
            string json;

            if (url.Host == "discord.com")
            {
                // If webhook target is Discord, convert it to a Discord compatible payload
                json = JsonConvert.SerializeObject(new PayloadDiscord(tweet));
            }
            else
            {
                json = JsonConvert.SerializeObject(new PayloadGeneric(tweet));
            }

            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            try
            {
                var result = await HttpClient.PostAsync(url, content);
                var output = await result.Content.ReadAsStringAsync();

                Log.WriteInfo($"Webhook result ({(int)result.StatusCode}): {output}");
            }
            catch (Exception e)
            {
                Log.WriteError($"Webhook exception: {e}");
            }
        }
    }
}
