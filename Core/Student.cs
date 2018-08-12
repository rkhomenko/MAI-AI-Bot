using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace MAIAIBot.Core
{
    public class StudentChannelInfo
    {
        public string ToId { get; set; }

        public string ToName { get; set; }

        public string FromId { get; set; }

        public string FromName { get; set; }

        public string ServiceUrl { get; set; }

        public string ChannelId { get; set; }

        public string ConversationId { get; set; }
    }

    public class Student
    {
        public Student(string name,
                       string group,
                       List<string> imgUrls,
                       StudentChannelInfo channelInfo)
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
            Group = group;
            ImgUrls = imgUrls;
            ChannelInfo = channelInfo;
            Visits = null;
            Variants = null;
        }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        public string Name { get; set; }

        public string Group { get; set; }

        public List<string> ImgUrls { get; set; }

        public List<DateTime> Visits { get; set; }

        public List<int> Variants { get; set; }

        public StudentChannelInfo ChannelInfo { get; set; }

        public override string ToString()
            => JsonConvert.SerializeObject(this);
    }
}
