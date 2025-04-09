using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Http.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Unicode;
using static BluetoothLE.Utilities.StravaOAuth;
using static System.ArgumentException;

namespace BluetoothLE.Utilities
{
    public class HttpLogger(ILogger<HttpLogger> Logger) : IHttpClientLogger
    {
        public void LogRequestFailed(object? context, HttpRequestMessage request, HttpResponseMessage? response, Exception exception, TimeSpan elapsed)
        {
            
        }

        public object? LogRequestStart(HttpRequestMessage request)
        {
            Logger.LogInformation($"Sending '{request.Method}' to '{request.RequestUri!.PathAndQuery}");

            Logger.LogInformation("Request Headers:");

            foreach (var header in request.Headers)
            {
                Logger.LogInformation($"  {header.Key}: {string.Join(';', header.Value)}");
            }

            return null;
        }

        public void LogRequestStop(object? context, HttpRequestMessage request, HttpResponseMessage response, TimeSpan elapsed)
        {
            
        }
    }

    public class IntervalsUploader(ILogger<IntervalsUploader> Logger, IHttpClientFactory HttpClientFactory, IConfiguration Config)
    {
        public async Task UploadAsync(int id, string name, Stream stream, string externalId)
        {
            ThrowIfNullOrEmpty(Config["Intervals:ApiKey"], "Intervals:ApiKey");

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

            using var httpClient = HttpClientFactory.CreateClient("my-client");

            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"API_KEY:{Config["Intervals:ApiKey"]}")));

            var content = new MultipartFormDataContent();

            content.Add(new System.Net.Http.StreamContent(stream), "file", "workout.fit");
            content.Add(new StringContent(name), "name");
            content.Add(new StringContent(externalId), "external_id");

            var uri = "https://intervals.icu/api/v1/athlete/0/activities";

            using var response = await httpClient.PostAsync(uri, content);

            if (response.IsSuccessStatusCode == false)
            {
                var body = await response.Content.ReadAsStringAsync();

                Logger.LogError($"Failed to submit activity: {response.StatusCode}");
                Logger.LogError($"{body}");
            }
            else
            {
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Logger.LogInformation("Successfully submitted activity (dup)");
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Created)
                {
                    Logger.LogInformation("Successfully submitted activity (new)");
                }
                else
                {
                    Logger.LogInformation($"Successfully submitted activity!: {response.StatusCode}");
                }
            }
        }
    }
}
