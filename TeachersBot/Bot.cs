using System;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Bot;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Core.Extensions;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Schema;

namespace MAIAIBot.TeachersBot
{
    public class Bot : IBot
    {
        private async Task SetTimer(ITurnContext context) {
            var timer = new Timer(5000);
            timer.Elapsed += async (source, e) => await SendProactiveMessage(context);
            timer.AutoReset = false;
            timer.Enabled = true;

            await Task.CompletedTask;
        }

        private async Task SendProactiveMessage(ITurnContext context) {
            var toId = context.Activity.From.Id;
            var toName = context.Activity.From.Name;
            var fromId = context.Activity.Recipient.Id;
            var fromName = context.Activity.Recipient.Name;
            var serviceUrl = context.Activity.ServiceUrl;
            var channelId = context.Activity.ChannelId;
            var conversationId = context.Activity.Conversation.Id;

            var userAccount = new ChannelAccount(toId, toName);
            var botAccount = new ChannelAccount(fromId, fromName);
            var connector = new ConnectorClient(new Uri(serviceUrl));


            IMessageActivity message = Activity.CreateMessageActivity();
            if (!string.IsNullOrEmpty(conversationId) && !string.IsNullOrEmpty(channelId)) {
                message.ChannelId = channelId;
            }
            else
            {
                conversationId = (await connector.Conversations.CreateDirectConversationAsync(
                    botAccount, userAccount)).Id;
            }

            message.From = botAccount;
            message.Recipient = userAccount;
            message.Conversation = new ConversationAccount(id: conversationId);
            message.Text = "Proactive message recived!";
            message.Locale = "en-us";

            await connector.Conversations.SendToConversationAsync(message as Activity);
        }

        public async Task OnTurn(ITurnContext context)
        {
            switch (context.Activity.Type)
            {
                case ActivityTypes.Message:
                    await SetTimer(context);
                    await context.SendActivity("Time for proactive message send to 5 seconds.");
                    break;
            }
        }
    }
}