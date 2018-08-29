using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;

using Microsoft.Bot.Schema;

using Newtonsoft.Json;

namespace MAIAIBot.Core.DirectLine
{
    public class Conversation
    {
        [JsonProperty(PropertyName = "conversationId")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "token")]
        public string Token { get; set; }

        [JsonProperty(PropertyName = "expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty(PropertyName = "streamUrl")]
        public string StreamUrl { get; set; }

        public override string ToString() => JsonConvert.SerializeObject(this);
    }

    public class DirectLineClient
    {
        private const string CreateConversationUrl = "https://directline.botframework.com/v3/directline/conversations";

        private readonly string Secret;

        public DirectLineClient(string secret)
        {
            Secret = secret;
        }

        private async Task<Conversation> CreateConversationAsync()
        {
            Conversation conversation = null;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Secret);

                var responce = await client.PostAsync(CreateConversationUrl, null);
                if (responce.StatusCode != System.Net.HttpStatusCode.OK
                    &&
                    responce.StatusCode != System.Net.HttpStatusCode.Created)
                {
                    throw new Exception($"Cannot create coversation! {await responce.Content.ReadAsStringAsync()}");
                }

                conversation = JsonConvert.DeserializeObject<Conversation>(await responce.Content.ReadAsStringAsync());
            }

            return conversation;
        }

        public async Task SendActivityAsync(Activity activity)
        {
            var conversation = await CreateConversationAsync();

            var stringContent = new StringContent(JsonConvert.SerializeObject(activity));
            stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", conversation.Token);
                var responce = await client.PostAsync($"https://directline.botframework.com/v3/directline/conversations/{conversation.Id}/activities", stringContent);
                if (responce.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    throw new Exception($"Cannot get access token! {await responce.Content.ReadAsStringAsync()} \n");
                }
            }
        }
    }
}
