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

        public PayloadDiscord(PayloadGeneric payload)
        {
            Username = $"@{payload.Username}";
            Avatar = payload.Avatar;

            // TODO: Escape markdown
            FormatTweet(payload.Tweet);

            Content = $"[Tweet:](<{payload.Url}>) {Content}";
        }

        private void FormatTweet(ITweet tweet)
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

            if (!entities.Any())
            {
                Content = WebUtility.HtmlDecode(text);
                return;
            }

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

            Content = WebUtility.HtmlDecode(text);
        }
    }
}
