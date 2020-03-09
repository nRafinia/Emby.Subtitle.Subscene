using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MediaBrowser.Common;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Subtitles;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;

namespace Emby.Subtitle.Subscene.Providers
{
    public class SubsceneSubtitleProvider : ISubtitleProvider, IHasOrder
    {
        private const string Domain = "https://subscene.com";
        private const string SubtitleUrl = "/subtitles/{0}/{1}";
        private const string SearchUrl = "/subtitles/searchbytitle?query={0}&l=";
        private const string XmlTag = "<?xml version=\"1.0\" ?>";

        public string Name => Plugin.StaticName;

        public IEnumerable<VideoContentType> SupportedMediaTypes =>
            new List<VideoContentType>() { VideoContentType.Movie };

        public int Order => 0;


        private readonly IHttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly IApplicationHost _appHost;
        private readonly ILocalizationManager _localizationManager;
        public SubsceneSubtitleProvider(IHttpClient httpClient, ILogger logger, IApplicationHost appHost
            , ILocalizationManager localizationManager)
        {
            _httpClient = httpClient;
            _logger = logger;
            _appHost = appHost;
            _localizationManager = localizationManager;
        }

        private HttpRequestOptions BaseRequestOptions => new HttpRequestOptions
        {
            UserAgent = $"Emby/{_appHost.ApplicationVersion}"
        };

        public async Task<SubtitleResponse> GetSubtitles(string id, CancellationToken cancellationToken)
        {
            _logger?.Info($"Subscene= Request for subtitle= {id}");

            var xml = new XmlDocument();
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

            _logger?.Info($"Subscene= Downloading subtitle= {downloadLink}");

            var opts = BaseRequestOptions;
            opts.Url = $"{Domain}/{downloadLink}";

            using (var response = await _httpClient.GetResponse(opts).ConfigureAwait(false))
            {
                var ms = new MemoryStream();
                var archive = new ZipArchive(response.Content);

                var item = archive.Entries.Count > 1
                    ? archive.Entries.FirstOrDefault(a => a.FullName.Contains("utf"))
                    : archive.Entries.FirstOrDefault();

                await item.Open().CopyToAsync(ms).ConfigureAwait(false);
                ms.Position = 0;

                var fileExt = item.FullName.Split('.').LastOrDefault();

                if (string.IsNullOrWhiteSpace(fileExt))
                {
                    fileExt = "srt";
                }

                return new SubtitleResponse
                {
                    Format = fileExt,
                    Language = NormalizeLanguage(lang),
                    Stream = ms
                };
            }
        }

        public Task<IEnumerable<RemoteSubtitleInfo>> Search(SubtitleSearchRequest request,
            CancellationToken cancellationToken)
        {
            return Search(request.Name, request.ProductionYear, request.Language);
        }

        public async Task<IEnumerable<RemoteSubtitleInfo>> Search(string title, int? year, string lang)
        {
            _logger?.Info($"Subscene= Request subtitle for {title}, language={lang}, year={year}");

            var res = new List<RemoteSubtitleInfo>();
            try
            {
                var xml = new XmlDocument();
                var html = await SearchMovie(title, year, lang);

                if (string.IsNullOrWhiteSpace(html))
                    return res;

                xml.LoadXml($"{XmlTag}{html}");

                var repeater = xml.SelectNodes("//table/tbody/tr");

                if (repeater == null)
                {
                    return res;
                }

                foreach (XmlElement node in repeater)
                {
                    var name = RemoveExtraCharacter(node.SelectSingleNode(".//a")?.SelectNodes("span").Item(1)
                        ?.InnerText);

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    var id = (node.SelectSingleNode(".//a")?.Attributes["href"].Value + "___" + lang)
                        .Replace("/", "__");
                    var item = new RemoteSubtitleInfo
                    {
                        Id = id,
                        Name = RemoveExtraCharacter(node.SelectSingleNode(".//a")?.SelectNodes("span").Item(1)
                            ?.InnerText),
                        Author = RemoveExtraCharacter(node.SelectSingleNode("td[@class='a6']")?.InnerText),
                        ProviderName = RemoveExtraCharacter(node.SelectSingleNode("td[@class='a5']")?.InnerText),
                        ThreeLetterISOLanguageName = NormalizeLanguage(lang),
                        IsHashMatch = true
                    };
                    res.Add(item);
                }
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
                    Name = s.First().ProviderName,
                    Author = s.First().Author,
                    ProviderName = "Subscene",
                    Comment = string.Join("<br/>", s.Select(n => n.Name)),
                }).ToList();
            return res.OrderBy(s => s.Name);
        }

