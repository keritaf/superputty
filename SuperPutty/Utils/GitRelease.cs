﻿using System.Runtime.Serialization;

namespace SuperPutty.Utils
{
    [DataContract]
    public class GitRelease
    {
        [DataMember(Name = "tag_name")]
        public string version;
        [DataMember(Name = "html_url")]
        public string release_url;
    }
}
