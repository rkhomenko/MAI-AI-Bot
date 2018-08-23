using System;
using Newtonsoft.Json;

namespace MAIAIBot.Core
{
    public class Message
    {
        public class FromToInfo
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName = "role")]
            public string Role { get; set; }

            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }
        }

        public class ConversationInfo
        {
            [JsonProperty(PropertyName = "id")]
            public string Id { get; set; }
        }

        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty(PropertyName = "serviceUrl")]
        public string ServiceUrl { get; set; }

        [JsonProperty(PropertyName = "channelId")]
        public string ChannelId;

        [JsonProperty(PropertyName = "from")]
        public FromToInfo From;

        [JsonProperty(PropertyName = "conversation")]
        public ConversationInfo Conversation { get; set; }

        [JsonProperty(PropertyName = "recipient")]
        public FromToInfo Recipient { get; set; }

        [JsonProperty(PropertyName = "text")]
        public string Text { get; set; }

        public Message()
        {
            Type = Constants.ActivityTypes.MyProactive;
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffffffK");
        }

        public override string ToString()
            => JsonConvert.SerializeObject(this);
    }
}
