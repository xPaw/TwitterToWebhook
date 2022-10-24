using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Tweetinvi.Models;

namespace TwitterStreaming
{
    class PayloadDiscord
    {
        private class EntityContainer
        {
            public int Start { get; set; }
            public int End { get; set; }
            public string Replacement { get; set; }
        }

        [JsonProperty("username")]
        public string Username { get; }

        [JsonProperty("avatar_url")]
        public string Avatar { get; }

        [JsonProperty("content")]
        public string Content { get; private set; }

        [JsonProperty("embeds")]
        public List<object> Embeds { get; } = new();

        public PayloadDiscord(ITweet tweet)
        {
            string author;

            if (tweet.RetweetedTweet != null)
            {
                author = $"@{tweet.RetweetedTweet.CreatedBy.ScreenName} (RT by @{tweet.CreatedBy.ScreenName})";
                tweet = tweet.RetweetedTweet;
            }
            else
            {
                author = $"@{tweet.CreatedBy.ScreenName}";
            }

            Username = "New Tweet";
            Avatar = tweet.CreatedBy.ProfileImageUrl;

            // TODO: Escape markdown
            FormatTweet(tweet, author);

            if (tweet.QuotedTweet != null)
            {
                FormatTweet(tweet.QuotedTweet, null, true);
            }
        }

        private void FormatTweet(ITweet tweet, string author, bool embed = false)
        {
            var text = tweet.FullText;
            var entities = new List<EntityContainer>();

            if (tweet.Entities?.Urls != null)
            {
                foreach (var entity in tweet.Entities.Urls)
                {
                    if (!entities.Exists(x => x.Start == entity.Indices[0]))
                    {
                        entities.Add(new EntityContainer
                        {
                            Start = entity.Indices[0],
                            End = entity.Indices[1],
                            Replacement = entity.ExpandedURL,
                        });
                    }
                }
            }

            if (tweet.Entities.UserMentions != null)
            {
                foreach (var entity in tweet.Entities.UserMentions)
                {
                    if (!entities.Exists(x => x.Start == entity.Indices[0]))
                    {
                        entities.Add(new EntityContainer
                        {
                            Start = entity.Indices[0],
                            End = entity.Indices[1],
                            Replacement = $"[@{entity.ScreenName}](https://twitter.com/{entity.ScreenName})"
                        });
                    }
                }
            }

            if (tweet.Entities?.Medias != null)
            {
                foreach (var entity in tweet.Entities.Medias)
                {
                    if (entity.MediaType is "photo" or "animated_gif")
                    {
                        Embeds.Add(new
                        {
                            url = tweet.Url,
                            color = 1941746,
                            image = new
                            {
                                url = entity.MediaURLHttps,
                            },
                        });

                        // Remove the short url from text
                        entity.ExpandedURL = "";
                    }

                    if (!entities.Exists(x => x.Start == entity.Indices[0]))
                    {
                        entities.Add(new EntityContainer
                        {
                            Start = entity.Indices[0],
                            End = entity.Indices[1],
                            Replacement = entity.ExpandedURL,
                        });
                    }
                }
            }

            if (entities.Any())
            {
                entities = entities.OrderBy(e => e.Start).ToList();

                var charIndex = 0;
                var entityIndex = 0;
                var codePointIndex = 0;
                var entityCurrent = entities[0];

                while (charIndex < text.Length)
                {
                    if (entityCurrent.Start == codePointIndex)
                    {
                        var len = entityCurrent.End - entityCurrent.Start;
                        entityCurrent.Start = charIndex;
                        entityCurrent.End = charIndex + len;

                        entityIndex++;

                        if (entityIndex == entities.Count)
                        {
                            // no more entity
                            break;
                        }

                        entityCurrent = entities[entityIndex];
                    }

                    if (charIndex < text.Length - 1 && char.IsSurrogatePair(text[charIndex], text[charIndex + 1]))
                    {
                        // Found surrogate pair
                        charIndex++;
                    }

                    codePointIndex++;
                    charIndex++;
                }

                foreach (var entity in entities.OrderByDescending(e => e.Start))
                {
                    text = text[..entity.Start] + entity.Replacement + text[entity.End..];
                }
            }

            text = WebUtility.HtmlDecode(text);

            if (embed)
            {
                Embeds.Insert(0, new
                {
                    url = tweet.Url,
                    color = 1941746,
                    description = text,
                    author = new
                    {
                        name = $"@{tweet.CreatedBy.ScreenName}",
                        icon_url = tweet.CreatedBy.ProfileImageUrl,
                    },
                });
            }
            else
            {
                Content = $"[**{author}**](<{tweet.Url}>): {text}";
            }
        }
    }
}
