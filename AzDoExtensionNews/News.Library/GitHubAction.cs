using System;
using System.Collections.Generic;
using System.Text;

namespace News.Library
{
    public class GitHubAction
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string Publisher { get; set; }
        public string Version { get; set; }
        public DateTime? Updated { get; set; }
        public string RepoUrl { get; set; }
    }
}
