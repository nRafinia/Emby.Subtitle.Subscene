using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using MediaBrowser.Model.Providers;

namespace Emby.Subtitle.Subscene.Providers
{
    public partial class SubsceneSubtitleProvider 
    {
        public async Task<List<RemoteSubtitleInfo>> SearchMovie(string title, int? year, string lang, string movieId)
        {
            var res = new List<RemoteSubtitleInfo>();

            if (!string.IsNullOrWhiteSpace(movieId))
            {
                var mDb = new MovieDb(_jsonSerializer, _httpClient, _appHost);
                var info = await mDb.GetMovieInfo(movieId);

                if (info != null)
                {
                    year = info.release_date.Year;
                    title = info.Title;
                    _logger?.Info($"Subscene= Original movie title=\"{info.Title}\", year={info.release_date.Year}");
                }
            }

            var html = await SearchSubSceneMovie(title, year, lang);
            if (string.IsNullOrWhiteSpace(html))
                return res;

            res = ExtractMovieSubtitleLinks(html, lang);

            return res;
        }

        private List<RemoteSubtitleInfo> ExtractMovieSubtitleLinks(string html, string lang)
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

        private async Task<string> SearchSubSceneMovie(string title, int? year, string lang)
        {
            #region Search subscene

            _logger?.Debug($"Subscene= Searching for site search \"{title}\"");
            var url = string.Format(SearchUrl, HttpUtility.UrlEncode(title));
            var html = await GetHtml(Domain, url);

            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            var xml = new XmlDocument();
            xml.LoadXml($"{XmlTag}{html}");

            var xNode = xml.SelectSingleNode("//div[@class='search-result']");
            if (xNode == null)
                return string.Empty;

            var ex = xNode?.SelectSingleNode("h2[@class='exact']")
                     ?? xNode?.SelectSingleNode("h2[@class='close']")
                     ?? xNode?.SelectSingleNode("h2[@class='popular']");

            if (ex == null)
                return string.Empty;

            xNode = xNode.SelectSingleNode("ul");
            if (xNode == null)
                return string.Empty;

            var sItems = xNode.SelectNodes(".//a");

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

            #endregion

            return html;
        }

    }
}