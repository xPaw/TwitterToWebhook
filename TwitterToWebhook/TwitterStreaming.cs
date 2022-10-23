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

            var payload = new PayloadGeneric
            {
                Url = tweet.Url,
                Username = tweet.CreatedBy.ScreenName,
                Avatar = tweet.CreatedBy.ProfileImageUrl,
                FullObject = tweet.TweetDTO,
                Tweet = tweet,
            };

            foreach (var hookUrl in TwitterToChannels[tweet.CreatedBy.Id])
            {
                await SendWebhook(hookUrl, payload);
            }
        }

        private async Task SendWebhook(Uri url, PayloadGeneric payload)
        {
            string json;

            if (url.Host == "discord.com")
            {
                // If webhook target is Discord, convert it to a Discord compatible payload
                json = JsonConvert.SerializeObject(new PayloadDiscord(payload));
            }
            else
            {
                json = JsonConvert.SerializeObject(payload);
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
