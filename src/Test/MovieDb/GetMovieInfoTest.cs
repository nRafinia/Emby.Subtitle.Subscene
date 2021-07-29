using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Emby.Subtitle.Subscene.Models;
using Emby.Subtitle.Subscene.Providers;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Serialization;
using Moq;
using Newtonsoft.Json;
using Xunit;
using HttpRequestOptions = MediaBrowser.Common.Net.HttpRequestOptions;

namespace Test.MovieDb
{
    public class GetMovieInfoTest
    {
        [Fact]
        public async Task Get_Request_Success()
        {
            //arrange
            var jsonSerializer = new Mock<IJsonSerializer>();
            var httpClient = new Mock<IHttpClient>();
            var applicationHost = new Mock<IApplicationHost>();

            jsonSerializer
                .Setup(m => m
                    .DeserializeFromStreamAsync<MovieInformation>(It.IsAny<Stream>())
                )
                .ReturnsAsync(() => new MovieInformation()
                {
                    release_date = DateTime.Now,
                    Title = "Test"
                });

            httpClient
                .Setup(m => m
                    .GetResponse(It.IsAny<HttpRequestOptions>()))
                .ReturnsAsync(() => new HttpResponseInfo()
                {
                    ContentLength = 1,
                    Content = new MemoryStream()
                });

            applicationHost
                .Setup(m => m
                    .ApplicationVersion
                )
                .Returns(Version.Parse("1.0"));

            var movieDb = new Emby.Subtitle.Subscene.Providers.MovieDb(
                jsonSerializer.Object,
                httpClient.Object,
                applicationHost.Object);

            //act
            var getTvInfo = await movieDb.GetMovieInfo("123");

            //assert
            Assert.Equal("Test", getTvInfo.Title);
        }

        [Fact]
        public async Task Get_Request_Success2()
        {
            //arrange
            var jsonSerializer = new Mock<IJsonSerializer>();
            var httpClient = new Mock<IHttpClient>();
            var applicationHost = new Mock<IApplicationHost>();

            var movieResult = MovieResult;

            jsonSerializer
                .Setup(m => m
                    .DeserializeFromStreamAsync<MovieInformation>(It.IsAny<Stream>())
                )
                .ReturnsAsync(() => JsonConvert.DeserializeObject<MovieInformation>(movieResult));

            var stream = movieResult.GenerateStreamFromString();

            httpClient
                .Setup(m => m
                    .GetResponse(It.IsAny<HttpRequestOptions>()))
                .ReturnsAsync(() => new HttpResponseInfo()
                {
                    ContentLength = stream.Length,
                    Content = stream
                });

            applicationHost
                .Setup(m => m
                    .ApplicationVersion
                )
                .Returns(Version.Parse("1.0"));

            var movieDb = new Emby.Subtitle.Subscene.Providers.MovieDb(
                jsonSerializer.Object,
                httpClient.Object,
                applicationHost.Object);

            //act
            var getTvInfo = await movieDb.GetMovieInfo("123");

            //assert
            Assert.Equal("Hitman's Wife's Bodyguard", getTvInfo.Title);
        }

