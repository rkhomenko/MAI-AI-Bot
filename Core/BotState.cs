using System.Collections.Generic;

namespace MAIAIBot.Core
{
    public class BotState : Dictionary<string, object>
    {
        private const string IdKey = "id";
        private const string NameKey = "name";
        private const string GroupKey = "group";
        private const string RegistrationCompleteKey = "registration";
        private const string ImgUrlsKey = "imgUrls";

        public BotState()
        {
            this[IdKey] = "";
            this[NameKey] = "";
            this[GroupKey] = "";
            this[ImgUrlsKey] = new List<string>();
            this[RegistrationCompleteKey] = false;
        }

        public string Id
        {
            get => (string)this[IdKey];
            set => this[IdKey] = value;
        }

        public string Name
        {
            get => (string)this[NameKey];
            set => this[NameKey] = value;
        }

        public string Group
        {
            get => (string)this[GroupKey];
            set => this[GroupKey] = value;
        }

        public bool RegistrationComplete
        {
            get => (bool)this[RegistrationCompleteKey];
            set => this[RegistrationCompleteKey] = value;
        }

        public List<string> ImgUrls
        {
            get => (List<string>)this[ImgUrlsKey];
            set => this[ImgUrlsKey] = value;
        }

        public void AddImgUrl(string url)
        {
            ImgUrls.Add(url);
        }
    }
}
