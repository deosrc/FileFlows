namespace FileFlow.Server.Controllers
{
    using System.Diagnostics;
    using Microsoft.AspNetCore.Mvc;
    using FileFlow.Server;
    using FileFlow.Shared.Models;
    using FileFlow.Server.Helpers;
    using System.Collections.Generic;
    using System.IO;

    [Route("/api/video-file")]
    public class VideoFileController : Controller
    {
        [HttpGet("scan")]
        public IEnumerable<VideoFile> Scan()
        {
            var settings = DbHelper.Single<Settings>();
            if (string.IsNullOrEmpty(settings?.Source))
                return new VideoFile[] { };

            DirectoryInfo dir = new DirectoryInfo(settings.Source);
            if (dir.Exists == false)
                return new VideoFile[] { };

            var extensions = settings.Extensions ?? new[] { "avi", "mp4", "mkv", "divx", "mov", "mpg", "mpeg" };

            List<VideoFile> files = new List<VideoFile>();
            SearchDirectory(dir);

            void SearchDirectory(DirectoryInfo dir)
            {
                try
                {
                    foreach (var subdir in dir.GetDirectories())
                    {
                        SearchDirectory(subdir);
                    }

                    foreach (var file in dir.GetFiles("*.*"))
                    {
                        string extension = file.Extension?.ToLower() ?? "";
                        if (extension.StartsWith("."))
                            extension = extension.Substring(1);
                        if (extensions.Contains(extension) == false)
                            continue;
                        var vidfile = new VideoFile
                        {
                            Name = file.FullName,
                            ShortName = file.Name,
                            Path = file.DirectoryName,
                            Extension = extension
                        };
                        files.Add(vidfile);
                    }
                }
                catch (Exception) { }
            }

            return files;
        }
    }

}