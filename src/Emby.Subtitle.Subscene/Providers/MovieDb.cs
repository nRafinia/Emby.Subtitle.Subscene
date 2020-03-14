using System.Threading.Tasks;
using Emby.Subtitle.Subscene.Models;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Serialization;

namespace Emby.Subtitle.Subscene.Providers
{
    public class MovieDb
    {
        private const string token = "API Token";// Get https://www.themoviedb.org/ API token

        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;
        private readonly IApplicationHost _appHost;
        public MovieDb(IJsonSerializer jsonSerializer, IHttpClient httpClient, IApplicationHost appHost)
        {
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _appHost = appHost;
        }

        public async Task<MovieInformation> GetInfo(string id)
        {
            var opts = BaseRequestOptions;
            opts.Url = GetServiceUrl(id);

            using (var response = await _httpClient.GetResponse(opts).ConfigureAwait(false))
            {
                if (response.ContentLength < 0)
                    return null;

                var searchResults = _jsonSerializer.DeserializeFromStream<MovieInformation>(response.Content);

                return searchResults;
            }
        }

        private HttpRequestOptions BaseRequestOptions => new HttpRequestOptions
        {
            UserAgent = $"Emby/{_appHost.ApplicationVersion}"
        };

        private string GetServiceUrl(string id)
        {
            return $"https://api.themoviedb.org/3/movie/{id}?api_key={token}";
        }
    }
}