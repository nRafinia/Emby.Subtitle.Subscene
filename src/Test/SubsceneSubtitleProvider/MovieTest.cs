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

namespace Test.SubsceneSubtitleProvider
{
    public class MovieTest
    {
        [Fact]
        public async Task Search_Movie_Success()
        {
            //arrange
            var jsonSerializer = new Mock<IJsonSerializer>();
            var httpClient = new Mock<IHttpClient>();
            var applicationHost = new Mock<IApplicationHost>();

            var movieResult = await Tools.RequestUrl(
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

            var provider = new Emby.Subtitle.Subscene.Providers.SubsceneSubtitleProvider(
                httpClient.Object,
                null,
                applicationHost.Object,
                null,
                jsonSerializer.Object);

            //act
            var res = await provider.SearchMovie(
                "The Hitman's Wife's Bodyguard",
                2021,
                "per",
                "tt8385148");

            //assert
            Assert.NotEmpty(res);
        }
    }
}