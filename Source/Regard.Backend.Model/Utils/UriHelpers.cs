using System;
using System.Collections.Generic;
using System.Text;

namespace Regard.Backend.Common.Utils
{
    public static class UriHelpers
    {
        public static Uri Join(this Uri baseUri, params Uri[] uris)
        {
            var builder = new StringBuilder();
            builder.Append(baseUri.ToString().TrimEnd('/'));

            foreach (var uri in uris)
            {
                if (uri.IsAbsoluteUri)
                    throw new ArgumentException("Except for baseUri, all other URIs must be relative");

                builder.Append('/');
                builder.Append(uri.ToString().Trim('/'));
            }

            return new Uri(builder.ToString(), baseUri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
        }

        public static Uri Join(this Uri baseUri, params string[] parts)
        {
            var builder = new StringBuilder();
            builder.Append(baseUri.ToString().TrimEnd('/'));

            foreach (var part in parts)
            {
                builder.Append('/');
                builder.Append(part.Trim('/'));
            }

            return new Uri(builder.ToString(), baseUri.IsAbsoluteUri ? UriKind.Absolute : UriKind.Relative);
        }
    }
}