        [Fact]
        public async Task Get_Request_Success3()
        {
            //arrange
            var jsonSerializer = new Mock<IJsonSerializer>();
            var httpClient = new Mock<IHttpClient>();
            var applicationHost = new Mock<IApplicationHost>();

            var movieResult =await Tools.RequestUrl(
                "https://api.themoviedb.org/",
                "3/movie/tt8385148?api_key=d9d7bb04fb2c52c2b594c5e30065c23c",
                HttpMethod.Get
                );

            jsonSerializer
                .Setup(m => m
                    .DeserializeFromStreamAsync<MovieInformation>(It.IsAny<Stream>())
                )
                .ReturnsAsync(() => JsonConvert.DeserializeObject<MovieInformation>(movieResult));

            var stream = movieResult.GenerateStreamFromString();

            httpClient
                .Setup(m => m
                    .GetResponse(It.IsAny<HttpRequestOptions>()))
                .ReturnsAsync(() => new HttpResponseInfo()
                {
                    ContentLength = stream.Length,
                    Content = stream
                });

            applicationHost
                .Setup(m => m
                    .ApplicationVersion
                )
                .Returns(Version.Parse("1.0"));

            var movieDb = new Emby.Subtitle.Subscene.Providers.MovieDb(
                jsonSerializer.Object,
                httpClient.Object,
                applicationHost.Object);

            //act
            var getTvInfo = await movieDb.GetMovieInfo("123");

            //assert
            Assert.Equal("Hitman's Wife's Bodyguard", getTvInfo.Title);
        }

       
        private const string MovieResult = @"{
  ""adult"": false,
  ""backdrop_path"": ""/bjIPzixuWnOzxDG25WaXKuy9lYZ.jpg"",
  ""belongs_to_collection"": {
    ""id"": 608101,
    ""name"": ""The Hitman's Bodyguard Collection"",
    ""poster_path"": ""/7LSrP9MURgzqHFFzcQ8jndUasaF.jpg"",
    ""backdrop_path"": ""/xMCIniDuIfJFk7XoRvYRm80lRKR.jpg""
  },
  ""budget"": 50000000,
  ""genres"": [
    {
      ""id"": 28,
      ""name"": ""Action""
    },
    {
      ""id"": 35,
      ""name"": ""Comedy""
    },
    {
      ""id"": 53,
      ""name"": ""Thriller""
    }
  ],
  ""homepage"": ""https://www.thehitmanswifesbodyguard.movie"",
  ""id"": 522931,
  ""imdb_id"": ""tt8385148"",
  ""original_language"": ""en"",
  ""original_title"": ""Hitman's Wife's Bodyguard"",
  ""overview"": ""The world’s most lethal odd couple – bodyguard Michael Bryce and hitman Darius Kincaid – are back on another life-threatening mission. Still unlicensed and under scrutiny, Bryce is forced into action by Darius's even more volatile wife, the infamous international con artist Sonia Kincaid. As Bryce is driven over the edge by his two most dangerous protectees, the trio get in over their heads in a global plot and soon find that they are all that stand between Europe and a vengeful and powerful madman."",
  ""popularity"": 239.547,
  ""poster_path"": ""/6zwGWDpY8Zu0L6W4SYWERBR8Msw.jpg"",
  ""production_companies"": [
    {
      ""id"": 1020,
      ""logo_path"": ""/kuUIHNwMec4dwOLghDhhZJzHZTd.png"",
      ""name"": ""Millennium Films"",
      ""origin_country"": ""US""
    },
    {
      ""id"": 48738,
      ""logo_path"": null,
      ""name"": ""Campbell Grobman Films"",
      ""origin_country"": ""US""
    },
    {
      ""id"": 1632,
      ""logo_path"": ""/cisLn1YAUuptXVBa0xjq7ST9cH0.png"",
      ""name"": ""Lionsgate"",
      ""origin_country"": ""US""
    },
    {
      ""id"": 74795,
      ""logo_path"": null,
      ""name"": ""Nu Boyana Film Studios"",
      ""origin_country"": """"
    },
    {
      ""id"": 2801,
      ""logo_path"": ""/bswb1PLLsKDUXMLgy42bZtCtIde.png"",
      ""name"": ""Film i Väst"",
      ""origin_country"": ""SE""
    },
    {
      ""id"": 18230,
      ""logo_path"": null,
      ""name"": ""Filmgate Films"",
      ""origin_country"": ""SE""
    },
    {
      ""id"": 29326,
      ""logo_path"": ""/48DThnoazC9wvKo8SieLzICIX0m.png"",
      ""name"": ""Dutch Filmworks"",
      ""origin_country"": ""NL""
    }
  ],
  ""production_countries"": [
    {
      ""iso_3166_1"": ""US"",
      ""name"": ""United States of America""
    }
  ],
  ""release_date"": ""2021-06-14"",
  ""revenue"": 64782768,
  ""runtime"": 100,
  ""spoken_languages"": [
    {
      ""english_name"": ""English"",
      ""iso_639_1"": ""en"",
      ""name"": ""English""
    }
  ],
  ""status"": ""Released"",
  ""tagline"": ""Killer threesome."",
  ""title"": ""Hitman's Wife's Bodyguard"",
  ""video"": false,
  ""vote_average"": 7.0,
  ""vote_count"": 356
}";
    }
}