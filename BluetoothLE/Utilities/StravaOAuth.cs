using BluetoothLE.Components.Pages;
using BluetoothLE.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Caching.Memory;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using static BluetoothLE.Components.Pages.TokenExchange;
using static System.ArgumentException;

namespace BluetoothLE.Utilities
{
    public class StravaOAuth(ILogger<StravaOAuth> Logger, IMemoryCache MemoryCache, IDbContextFactory<Models.AthleteContext> DbFactory, IConfiguration Config)
    {
        public class TokenResponse
        {
            public string refresh_token { get; set; } = string.Empty;
            public string access_token { get; set; } = string.Empty;
            public int expires_in { get; set; }
            public int expires_at { get; set; }
        }

        public record UploadResponse
        {
            public long id { get; set; }
            public string id_str { get; set; } = string.Empty;
            public string external_id { get; set; } = string.Empty;
            public string? error { get; set; } = string.Empty;
            public string status { get; set; } = string.Empty;
            public long? activity_id { get; set; }
        }

        private static readonly string AccessTokenKey = "AccessToken";
        private static readonly string RefreshTokenKey = "RefreshToken";
        private static readonly string RefreshTimeKey = "RefreshTime";
        private static readonly string CurrentIdKey = "CurrentId";

        public async Task Authorize(int id, string scope, string code, string state)
        {
            ThrowIfNullOrEmpty(Config["Strava:ClientID"], "Strava:ClientID");
            ThrowIfNullOrEmpty(Config["Strava:ClientSecret"], "Strava:ClientSecret");
            
            Logger.LogInformation($"Received authorisation code: {code}");

            using var client = new HttpClient();

            using var response = await client.PostAsync("https://www.strava.com/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>()
            {
                ["client_id"] = Config["Strava:ClientId"]!,
                ["client_secret"] = Config["Strava:ClientSecret"]!,
                ["code"] = code,
                ["grant_type"] = "authorization_code"
            }));

            var json = await JsonSerializer.DeserializeAsync<TokenResponse>(await response.Content.ReadAsStreamAsync());

            if (json is null || string.IsNullOrEmpty(json.access_token) || string.IsNullOrEmpty(json.refresh_token) || json.expires_in <= 0)
            {
                Logger.LogError($"Unable to parse JSON: {json}");

                return;
            }

            Logger.LogInformation($"Refresh Token: {json.refresh_token}; Access Token: {json.access_token}");

            await SaveTokens(id, json.access_token, DateTime.UtcNow.AddSeconds(json.expires_in), json.refresh_token);
        }

        public async Task<string?> GetAccessToken(int id)
        {
            if (MemoryCache.TryGetValue(CurrentIdKey, out int? currentId) && id == currentId)
            {
                if (MemoryCache.TryGetValue(AccessTokenKey, out string? accessToken) && !string.IsNullOrEmpty(accessToken))
                {
                    if (MemoryCache.TryGetValue(RefreshTimeKey, out DateTime? refreshTime) && refreshTime > DateTime.UtcNow)
                    {
                        return accessToken;
                    }
                    else if (MemoryCache.TryGetValue(RefreshTimeKey, out string? refreshToken) && !string.IsNullOrEmpty(refreshToken))
                    {
                        return await RefreshAccessToken(id);
                    }
                }
            }

            // If we're here, one of these occurred:
            // 1. ID doesn't match
            // 2. Have access token, but it's expired, and don't have a refresh token
            // So reset all of these cached values
            MemoryCache.Remove(CurrentIdKey);
            MemoryCache.Remove(AccessTokenKey);
            MemoryCache.Remove(RefreshTokenKey);
            MemoryCache.Remove(RefreshTimeKey);

            using var context = await DbFactory.CreateDbContextAsync();

            if (await context.AccessTokens.SingleOrDefaultAsync(item => item.Id == id) is StravaAccessTokens dbToken && dbToken.Timestamp > DateTime.UtcNow)
            {
                CacheTokens(id, dbToken);

                return dbToken.AccessToken;
            }

            return await RefreshAccessToken(id);
        }

        public async Task<bool> HasAccessToken(int id)
        {
            return !string.IsNullOrEmpty(await GetAccessToken(id));
        }

