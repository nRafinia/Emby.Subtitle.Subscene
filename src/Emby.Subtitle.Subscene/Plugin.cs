using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Emby.Subtitle.Subscene
{
    public class Plugin : BasePlugin, IHasThumbImage
    {
        public override Guid Id => new Guid("b342d758-3671-4003-a095-fbca4adb463b");

        public override string Name => StaticName;
        public static string StaticName= "Subscene";

        public override string Description => "Download subtitles from Subscene";

        public ImageFormat ThumbImageFormat => ImageFormat.Gif;

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.gif");
        }
    }
}
