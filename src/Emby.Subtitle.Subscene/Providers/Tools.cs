using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Emby.Subtitle.Subscene.Providers
{

    public static class Tools
    {
        private static readonly HttpClient _httpClient;

        static Tools()
        {
            _httpClient = new HttpClient();
        }

        public static async Task<string> RequestUrl(string baseUrl, string path, HttpMethod method, object postData = null,
                    Dictionary<string, string> headers = null, int timeout = 10_000)
        {
            var retValue = string.Empty;
            try
            {
                if (!baseUrl.StartsWith("http://") && !baseUrl.StartsWith("https://"))
                    baseUrl = $"http://{baseUrl}";

                var message = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), path));

                if (headers != null && headers.Count > 0)
                {
                    foreach (var header in headers)
                    {
                        message.Headers.Add(header.Key, header.Value);
                    }
                }

                var cancellationToken = new CancellationTokenSource();
                cancellationToken.CancelAfter(timeout);

                var res = await _httpClient.SendAsync(message, cancellationToken.Token);

                if (res == null)
                    return retValue;

                res.EnsureSuccessStatusCode();

                if (!res.IsSuccessStatusCode)
                {
                    /*var cnt = string.Empty;
                    if (res.Content != null)
                        cnt = await res.Content?.ReadAsStringAsync();*/

                    return retValue;
                }

                retValue = await res.Content.ReadAsStringAsync();

            }
            catch
            {
                //
            }

            return retValue;
        }

        /// <summary>
        /// Sub string by start index and end index
        /// </summary>
        /// <param name="value">Original string</param>
        /// <param name="startIndex">Start index</param>
        /// <param name="endIndex">End index</param>
        /// <returns>Sub stringed text</returns>
        public static string SubStr(this string value, int startIndex, int endIndex) =>
            value.Substring(startIndex, (endIndex - startIndex + 1));

        /// <summary>
        /// Convert string to Base64
        /// </summary>
        /// <param name="plainText">String to convert</param>
        /// <returns>Base64 string</returns>
        public static string Base64Encode(this string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        /// <summary>
        /// Convert Base64 to string
        /// </summary>
        /// <param name="base64EncodedData">Base64 strign</param>
        /// <returns>String</returns>
        public static string Base64Decode(this string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
    }
}