using System.Collections.Generic;

namespace MAIAIBot.TeachersBot
{
    public class BotState : Dictionary<string, object>
    {
        private const string RegistrationCompleteKey = "registration";

        public BotState()
        {
            this[RegistrationCompleteKey] = false;
        }

        public bool RegistrationComplete
        {
            get => (bool)this[RegistrationCompleteKey];
            set => this[RegistrationCompleteKey] = value;
        }
    }
}