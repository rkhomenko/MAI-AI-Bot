using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MAIAIBot.Core
{
    public static class Authorization
    {
        public class AccessToken
        {
            [JsonProperty(PropertyName = "token_type")]
            public string TokenType { get; set; }

            [JsonProperty(PropertyName = "expires_in")]
            public int ExpiresIn { get; set; }

            [JsonProperty(PropertyName = "ext_expires_in")]
            public int ExtExpiresIn { get; set; }

            [JsonProperty(PropertyName = "access_token")]
            public string Token { get; set; }
        }

        public static async Task<AccessToken> GetAccessToken(string appId, string appPassword)
        {
            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri("https://login.microsoftonline.com/botframework.com/oauth2/v2.0/token")
            };

            var stringContent = new StringContent($"grant_type=client_credentials&client_id={appId}&client_secret={appPassword}&scope=https%3A%2F%2Fapi.botframework.com%2F.default");
            //stringContent.Headers.Add("Host", "login.microsoftonline.com");
            stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            var responce = await client.PostAsync("https://login.microsoftonline.com/botframework.com/oauth2/v2.0/token", stringContent);

            if (responce.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception($"Cannot get access token! {await responce.Content.ReadAsStringAsync()}");
            }

            var accessToken = JsonConvert.DeserializeObject<AccessToken>(await responce.Content.ReadAsStringAsync());

            client.Dispose();

            return accessToken;
        }
    }
}
