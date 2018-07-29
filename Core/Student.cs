using System;

using Newtonsoft.Json;

namespace MAIAIBot.Core
{
    public class Student
    {
        public Student(string id,
                       string name,
                       string group,
                       string[] imgUrls)
        {
            Id = id;
            Name = name;
            Group = group;
            ImgUrls = imgUrls;
            Visits = null;
            Variants = null;
        }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string Name { get; set; }
        public string Group { get; set; }
        public string[] ImgUrls { get; set; }
        public DateTime[] Visits { get; set; }
        public int[] Variants { get; set; }

        public override string ToString()
            => JsonConvert.SerializeObject(this);
    }
}