        private async Task<string?> RefreshAccessToken(int id)
        {
            if (MemoryCache.TryGetValue("RefreshToken", out string? refreshToken) && !string.IsNullOrEmpty(refreshToken))
            {
                return await AccessToken(refreshToken);
            }

            using var context = await DbFactory.CreateDbContextAsync();

            if (await context.RefreshTokens.SingleOrDefaultAsync(item => item.Id == id) is StravaRefreshTokens dbToken && !string.IsNullOrEmpty(dbToken.RefreshToken))
            {
                return await AccessToken(dbToken.RefreshToken);
            }

            return null;

            async Task<string?> AccessToken(string refreshToken)
            {
                ThrowIfNullOrEmpty(Config["Strava:ClientID"], "Strava:ClientID");
                ThrowIfNullOrEmpty(Config["Strava:ClientSecret"], "Strava:ClientSecret");

                using var httpClient = new HttpClient();

                using var refreshResponse = await httpClient.PostAsync("https://www.strava.com/api/v3/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>()
                {
                    ["client_id"] = Config["Strava:ClientId"]!,
                    ["client_secret"] = Config["Strava:ClientSecret"]!,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken
                }));

                var json = await JsonSerializer.DeserializeAsync<TokenResponse>(await refreshResponse.Content.ReadAsStreamAsync());

                if (json is null || string.IsNullOrEmpty(json.access_token) || string.IsNullOrEmpty(json.refresh_token) || json.expires_in <= 0)
                {
                    Logger.LogError($"Unable to parse JSON: {json}");

                    return null;
                }

                await SaveTokens(id, json.access_token, DateTime.UtcNow.AddSeconds(json.expires_in), json.refresh_token);

                return json.access_token;
            }
        }

        private void CacheTokens(int id, StravaAccessTokens accessToken)
        {
            MemoryCache.Set(CurrentIdKey, id);
            MemoryCache.Set(AccessTokenKey, accessToken.AccessToken);
            MemoryCache.Set(RefreshTimeKey, accessToken.Timestamp);
        }

        private async Task SaveTokens(int id, string accessToken, DateTime timestamp, string refreshToken)
        {
            MemoryCache.Set<string>(AccessTokenKey, accessToken);
            MemoryCache.Set<string>(RefreshTokenKey, refreshToken);
            MemoryCache.Set<DateTime>(RefreshTimeKey, timestamp);
            MemoryCache.Set<int>(CurrentIdKey, id);

            using var context = await DbFactory.CreateDbContextAsync();

            int numUpdated = await context.AccessTokens.Where(item => item.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(b => b.AccessToken, accessToken)
                    .SetProperty(b => b.Timestamp, timestamp));

            if (numUpdated == 0)
            {
                context.AccessTokens.Add(new()
                {
                    Id = id,
                    AccessToken = accessToken,
                    Timestamp = timestamp
                });
            }

            numUpdated = await context.RefreshTokens.Where(item => item.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(b => b.RefreshToken, refreshToken));

            if (numUpdated == 0)
            {
                context.RefreshTokens.Add(new()
                {
                    Id = id,
                    RefreshToken = refreshToken
                });
            }

            await context.SaveChangesAsync();
        }

        public async Task UploadAsync(int id, string name, Stream stream, string externalId, string sportType)
        {
            string? accessToken = await GetAccessToken(id);

            if (string.IsNullOrEmpty(accessToken))
            {
                Logger.LogInformation("Unable to acquire an access token for Strava, activity NOT saved!");

                return;
            }

            if (stream.Length == 0)
            {
                Logger.LogError("Nothing to upload");

                return;
            }

            if (stream.Position != 0 && stream.CanSeek == false)
            {
                Logger.LogError("Unable to seek to beginning of stream");

                return;
            }

            stream.Seek(0, SeekOrigin.Begin);

            using var httpClient = new HttpClient();

            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var content = new MultipartFormDataContent();

            content.Add(new System.Net.Http.StreamContent(stream), "file", "workout.fit");
            content.Add(new StringContent("fit"), "data_type");
            content.Add(new StringContent(sportType), "sport_type");
            if (sportType == "Ride")
            {
                content.Add(new StringContent("1"), "trainer");
            }
            content.Add(new StringContent(name), "name");
            content.Add(new StringContent(externalId), "external_id");

            using var response = await httpClient.PostAsync("https://www.strava.com/api/v3/uploads", content);

            if (response.IsSuccessStatusCode == false)
            {
                var body = await response.Content.ReadAsStringAsync();

                Logger.LogError($"Failed to submit activity: {response.StatusCode}");
                Logger.LogError($"{body}");
            }
            else
            {
                var body = await JsonSerializer.DeserializeAsync<UploadResponse>(await response.Content.ReadAsStreamAsync());

                Logger.LogInformation($"Successfully submitted activity!");
                Logger.LogInformation($"{body}");
            }
        }
    }
}
