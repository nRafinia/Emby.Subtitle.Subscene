using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using Emby.Subtitle.Subscene.Models;
using MediaBrowser.Model.Providers;

namespace Emby.Subtitle.Subscene.Providers
{
    public partial class SubsceneSubtitleProvider
    {
        private readonly string[] _seasonNumbers =
            {"", "First", "Second", "Third", "Fourth", "Fifth", "Sixth", "Seventh", "Eighth", "Ninth"};

        private async Task<List<RemoteSubtitleInfo>> SearchTV(string title, int? year, string lang, string movieId,
            int season, int episode)
        {
            var res = new List<RemoteSubtitleInfo>();

            var mDb = new MovieDb(_jsonSerializer, _httpClient, _appHost);
            var info = await mDb.GetTvInfo(movieId);

            if (info == null)
                return new List<RemoteSubtitleInfo>();

            var html = await SearchSubSceneTvShow(info, season, lang);
            if (string.IsNullOrWhiteSpace(html))
                return res;

            return ExtractTvShowSubtitleLinks(html, lang);
        }

        private async Task<string> SearchSubSceneTvShow(TvInformation info, int season,  string lang)
        {
            #region Search TV Shows

            var title = info.Name;

            _logger?.Debug($"Subscene= Searching for site search \"{title}\"");
            var url = string.Format(SearchUrl, HttpUtility.UrlEncode($"{title} - {_seasonNumbers[season]} Season"));
            var html = await GetHtml(Domain, url);

            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var xml = new XmlDocument();
            xml.LoadXml($"{XmlTag}{html}");

            var xNode = xml.SelectSingleNode("//div[@class='search-result']");
            if (xNode == null)
                return string.Empty;

            var ex = xNode?.SelectSingleNode("h2[@class='exact']")
                     ?? xNode?.SelectSingleNode("h2[@class='close']");
            if (ex == null)
                return string.Empty;

            xNode = xNode.SelectSingleNode("ul");
            if (xNode == null)
                return string.Empty;

            var sItems = xNode.SelectNodes(".//a");
            foreach (XmlNode item in sItems)
            {
                if (!item.InnerText.StartsWith($"{title} - {_seasonNumbers[season]} Season"))
                    continue;

                var link = item.Attributes["href"].Value;
                link += $"/{MapLanguage(lang)}";
                html = await GetHtml(Domain, link);
                break;
            }

            #endregion

            return html;
        }

        private List<RemoteSubtitleInfo> ExtractTvShowSubtitleLinks(string html, string lang)
        {
            var res = new List<RemoteSubtitleInfo>();
            #region Extract subtitle links

            var xml = new XmlDocument();
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

            #endregion

            return res;
        }
    }
}