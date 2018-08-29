using System.Collections.Generic;

namespace MAIAIBot.TeachersBot
{
    public class BotState : Dictionary<string, object>
    {
        private const string RegistrationCompleteKey = "registration";
        private const string IdKey = "id";

        public BotState()
        {
            this[RegistrationCompleteKey] = false;
            this[IdKey] = null;
        }

        public bool RegistrationComplete
        {
            get => (bool)this[RegistrationCompleteKey];
            set => this[RegistrationCompleteKey] = value;
        }

        public string Id
        {
            get => (string)this[IdKey];
            set => this[IdKey] = value;
        }
    }
}