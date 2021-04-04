using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Regard.Frontend.Utils
{
    public static class HtmlUtils
    {
        public static string FormatAsHtml(this string text)
        {
            var output = new StringBuilder();

            // Encode to HTML (security risk)
            string input = HttpUtility.HtmlEncode(text);

            // Normalize line endings
            input = input.Replace("\r\n", "\n");

            // Make URLs - may not cover all edge cases, but should be fine
            input = Regex.Replace(input,
                @"((http|ftp|https):\/\/[\w\-_]+(\.[\w\-_]+)+([\w\-\.,@?^=%&amp;:/~\+#]*[\w\-\@?^=%&amp;/~\+#])?)",
                "<a target='_blank' href='$1'>$1</a>");

            // Make paragraphs
            foreach (var paragraph in input.Split('\n'))
                output.Append($"<p>{paragraph.Trim()}</p>");

            return output.ToString();
        }
    }
}
