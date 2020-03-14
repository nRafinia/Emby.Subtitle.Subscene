using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Emby.Subtitle.Subscene.Providers;
using MediaBrowser.Common.Net;

namespace Test
{
    class Program
    {
        private const string Domain = "https://subscene.com";
        private const string SearchApi = "{0}/subtitles/searchbytitle?query={1}&l=";

        static async Task Main(string[] args)
        {
            var txt = "آکوامن";
            var lang = "per";
            var searchResult =await new SubsceneSubtitleProvider(null,null,null,null, null)
                .Search(txt,1969,lang, "tt1477834");

            Console.WriteLine("Result:");
            foreach (var item in searchResult)
            {
                Console.WriteLine($"{item.Name} - {item.Id}");
            }

            /*await new SubsceneSubtitleProvider(null, null, null, null).GetSubtitles(
                "__subtitles__bright__farsi_persian__1922088___per", CancellationToken.None);*/
            

            Console.ReadKey();
        }
    }
}
