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

    public class VisitInfo
    {
        public DateTime Date { get; set; }

        public bool Visited { get; set; }
    }

    public class Student
    {
        public Student(string name,
                       string group,
                       List<string> imgUrls,
                       StudentChannelInfo channelInfo,
                       bool isTheacher = false)
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
            Group = group;
            ImgUrls = imgUrls;
            IsTeacher = isTheacher;
            ChannelInfo = channelInfo;
            Visits = new List<VisitInfo>();
            Variants = new List<int>();
        }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        public string Name { get; set; }

        public string Group { get; set; }

        public bool IsTeacher { get; set; }

        public List<string> ImgUrls { get; set; }

        public List<VisitInfo> Visits { get; set; }

        public List<int> Variants { get; set; }

        public StudentChannelInfo ChannelInfo { get; set; }

        public override string ToString()
            => JsonConvert.SerializeObject(this);

        public void AddVisit(DateTime dateTime, bool visited)
        {
            if (Visits == null) {
                Visits = new List<VisitInfo>();
            }
            Visits.Add(new VisitInfo
            {
                Date = dateTime,
                Visited = visited
            });
        }

        public void UpdateLastVisit(bool visited)
        {
            var lastVisit = Visits[Visits.Count - 1];
            lastVisit.Visited = visited;
        }

        public void AddVariant(int variant)
        {
            if (Variants == null) {
                Variants = new List<int>();
            }
            Variants.Add(variant);
        }
    }
}
