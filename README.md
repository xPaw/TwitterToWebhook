# TwitterToWebhook

This is a very simple program that listens to Twitter stream and
forwards tweets to a webhook url.

Discord webhooks are also natively supported.

See `twitter.json` file for configuration.

## Payload

POST request is sent to the configured url with the following JSON payload:

```json
{
	"Type": "NewTweet",
	"Username": "jack",
	"Url": "https://twitter.com/jack/status/20",
	"Avatar": "https://pbs.twimg.com/profile_images/1115644092329758721/AFjOr-K8_400x400.jpg",
}
```
