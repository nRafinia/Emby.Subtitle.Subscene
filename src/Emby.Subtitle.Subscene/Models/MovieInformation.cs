﻿using System;
using MediaBrowser.Common.Net;

namespace Emby.Subtitle.Subscene.Models
{
    public class MovieInformation
    {
        public int Id { get; set; }

        public string Imdb_Id { get; set; }

        public string Title { get; set; }

        public string Original_Title { get; set; }

        public DateTime release_date { get; set; }
    }
}