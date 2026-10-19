using System;

namespace YandexDisk.Client.Tests
{
    public static class Url
    {
        /// <summary>
        /// Escapes "/" and "," in the query the way the client does (RFC 3986, upper-case hex).
        /// </summary>
        public static string EscapePath(string url)
        {
            int queryStart = url.IndexOf('?');

            if (queryStart < 0)
                return url;

            string query = url.Substring(queryStart + 1)
                .Replace("/", Uri.EscapeDataString("/"))
                .Replace(",", Uri.EscapeDataString(","));

            return url.Substring(0, queryStart + 1) + query;
        }
    }
}
