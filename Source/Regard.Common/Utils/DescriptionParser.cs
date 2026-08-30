using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Regard.Common.Utils
{
    public enum DescBlockKind
    {
        Paragraph,
        Heading,
        UnorderedList,
        OrderedList,
    }

    public enum DescInlineKind
    {
        Text,
        Link,
        Timestamp,
        LineBreak,
    }

    /// <summary>A run of inline content. Text is always literal — never markup.</summary>
    public class DescInline
    {
        public DescInlineKind Kind { get; set; }

        /// <summary>Literal text to display. Null for <see cref="DescInlineKind.LineBreak"/>.</summary>
        public string Text { get; set; }

        /// <summary>Destination for <see cref="DescInlineKind.Link"/>.</summary>
        public string Url { get; set; }

        /// <summary>Seek target in seconds for <see cref="DescInlineKind.Timestamp"/>.</summary>
        public double Seconds { get; set; }

        public bool Bold { get; set; }

        public bool Italic { get; set; }
    }

    /// <summary>One line of a list, or one paragraph/heading.</summary>
    public class DescLine
    {
        public List<DescInline> Inlines { get; } = new List<DescInline>();
    }

    public class DescBlock
    {
        public DescBlockKind Kind { get; set; }

        /// <summary>1-6 for <see cref="DescBlockKind.Heading"/>; ignored otherwise.</summary>
        public int Level { get; set; }

        /// <summary>One entry per list item; exactly one for a paragraph or heading.</summary>
        public List<DescLine> Lines { get; } = new List<DescLine>();
    }

    /// <summary>
    /// Turns a video description into a tree of blocks and inline runs.
    ///
    /// The output is deliberately NOT html. The renderer emits real elements from these nodes, so text is
    /// escaped by the framework and nothing a creator writes can become markup. That also lets a
    /// timestamp be a real button with a Blazor click handler instead of needing JS click delegation.
    ///
    /// Inline scanning is a single left-to-right pass rather than a chain of regex replaces. Chaining is
    /// how a URL's "12:34" ends up re-tokenised as a timestamp, or how a link's own text gets linkified a
    /// second time — the pass below consumes URLs first and never revisits what it has emitted.
    /// </summary>
    public static class DescriptionParser
    {
        /// <summary>A description far past this is pathological; parse the head and keep the rest literal.</summary>
        private const int MaxLength = 100_000;

        private static readonly Regex HeadingLine = new Regex(
            @"^(?<hashes>#{1,6})\s+(?<text>.*\S)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // The space after the marker is what separates a bullet from a stray dash or an emphasis marker.
        private static readonly Regex UnorderedLine = new Regex(
            @"^\s{0,3}[-*+]\s+(?<text>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex OrderedLine = new Regex(
            @"^\s{0,3}\d{1,3}[.)]\s+(?<text>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Scheme-qualified or bare www. The character class deliberately includes # and % so a fragment
        // ("...%3F#Options_that_you_should_pass") is swallowed by the URL rather than left behind to be
        // mistaken for a hashtag.
        private static readonly Regex Url = new Regex(
            @"\b(?:https?://|www\.)[^\s<>""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // (?<![\w.]) keeps an e-mail address out: "subtitle@kurzgesagt.org" has a word character before
        // the @, so it is never a handle. That string is in four of the videos in this library.
        private static readonly Regex Handle = new Regex(
            @"(?<![\w.@])@(?<name>[A-Za-z0-9][A-Za-z0-9._-]{2,29})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Same boundary idea: a "#" glued to a preceding word, or followed by punctuation like "#!/user",
        // is not a hashtag.
        private static readonly Regex Hashtag = new Regex(
            @"(?<![\w#])#(?<name>[A-Za-z0-9_]{1,60})(?![\w#])", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Optional hours. The lookarounds stop "1:2:3:4" and the ":00" inside "https://" from matching.
        private static readonly Regex Timestamp = new Regex(
            @"(?<![\d:])(?:(?<h>\d{1,2}):)?(?<m>\d{1,2}):(?<s>\d{2})(?![\d:])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Emphasis requires the marker to hug non-space content, so "2 * 3 * 4" and a "- " bullet stay
        // literal. Bold is tried before italic so "**x**" isn't read as an empty italic.
        private static readonly Regex Bold = new Regex(
            @"(?<!\*)\*\*(?<text>\S(?:[^*]*\S)?)\*\*(?!\*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex Italic = new Regex(
            @"(?<![\*\w])(?<marker>[*_])(?<text>\S(?:[^*_]*\S)?)\k<marker>(?![\*\w])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static List<DescBlock> Parse(string text, bool linkifyYouTube)
        {
            var blocks = new List<DescBlock>();
            if (string.IsNullOrWhiteSpace(text))
                return blocks;

            if (text.Length > MaxLength)
                text = text.Substring(0, MaxLength);

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            DescBlock current = null;

            foreach (var raw in lines)
            {
                string line = raw.TrimEnd();

                if (string.IsNullOrWhiteSpace(line))
                {
                    current = null;                       // a blank line ends whatever block was open
                    continue;
                }

                var heading = HeadingLine.Match(line);
                if (heading.Success)
                {
                    var block = new DescBlock
                    {
                        Kind = DescBlockKind.Heading,
                        Level = heading.Groups["hashes"].Value.Length,
                    };
                    block.Lines.Add(BuildLine(heading.Groups["text"].Value, linkifyYouTube));
                    blocks.Add(block);
                    current = null;                       // a heading is always its own block
                    continue;
                }

                var unordered = UnorderedLine.Match(line);
                if (unordered.Success)
                {
                    current = Continue(blocks, current, DescBlockKind.UnorderedList);
                    current.Lines.Add(BuildLine(unordered.Groups["text"].Value, linkifyYouTube));
                    continue;
                }

                var ordered = OrderedLine.Match(line);
                if (ordered.Success)
                {
                    current = Continue(blocks, current, DescBlockKind.OrderedList);
                    current.Lines.Add(BuildLine(ordered.Groups["text"].Value, linkifyYouTube));
                    continue;
                }

                // Plain text. Consecutive lines join into one paragraph separated by line breaks, which is
                // what makes a chapter list read as a block instead of as a stack of margin-spaced <p>s.
                if (current == null || current.Kind != DescBlockKind.Paragraph)
                {
                    current = new DescBlock { Kind = DescBlockKind.Paragraph };
                    current.Lines.Add(new DescLine());
                    blocks.Add(current);
                }
                else
                {
                    current.Lines[0].Inlines.Add(new DescInline { Kind = DescInlineKind.LineBreak });
                }

                AppendInlines(current.Lines[0].Inlines, line, linkifyYouTube);
            }

            return blocks;
        }

        private static DescBlock Continue(List<DescBlock> blocks, DescBlock current, DescBlockKind kind)
        {
            if (current != null && current.Kind == kind)
                return current;

            var block = new DescBlock { Kind = kind };
            blocks.Add(block);
            return block;
        }

        private static DescLine BuildLine(string text, bool linkifyYouTube)
        {
            var line = new DescLine();
            AppendInlines(line.Inlines, text, linkifyYouTube);
            return line;
        }

        /// <summary>
        /// One left-to-right pass. At each position the earliest-starting candidate wins; whatever it
        /// consumes is emitted as a finished node and never re-scanned, which is what keeps a timestamp
        /// out of a URL and a URL out of a hashtag.
        /// </summary>
        private static void AppendInlines(List<DescInline> output, string text, bool linkifyYouTube)
        {
            int position = 0;

            while (position < text.Length)
            {
                Match best = null;
                int bestKind = 0;   // 1 url, 2 handle, 3 hashtag, 4 timestamp, 5 bold, 6 italic

                void Consider(Regex regex, int kind)
                {
                    var match = regex.Match(text, position);
                    if (!match.Success)
                        return;
                    if (best == null || match.Index < best.Index)
                    {
                        best = match;
                        bestKind = kind;
                    }
                }

                Consider(Url, 1);
                if (linkifyYouTube)
                {
                    Consider(Handle, 2);
                    Consider(Hashtag, 3);
                }
                Consider(Timestamp, 4);
                Consider(Bold, 5);
                Consider(Italic, 6);

                if (best == null)
                {
                    AddText(output, text.Substring(position));
                    return;
                }

                if (best.Index > position)
                    AddText(output, text.Substring(position, best.Index - position));

                switch (bestKind)
                {
                    case 1:
                    {
                        string url = TrimTrailingPunctuation(best.Value);
                        output.Add(new DescInline
                        {
                            Kind = DescInlineKind.Link,
                            Text = url,
                            Url = url.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "https://" + url : url,
                        });
                        position = best.Index + url.Length;
                        break;
                    }
                    case 2:
                    {
                        string name = best.Groups["name"].Value;
                        output.Add(new DescInline
                        {
                            Kind = DescInlineKind.Link,
                            Text = "@" + name,
                            Url = "https://www.youtube.com/@" + Uri.EscapeDataString(name),
                        });
                        position = best.Index + best.Length;
                        break;
                    }
                    case 3:
                    {
                        string name = best.Groups["name"].Value;
                        output.Add(new DescInline
                        {
                            Kind = DescInlineKind.Link,
                            Text = "#" + name,
                            Url = "https://www.youtube.com/hashtag/" + Uri.EscapeDataString(name),
                        });
                        position = best.Index + best.Length;
                        break;
                    }
                    case 4:
                    {
                        int hours = best.Groups["h"].Success ? int.Parse(best.Groups["h"].Value) : 0;
                        int minutes = int.Parse(best.Groups["m"].Value);
                        int seconds = int.Parse(best.Groups["s"].Value);
                        if (seconds > 59 || (best.Groups["h"].Success && minutes > 59))
                        {
                            // Not a real time ("12:99"); keep it as text.
                            AddText(output, best.Value);
                        }
                        else
                        {
                            output.Add(new DescInline
                            {
                                Kind = DescInlineKind.Timestamp,
                                Text = best.Value,
                                Seconds = hours * 3600 + minutes * 60 + seconds,
                            });
                        }
                        position = best.Index + best.Length;
                        break;
                    }
                    case 5:
                    case 6:
                    {
                        var inner = new List<DescInline>();
                        AppendInlines(inner, best.Groups["text"].Value, linkifyYouTube);
                        foreach (var node in inner)
                        {
                            if (bestKind == 5) node.Bold = true; else node.Italic = true;
                            output.Add(node);
                        }
                        position = best.Index + best.Length;
                        break;
                    }
                }
            }
        }

        private static void AddText(List<DescInline> output, string text)
        {
            if (text.Length == 0)
                return;
            output.Add(new DescInline { Kind = DescInlineKind.Text, Text = text });
        }

        /// <summary>
        /// Drops sentence punctuation that a URL regex greedily swallowed, while keeping a closing paren
        /// that belongs to the URL. Wikipedia links depend on this:
        /// "en.wikipedia.org/wiki/Memory_management_(operating_systems)" must survive intact.
        /// </summary>
        private static string TrimTrailingPunctuation(string url)
        {
            while (url.Length > 0)
            {
                char last = url[url.Length - 1];

                if (last == ')')
                {
                    int opens = 0, closes = 0;
                    foreach (char c in url)
                    {
                        if (c == '(') opens++;
                        else if (c == ')') closes++;
                    }
                    if (closes <= opens)
                        break;                                  // balanced — the paren is part of the URL
                }
                else if (".,;:!?'\"".IndexOf(last) < 0)
                {
                    break;
                }

                url = url.Substring(0, url.Length - 1);
            }

            return url;
        }
    }
}