        private async Task<string> SearchMovie(string title, int? year, string lang)
        {
            var sTitle = title
                .Replace('-', ' ')
                .Replace(":", "")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("!", "")
                .Replace("?", "")
                .Replace("#", "")
                .Replace(' ', '-')
                .Replace("-II", "-2")
                .Replace("-III", "-3")
                .Replace("----", "-")
                .Replace("---", "-")
                .Replace("--", "-");

            var url = string.Format(SubtitleUrl, sTitle, MapLanguage(lang));
            var html = await GetHtml(Domain, url);

            if (!string.IsNullOrWhiteSpace(html) || year == null)
                return html;

            _logger?.Info($"Subscene= Searching for subtitle \"{sTitle}-{year}\", language={lang}");
            url = string.Format(SubtitleUrl, $"{sTitle}-{year}", MapLanguage(lang));
            html = await GetHtml(Domain, url);

            if (!string.IsNullOrWhiteSpace(html))
                return html;

            _logger?.Info($"Subscene= Searching for site search \"{title}\"");
            url = string.Format(SearchUrl, title);
            html = await GetHtml(Domain, url);

            if (string.IsNullOrWhiteSpace(html))
                return html;

            var xml = new XmlDocument();
            xml.LoadXml($"{XmlTag}{html}");

            var node = xml.SelectSingleNode("//div[@class='search-result']");
            if (node == null)
                return html;

            var ex = node?.SelectSingleNode("h2[@class='exact']") 
                     ?? node?.SelectSingleNode("h2[@class='close']");

            if (ex == null)
                return html;

            node = node.SelectSingleNode("ul");
            if (node == null)
                return html;
            var sItems = node.SelectNodes(".//a");

            foreach (XmlNode item in sItems)
            {
                var sYear = item.InnerText.Split('(', ')')[1];
                if (year.Value != Convert.ToInt16(sYear))
                    continue;

                var link = item.Attributes["href"].Value;
                link += $"/{MapLanguage(lang)}";
                html = await GetHtml(Domain, link);
                break;
            }

            return html;
        }

        private string RemoveExtraCharacter(string text) =>
            text?.Replace("\r\n", "")
                .Replace("\t", "")
                .Trim();

        private async Task<string> GetHtml(string domain, string path)
        {
            var html = await Tools.RequestUrl(domain, path, HttpMethod.Get).ConfigureAwait(false);

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
                case "per":
                    lang = "farsi_persian";
                    break;
                case "ara":
                    lang = "arabic";
                    break;
                case "eng":
                    lang = "english";
                    break;
                case "bur":
                    lang = "burmese";
                    break;
                case "dan":
                    lang = "danish";
                    break;
                case "dut":
                    lang = "dutch";
                    break;
                case "heb":
                    lang = "hebrew";
                    break;
                case "ind":
                    lang = "indonesian";
                    break;
                case "kor":
                    lang = "korean";
                    break;
                case "may":
                    lang = "malay";
                    break;
                case "spa":
                    lang = "spanish";
                    break;
                case "vie":
                    lang = "vietnamese";
                    break;
                case "tur":
                    lang = "turkish";
                    break;
                case "ben":
                    lang = "bengali";
                    break;
                case "bul":
                    lang = "bulgarian";
                    break;
                case "hrv":
                    lang = "croatian";
                    break;
                case "fin":
                    lang = "finnish";
                    break;
                case "fre":
                    lang = "french";
                    break;
                case "ger":
                    lang = "german";
                    break;
                case "gre":
                    lang = "greek";
                    break;
                case "hun":
                    lang = "hungarian";
                    break;
                case "ita":
                    lang = "italian";
                    break;
                case "kur":
                    lang = "kurdish";
                    break;
                case "mac":
                    lang = "macedonian";
                    break;
                case "mal":
                    lang = "malayalam";
                    break;
                case "nno":
                    lang = "norwegian";
                    break;
                case "por":
                    lang = "portuguese";
                    break;
                case "rus":
                    lang = "russian";
                    break;
                case "srp":
                    lang = "serbian";
                    break;
                case "sin":
                    lang = "sinhala";
                    break;
                case "slv":
                    lang = "slovenian";
                    break;
                case "swe":
                    lang = "swedish";
                    break;
                case "tha":
                    lang = "thai";
                    break;
                case "urd":
                    lang = "urdu";
                    break;
            }

            return lang;
        }
    }
}