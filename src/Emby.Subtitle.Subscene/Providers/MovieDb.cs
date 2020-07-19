using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Emby.Subtitle.Subscene.Models;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Serialization;
using MovieFinder.Models;

namespace Emby.Subtitle.Subscene.Providers
{
    public class MovieDb
    {
        private const string token = "d9d7bb04fb2c52c2b594c5e30065c23c";// Get https://www.themoviedb.org/ API token
        private readonly string _movieUrl = "https://api.themoviedb.org/3/movie/{0}?api_key={1}";
        private readonly string _tvUrl = "https://api.themoviedb.org/3/tv/{0}?api_key={1}";
        private readonly string _searchMovie = "https://api.themoviedb.org/3/find/{0}?api_key={1}&external_source={2}";

        private readonly IJsonSerializer _jsonSerializer;
        private readonly IHttpClient _httpClient;
        private readonly IApplicationHost _appHost;
        public MovieDb(IJsonSerializer jsonSerializer, IHttpClient httpClient, IApplicationHost appHost)
        {
            _jsonSerializer = jsonSerializer;
            _httpClient = httpClient;
            _appHost = appHost;
        }

        public async Task<MovieInformation> GetMovieInfo(string id)
        {
            var opts = BaseRequestOptions;
            opts.Url = string.Format(_movieUrl, id, token);

            /*var searchResults = await Tools.RequestUrl<MovieInformation>(opts.Url, "", HttpMethod.Get);
            return searchResults;*/
            using (var response = await _httpClient.GetResponse(opts)/*.ConfigureAwait(false)*/)
            {
                if (response.ContentLength < 0)
                    return null;

                var searchResults = _jsonSerializer.DeserializeFromStream<MovieInformation>(response.Content);

                return searchResults;
            }
        }

        public async Task<FindMovie> SearchMovie(string id)
        {
            var opts = BaseRequestOptions;
            var type = id.StartsWith("tt") ? MovieSourceType.imdb_id : MovieSourceType.tvdb_id;
            opts.Url = string.Format(_searchMovie, id, token, type.ToString());

            using (var response = await _httpClient.GetResponse(opts).ConfigureAwait(false))
            {
                if (response.ContentLength < 0)
                    return null;

                var searchResults = _jsonSerializer.DeserializeFromStream<FindMovie>(response.Content);

                return searchResults;
            }
        }

        public async Task<TvInformation> GetTvInfo(string id)
        {
            var movie = await SearchMovie(id);
            
            if(movie?.tv_episode_results==null || !movie.tv_episode_results.Any())
                return null;

            var opts = BaseRequestOptions;
            opts.Url = string.Format(_tvUrl, movie.tv_episode_results.First().show_id, token);

            using (var response = await _httpClient.GetResponse(opts).ConfigureAwait(false))
            {
                if (response.ContentLength < 0)
                    return null;

                var searchResults = _jsonSerializer.DeserializeFromStream<TvInformation>(response.Content);

                return searchResults;
            }
        }


        private HttpRequestOptions BaseRequestOptions => new HttpRequestOptions
        {
            UserAgent = $"Emby/{_appHost?.ApplicationVersion}"
        };

    }
}