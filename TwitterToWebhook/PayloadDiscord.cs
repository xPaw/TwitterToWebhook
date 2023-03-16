using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Tweetinvi.Models.V2;

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

        public class Embed
        {
            public class Author
            {
                public string name { get; set; }
                public string icon_url { get; set; }
                public string url { get; set; }
            }

            public class Footer
            {
                public string text { get; set; }
                public string icon_url { get; set; }
            }

            public class Image
            {
                public string url { get; set; }
            }

            public string url { get; set; }
            public int color { get; set; }
            public string description { get; set; }
            public Author author { get; set; }
            public Footer footer { get; set; }
            public Image image { get; set; }
        }

        [JsonProperty("username")]
        public string Username { get; }

        [JsonProperty("avatar_url")]
        public string Avatar { get; }

        [JsonProperty("content")]
        public string Content { get; private set; }

        [JsonProperty("embeds")]
        public List<Embed> Embeds { get; } = new();

        public PayloadDiscord(TweetV2 tweet, UserV2 author, string url, bool ignoreQuoteTweet)
        {
            Username = $"New Tweet by @{author.Username}";
            Avatar = author.ProfileImageUrl;
            Content = url;
        }

#if false
        public PayloadDiscord(ITweet tweet, bool ignoreQuoteTweet)
        {
            Username = "New Tweet";

            if (tweet.RetweetedTweet != null)
            {
                Avatar = tweet.RetweetedTweet.CreatedBy.ProfileImageUrl;
            }
            else
            {
                Avatar = tweet.CreatedBy.ProfileImageUrl;
            }

            // TODO: Escape markdown
            FormatTweet(tweet);

            if (tweet.QuotedTweet != null && !ignoreQuoteTweet)
            {
                FormatTweet(tweet.QuotedTweet);
            }
        }

        private void FormatTweet(ITweet tweet)
        {
            string author;

            if (tweet.RetweetedTweet != null)
            {
                author = $"{tweet.RetweetedTweet.CreatedBy.Name} (@{tweet.RetweetedTweet.CreatedBy.ScreenName}) (@{tweet.CreatedBy.ScreenName} retweeted)";
                tweet = tweet.RetweetedTweet;
            }
            else
            {
                author = $"{tweet.CreatedBy.Name} (@{tweet.CreatedBy.ScreenName})";
            }

            var text = tweet.FullText;
            var entities = new List<EntityContainer>();
            var images = new List<Embed>();

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

            if (tweet.Entities.Hashtags != null)
            {
                foreach (var entity in tweet.Entities.Hashtags)
                {
                    if (!entities.Exists(x => x.Start == entity.Indices[0]))
                    {
                        entities.Add(new EntityContainer
                        {
                            Start = entity.Indices[0],
                            End = entity.Indices[1],
                            Replacement = $"[#{entity.Text}](https://twitter.com/hashtag/{entity.Text})"
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
                    if (entity.MediaType is "photo" or "animated_gif" or "video")
                    {
                        images.Add(new Embed
                        {
                            url = tweet.Url,
                            image = new Embed.Image
                            {
                                url = entity.MediaURLHttps,
                            },
                        });

                        if (entity.MediaType is "photo" or "animated_gif")
                        {
                            // Remove the short url from text
                            entity.ExpandedURL = "";
                        }
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

            var embed = new Embed
            {
                url = tweet.Url,
                color = 1941746,
                description = text,
                author = new Embed.Author
                {
                    name = author,
                    icon_url = tweet.CreatedBy.ProfileImageUrl,
                    url = tweet.Url,
                },
            };

            if (images.Any())
            {
                embed.image = images[0].image;
                images.RemoveAt(0);
            }

            if (tweet.QuotedTweet != null)
            {
                embed.footer = new Embed.Footer
                {
                    text = $"quoting @{tweet.QuotedTweet.CreatedBy.ScreenName}",
                    icon_url = tweet.QuotedTweet.CreatedBy.ProfileImageUrl,
                };
            }

            Embeds.Add(embed);
            Embeds.AddRange(images);
        }
#endif
    }
}
