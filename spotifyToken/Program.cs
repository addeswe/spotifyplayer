using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Polly;
using StackExchange.Redis;

namespace SpotifyCurrent
{
    class Program
    {
        private static string clientId = "";
        private static string clientSecret = "";
        private static string redirectUri = "http://127.0.0.1:5000/callback/";
        private static readonly HttpClient httpClient = new HttpClient();
        private static string accessToken = "";

        // Redis
        private static readonly ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
        private static readonly IDatabase db = redis.GetDatabase();
        private static readonly string redisKey = "spotify:access_token";

        static async Task Main(string[] args)
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryForeverAsync(retryAttempt => TimeSpan.FromSeconds(Math.Min(retryAttempt * 10, 60)),
                    (exception, delay) =>
                    {
                        Console.WriteLine($"Fel i huvudloop: {exception.Message}. Försöker igen om {delay.TotalSeconds} sekunder...");
                    });

            await retryPolicy.ExecuteAsync(async () =>
            {
                while (true)
                {
                    accessToken = await GetOrRefreshAccessToken();
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        Console.WriteLine("Kan inte hämta access token, försöker igen om 5 minuter.");
                    }
                    else
                    {
                        Console.WriteLine("Access token hämtat!");
                    }

                    Console.WriteLine("Väntar 5 minuter till nästa körning...");
                    await Task.Delay(TimeSpan.FromMinutes(5));
                }
            });
        }

        static async Task<string> GetOrRefreshAccessToken()
        {
            clientId = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") ?? string.Empty;
            clientSecret = Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_SECRET") ?? string.Empty;

            string? cached = await db.StringGetAsync(redisKey);
            if (!string.IsNullOrEmpty(cached) && db.KeyTimeToLive(redisKey) > TimeSpan.FromMinutes(5))
            {
                Console.WriteLine("Använder cachat token från Redis.");
                return cached;
            }

            Console.WriteLine("Inget cachat token – hämtar nytt via OAuth...");
            string newToken = await GetAccessTokenViaOAuth();

            if (!string.IsNullOrEmpty(newToken))
            {
                // Spara i Redis med 55 minuters TTL (Spotify ger 60 min)
                await db.StringSetAsync(redisKey, newToken, TimeSpan.FromMinutes(58));
                Console.WriteLine("Token sparat i Redis.");
            }

            return newToken;
        }

        static async Task<string> GetAccessTokenViaOAuth()
        {
            string scope = "user-read-currently-playing";
            string authUrl = $"https://accounts.spotify.com/authorize?client_id={clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={Uri.EscapeDataString(scope)}";

            Console.WriteLine("\n========================================");
            Console.WriteLine("SPOTIFY OAUTH - LOGIN REQUIRED");
            Console.WriteLine("========================================");
            Console.WriteLine("Click this link to authorize:");
            Console.WriteLine($"\n{authUrl}\n");
            Console.WriteLine("========================================\n");

            string code = await ListenForCallback();
            if (string.IsNullOrEmpty(code)) return "";

            var request = new HttpRequestMessage(HttpMethod.Post, "https://accounts.spotify.com/api/token");
            request.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri),
            });
            request.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")));

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Token-fel: {content}");
                return "";
            }

            var json = JsonSerializer.Deserialize<JsonElement>(content);
            return json.GetProperty("access_token").GetString()!;
        }

        static async Task<string> ListenForCallback()
        {
            var listener = new HttpListener();
            listener.Prefixes.Add("http://*:5000/callback/");
            listener.Start();
            Console.WriteLine("Väntar på inloggning i webbläsaren...");

            var context = await listener.GetContextAsync();
            string code = context.Request.QueryString["code"]!;

            var responseString = "<html><body>Inloggad! Du kan stänga detta fönster.<script>setTimeout(function(){ window.close(); }, 1500);</script></body></html>";
            var buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer);
            context.Response.OutputStream.Close();
            listener.Stop();
            return code;
        }
    }
}
