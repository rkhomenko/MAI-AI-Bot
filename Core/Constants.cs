namespace MAIAIBot.Core
{
    public static class Constants
    {
        public const string CognitiveServiceGroupId = "students";
        public const string CognitiveServiceGroupName = "MAI students";
        public const string CognitiveServiceConnectionStrIndex = "CognitiveService";
        public const string CognitiveServiceKeyIndex = "CognitiveService:Key";

        public const string CosmosDbConnectionStrIndex = "CosmosDb";
        public const string CosmosDbKeyIndex = "CosmosDb:Key";
        public const string AzureStorageShareName = "mai-ai-bot-photo";
        public const string AzureStorageImagePrefix = "maiaibotphoto-";
        public const string AzureStorageSasTokenIndex = "AzureStorage:SasToken";
        public const string AzureStorageConnectionStrIndex = "AzureStorage:ConnectionString";

        public static class ActivityTypes
        {
            public const string MyProactive = "myproactive";
        }

        public const string TeachersGroupName = "Teachers";
        public const string AcceptStudentCommand = "Accept";
        public const string NotifyTeacherCommand = "Notify";
        public const string DeclineStudentCommand = "Decline";
        public const string NotifyStudentCommand = "Notification";
    }
}