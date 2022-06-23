using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace Emby.Subtitle.Subscene.Providers
{
    public partial class SubsceneSubtitleProvider : ISubtitleProvider, IHasOrder
    {
        private const string Domain = "https://subscene.com";
        private const string SubtitleUrl = "/subtitles/{0}/{1}";
        private const string SearchUrl = "/subtitles/searchbytitle?query={0}&l=";
        private const string XmlTag = "<?xml version=\"1.0\" ?>";

        public string Name => Plugin.StaticName;

        public IEnumerable<VideoContentType> SupportedMediaTypes =>
            new List<VideoContentType>()
            {
                VideoContentType.Movie,
                VideoContentType.Episode
            };

        public int Order => 0;


        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IApplicationHost _appHost;
        private readonly ILocalizationManager _localizationManager;
        private readonly IJsonSerializer _jsonSerializer;

        public SubsceneSubtitleProvider(IHttpClient httpClient, ILogger logger, IApplicationHost appHost
            , ILocalizationManager localizationManager, IJsonSerializer jsonSerializer)
        {
            _httpClient = httpClient;
            _logger = logger;
            _appHost = appHost;
            _localizationManager = localizationManager;
            _jsonSerializer = jsonSerializer;
        }

        private HttpRequestOptions BaseRequestOptions => new HttpRequestOptions
        {
            UserAgent = $"Emby/{_appHost.ApplicationVersion}"
        };

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            var ids = id.Split(new[] { "___" }, StringSplitOptions.RemoveEmptyEntries);
            var url = ids[0].Replace("__", "/");
            var lang = ids[1];

            _logger?.Info($"Subscene= Request for subtitle= {url}");

            var html = await GetHtml(Domain, url);
            if (string.IsNullOrWhiteSpace(html))
                return new SubtitleResponse();

            var startIndex = html.IndexOf("<div class=\"download\">");
            var endIndex = html.IndexOf("</div>", startIndex);

            var downText = html.SubStr(startIndex, endIndex);
            startIndex = downText.IndexOf("<a href=\"");
            endIndex = downText.IndexOf("\"", startIndex + 10);

            var downloadLink = downText.SubStr(startIndex + 10, endIndex - 1);

            _logger?.Debug($"Subscene= Downloading subtitle= {downloadLink}");

            var opts = BaseRequestOptions;
            opts.Url = $"{Domain}/{downloadLink}";

            var ms = new MemoryStream();
            try
            {
                using var response = await _httpClient.GetResponse(opts).ConfigureAwait(false);
                _logger?.Info("Subscene=" + response.ContentType);
                var contentType = response.ContentType.ToLower();
                if (!contentType.Contains("zip"))
                {
                    return new SubtitleResponse()
                    {
                        Stream = ms
                    };
                }

                var archive = new ZipArchive(response.Content);

                var item = (archive.Entries.Count > 1
                    ? archive.Entries.FirstOrDefault(a => a.FullName.ToLower().Contains("utf"))
                    : archive.Entries.First()) ?? archive.Entries.First();

                await item.Open().CopyToAsync(ms).ConfigureAwait(false);
                ms.Position = 0;

                var fileExt = item.FullName.Split('.').LastOrDefault();

                if (string.IsNullOrWhiteSpace(fileExt))
                {
                    fileExt = ".srt";
                }
                return new SubtitleResponse
                {
                    Format = fileExt[1..],
                    Language = NormalizeLanguage(lang),
                    Stream = ms
                };
            }
            catch (Exception e)
            {
                //
            }

            return new SubtitleResponse()
            {
                Stream = Stream.Null
            };
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request,
            CancellationToken cancellationToken)
        {
            var prov = request.ProviderIds?.FirstOrDefault(p => p.Key.ToLower() == "imdb") ??
                       request.ProviderIds?.FirstOrDefault(p =>
                           p.Key.ToLower() == "imdb" || p.Key.ToLower() == "tmdb" || p.Key.ToLower() == "tvdb");

            if (prov == null)
                return new List<RemoteSubtitleInfo>();

            if (request.ContentType == VideoContentType.Episode &&
                (request.ParentIndexNumber == null || request.IndexNumber == null))
                return new List<RemoteSubtitleInfo>();

            var title = request.ContentType == VideoContentType.Movie
                ? request.Name
                : request.SeriesName;

            var res = await Search(title, request.ProductionYear, request.Language, request.ContentType,
                prov.Value.Value,
                request.ParentIndexNumber ?? 0, request.IndexNumber ?? 0);

            _logger?.Debug($"Subscene= result found={res?.Count()}");
            return res;
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(string title, int? year, string lang,
            VideoContentType contentType, string movieId, int season, int episode)
        {
            _logger?.Info(
                $"Subscene= Request subtitle for '{title}', language={lang}, year={year}, movie Id={movieId}, Season={season}, Episode={episode}");

            var res = new List<RemoteSubtitleInfo>();
            try
            {
                res = contentType == VideoContentType.Movie
                    ? await SearchMovie(title, year, lang, movieId)
                    : await SearchTV(title, year, lang, movieId, season, episode);

                if (!res.Any())
                    return res;
            }
            catch (Exception e)
            {
                _logger?.Error(e.Message, e);
            }

            res.RemoveAll(l => string.IsNullOrWhiteSpace(l.Name));

            res = res.GroupBy(s => s.Id)
                .Select(s => new RemoteSubtitleInfo()
                {
                    Id = s.First().Id,
                    Name = $"{s.First().ProviderName} ({s.First().Author})",
                    Author = s.First().Author,
                    ProviderName = "Subscene",
                    Comment = string.Join("<br/>", s.Select(n => n.Name)),
                    //Format = "srt"
                }).ToList();
            return res.OrderBy(s => s.Name);
        }

        private string RemoveExtraCharacter(string text) =>
            text?.Replace("\r\n", "")
                .Replace("\t", "")
                .Trim();

        private async Task<string> GetHtml(string domain, string path)
        {
            var html = await Tools.RequestUrl(
                domain,
                path,
                HttpMethod.Get,
                null,
                new Dictionary<string, string>()
                {
                    { "User-Agent", $"Emby/{_appHost?.ApplicationVersion}" }
                });

            var scIndex = html.IndexOf("<script");
            while (scIndex >= 0)
            {
                var scEnd = html.IndexOf("</script>", scIndex + 1);
                var end = scEnd - scIndex + 9;
                html = html.Remove(scIndex, end);
                scIndex = html.IndexOf("<script");
            }

            scIndex = html.IndexOf("&#");
            while (scIndex >= 0)
            {
                var scEnd = html.IndexOf(";", scIndex + 1);
                var end = scEnd - scIndex + 1;
                var word = html.Substring(scIndex, end);
                html = html.Replace(word, System.Net.WebUtility.HtmlDecode(word));
                scIndex = html.IndexOf("&#");
            }

            html = html.Replace("&nbsp;", "");
            html = html.Replace("&amp;", "Xamp;");
            html = html.Replace("&", "&amp;");
            html = html.Replace("Xamp;", "&amp;");
            html = html.Replace("--->", "---");
            html = html.Replace("<---", "---");
            html = html.Replace("<--", "--");
            html = html.Replace("Xamp;", "&amp;");
            html = html.Replace("<!DOCTYPE html>", "");
            return html;
        }

        private string NormalizeLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                var culture = _localizationManager?.FindLanguageInfo(language.AsSpan());
                if (culture != null)
                {
                    return culture.ThreeLetterISOLanguageName;
                }
            }

            return language;
        }

        private string MapLanguage(string lang)
        {
            switch (lang.ToLower())
            {
                case "fa":
                case "per":
                    lang = "farsi_persian";
                    break;
                case "ar":
                case "ara":
                    lang = "arabic";
                    break;
                case "en":
                case "eng":
                    lang = "english";
                    break;
                case "my":
                case "bur":
                    lang = "burmese";
                    break;
                case "da":
                case "dan":
                    lang = "danish";
                    break;
                case "nl":
                case "dut":
                    lang = "dutch";
                    break;
                case "he":
                case "heb":
                    lang = "hebrew";
                    break;
                case "id":
                case "ind":
                    lang = "indonesian";
                    break;
                case "ko":
                case "kor":
                    lang = "korean";
                    break;
                case "ms":
                case "may":
                    lang = "malay";
                    break;
                case "es":
                case "spa":
                    lang = "spanish";
                    break;
                case "vi":
                case "vie":
                    lang = "vietnamese";
                    break;
                case "tr":
                case "tur":
                    lang = "turkish";
                    break;
                case "bn":
                case "ben":
                    lang = "bengali";
                    break;
                case "bg":
                case "bul":
                    lang = "bulgarian";
                    break;
                case "hr":
                case "hrv":
                    lang = "croatian";
                    break;
                case "fi":
                case "fin":
                    lang = "finnish";
                    break;
                case "fr":
                case "fre":
                    lang = "french";
                    break;
                case "de":
                case "ger":
                    lang = "german";
                    break;
                case "el":
                case "gre":
                    lang = "greek";
                    break;
                case "hu":
                case "hun":
                    lang = "hungarian";
                    break;
                case "it":
                case "ita":
                    lang = "italian";
                    break;
                case "ku":
                case "kur":
                    lang = "kurdish";
                    break;
                case "mk":
                case "mac":
                    lang = "macedonian";
                    break;
                case "ml":
                case "mal":
                    lang = "malayalam";
                    break;
                case "nn":
                case "nno":
                case "nb":
                case "nob":
                case "no":
                case "nor":
                    lang = "norwegian";
                    break;
                case "pt":
                case "por":
                    lang = "portuguese";
                    break;
                case "ru":
                case "rus":
                    lang = "russian";
                    break;
                case "sr":
                case "srp":
                    lang = "serbian";
                    break;
                case "si":
                case "sin":
                    lang = "sinhala";
                    break;
                case "sl":
                case "slv":
                    lang = "slovenian";
                    break;
                case "sv":
                case "swe":
                    lang = "swedish";
                    break;
                case "th":
                case "tha":
                    lang = "thai";
                    break;
                case "ur":
                case "urd":
                    lang = "urdu";
                    break;
                case "pt-br":
                case "pob":
                    lang = "brazillian-portuguese";
                    break;
            }

            return lang;
        }
    }
}