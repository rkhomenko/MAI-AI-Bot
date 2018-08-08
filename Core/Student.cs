using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace MAIAIBot.Core
{
    public class Student
    {
        public Student(string id,
                       string name,
                       string group,
                       string[] imgUrls,
                       object connection)
        {
            Id = id;
            Name = name;
            Group = group;
            ImgUrls.AddRange(imgUrls);
            Connection = connection;
            Visits = null;
            Variants = null;
        }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        public string Name { get; set; }

        public string Group { get; set; }

        public List<string> ImgUrls { get; set; }

        public List<DateTime> Visits { get; set; }

        public int[] Variants { get; set; }

        public object Connection { get; set; }

        public override string ToString()
            => JsonConvert.SerializeObject(this);
    }
}
