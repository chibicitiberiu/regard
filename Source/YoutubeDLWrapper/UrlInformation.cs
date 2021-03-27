using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace YoutubeDLWrapper
{
    public enum UrlType
    {
        [Description("video")] Video,
        [Description("playlist")] Playlist,
        [Description("multi_video")] MultiVideo,
        [Description("url")] Url,
        [Description("url_transparent")] UrlTransparent
    }

    public class UrlInformation
    {
        [JsonProperty("_type")]
        [JsonConverter(typeof(EnumDescriptionConverter<UrlType>))]
        public UrlType Type { get; set; } = UrlType.Video;

        /// <summary>
        /// Video identifier
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Video title, unescaped
        /// </summary>
        public string Title { get; set; }

        // TODO: formats

        /// <summary>
        /// Final video URL.
        /// </summary>
        public Uri Url { get; set; }

        /// <summary>
        /// Video filename extension.
        /// </summary>
        public string Ext { get; set; }

        /// <summary>
        /// The video format, defaults to ext (used for --get-format)
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// SWF Player URL (used for rtmpdump).
        /// </summary>
        [JsonProperty("player_url")]
        public Uri PlayerUrl { get; set; }

        /// <summary>
        /// Opt: A secondary title of the video.
        /// </summary>
        [JsonProperty("alt_title")]
        public string AltTitle { get; set; }

        /// <summary>
        /// Opt: An alternative identifier for the video, not necessarily unique, but available before title.
        /// Typically, id is something like "4234987", title "Dancing naked mole rats", and display_id "dancing-naked-mole-rats"
        /// </summary>
        [JsonProperty("display_id")]
        public string DisplayId { get; set; }

        /// <summary>
        /// Opt: Thumbnails
        /// </summary>
        public Thumbnail[] Thumbnails { get; set; }

        /// <summary>
        /// Opt: Full URL to a video thumbnail image.
        /// </summary>
        public Uri Thumbnail { get; set; }

        /// <summary>
        /// Opt: Full video description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Opt: Full name of the video uploader.
        /// </summary>
        public string Uploader { get; set; }

        /// <summary>
        /// Opt: License name the video is licensed under.
        /// </summary>
        public string License { get; set; }

        /// <summary>
        /// Opt: The creator of the video.
        /// </summary>
        public string Creator { get; set; }

        /// <summary>
        /// Opt: The date (YYYYMMDD) when the video was released.
        /// </summary>
        [JsonProperty("release_date")]
        [JsonConverter(typeof(YoutubeDLDateConverter))]
        public DateTime ReleaseDate { get; set; }

        /// <summary>
        /// Opt: UNIX timestamp of the moment the video became available.
        /// </summary>
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Opt: Video upload date (YYYYMMDD). 
        /// If not explicitly set, calculated from timestamp.
        /// </summary>
        [JsonProperty("upload_date")]
        [JsonConverter(typeof(YoutubeDLDateConverter))]
        public DateTime UploadDate { get; set; }

        /// <summary>
        /// Opt: Nickname or id of the video uploader.
        /// </summary>
        [JsonProperty("uploader_id")]
        public string UploaderId { get; set; }

        /// <summary>
        /// Opt: Full URL to a personal webpage of the video uploader.
        /// </summary>
        [JsonProperty("uploader_url")]
        public Uri UploaderUrl { get; set; }

        /// <summary>
        /// Opt: Full name of the channel the video is uploaded on. 
        /// Note that channel fields may or may not repeat uploader
        /// fields. This depends on a particular extractor.
        /// </summary>
        public string Channel { get; set; }

        /// <summary>
        /// Opt: Id of the channel.
        /// </summary>
        [JsonProperty("channel_id")]
        public string ChannelId { get; set; }

        /// <summary>
        /// Opt: Full URL to a channel webpage.
        /// </summary>
        [JsonProperty("channel_url")]
        public Uri ChannelUrl { get; set; }

        /// <summary>
        /// Physical location where the video was filmed.
        /// </summary>
        public string Location { get; set; }

        // TODO: subtitles: The available subtitles as a dictionary in the format
        //                {tag: subformats}. "tag" is usually a language code, and
        //                "subformats" is a list sorted from lower to higher
        //                preference, each element is a dictionary with the "ext"
        //                entry and one of:
        //                    * "data": The subtitles file contents
        //                    * "url": A URL pointing to the subtitles file
        //                "ext" will be calculated from URL if missing

        // TODO: automatic_captions: Like 'subtitles', used by the YoutubeIE for
        //                automatically generated captions

        /// <summary>
        /// Opt: Length of the video in seconds, as an integer or float.
        /// </summary>
        public double? Duration { get; set; }

        /// <summary>
        /// Opt: How many users have watched the video on the platform.
        /// </summary>
        [JsonProperty("view_count")]
        public int? ViewCount { get; set; }

        /// <summary>
        /// Opt: Number of positive ratings of the video
        /// </summary>
        [JsonProperty("like_count")]
        public int? LikeCount { get; set; }

        /// <summary>
        /// Opt: Number of negative ratings of the video
        /// </summary>
        [JsonProperty("dislike_count")]
        public int? DislikeCount { get; set; }

        /// <summary>
        /// Opt: Number of reposts of the video
        /// </summary>
        [JsonProperty("repost_count")]
        public int? RepostCount { get; set; }

        /// <summary>
        /// Opt: Average rating give by users, the scale used depends on the webpage
        /// </summary>
        [JsonProperty("average_rating")]
        public double? AverageRating { get; set; }

        /// <summary>
        /// Opt: Number of comments on the video
        /// </summary>
        [JsonProperty("comment_count")]
        public int? CommentCount { get; set; }

        // TODO: comments:       A list of comments, each with one or more of the following
        //                properties (all but one of text or html optional):
        //                    * "author" - human-readable name of the comment author
        //                    * "author_id" - user ID of the comment author
        //                    * "id" - Comment ID
        //                    * "html" - Comment as HTML
        //                    * "text" - Plain text of the comment
        //                    * "timestamp" - UNIX timestamp of comment
        //                    * "parent" - ID of the comment this one is replying to.
        //                                 Set to "root" to indicate that this is a
        //                                 comment to the original video.

        /// <summary>
        /// Age restriction for the video, as an integer (years)
        /// </summary>
        [JsonProperty("age_limit")]
        public int? AgeLimit { get; set; }

        /// <summary>
        /// The URL to the video webpage, if given to youtube-dl it 
        /// should allow to get the same result again. (It will be set
        /// by YoutubeDL if it's missing)
        /// </summary>
        [JsonProperty("webpage_url")]
        public Uri WebpageUrl { get; set; }

        /// <summary>
        /// A list of categories that the video falls in,
        /// for example ["Sports", "Berlin"]
        /// </summary>
        public string[] Categories { get; set; }

        /// <summary>
        /// A list of tags assigned to the video, e.g. ["sweden", "pop music"]
        /// </summary>
        public string[] Tags { get; set; }

        /// <summary>
        /// True, False, or None (=unknown). Whether this video is a live stream that goes on instead of a fixed-length video.
        /// </summary>
        [JsonProperty("is_live")]
        public bool? IsLive { get; set; }

        /// <summary>
        /// Time in seconds where the reproduction should start, as specified in the URL.
        /// </summary>
        [JsonProperty("start_time")]
        public int? StartTime { get; set; }

        /// <summary>
        /// Time in seconds where the reproduction should end, as specified in the URL.
        /// </summary>
        [JsonProperty("end_time")]
        public int? EndTime { get; set; }

        // TODO: chapters:       A list of dictionaries, with the following entries:
        //                    * "start_time" - The start time of the chapter in seconds
        //                    * "end_time" - The end time of the chapter in seconds
        //                    * "title" (optional, string)

        //The following fields should only be used when the video belongs to some logical
        //chapter or section:
        //chapter:        Name or title of the chapter the video belongs to.
        //chapter_number: Number of the chapter the video belongs to, as an integer.
        //chapter_id:     Id of the chapter the video belongs to, as a unicode string.

        //The following fields should only be used when the video is an episode of some
        //series, programme or podcast:
        //series:         Title of the series or programme the video episode belongs to.
        //season:         Title of the season the video episode belongs to.
        //season_number:  Number of the season the video episode belongs to, as an integer.
        //season_id:      Id of the season the video episode belongs to, as a unicode string.
        //episode:        Title of the video episode. Unlike mandatory video title field,
        //                this field should denote the exact title of the video episode
        //                without any kind of decoration.
        //episode_number: Number of the video episode within a season, as an integer.
        //episode_id:     Id of the video episode, as a unicode string.

        //The following fields should only be used when the media is a track or a part of
        //a music album:
        //track:          Title of the track.
        //track_number:   Number of the track within an album or a disc, as an integer.
        //track_id:       Id of the track (useful in case of custom indexing, e.g. 6.iii),
        //                as a unicode string.
        //artist:         Artist(s) of the track.
        //genre:          Genre(s) of the track.
        //album:          Title of the album the track belongs to.
        //album_type:     Type of the album (e.g. "Demo", "Full-length", "Split", "Compilation", etc).
        //album_artist:   List of all artists appeared on the album (e.g.
        //                "Ash Borer / Fell Voices" or "Various Artists", useful for splits
        //                and compilations).
        //disc_number:    Number of the disc or other physical medium the track belongs to,
        //                as an integer.
        //release_year:   Year (YYYY) when the album was released.


        //        _type "playlist" indicates multiple videos.


        //There must be a key "entries", which is a list, an iterable, or a PagedList
        //object, each element of which is a valid dictionary by this specification.
        //Additionally, playlists can have "id", "title", "description", "uploader",
        //"uploader_id", "uploader_url", "duration" attributes with the same semantics
        //as videos (see above).

        /// <summary>
        /// Playlist items
        /// </summary>
        public UrlInformation[] Entries { get; set; }

        //_type "multi_video" indicates that there are multiple videos that
        //form a single show, for examples multiple acts of an opera or TV episode.
        //It must have an entries key like a playlist and contain all the keys
        //required for a video at the same time.

        //_type "url" indicates that the video must be extracted from another
        //location, possibly by a different extractor. Its only required key is:
        //"url" - the next URL to extract.

        //The key "ie_key" can be set to the class name (minus the trailing "IE",
        //e.g. "Youtube") if the extractor class is known in advance.
        //Additionally, the dictionary may have any properties of the resolved entity
        //known in advance, for example "title" if the title of the referred video is
        //known ahead of time.

        //_type "url_transparent" entities have the same specification as "url", but
        //indicate that the given additional information is more precise than the one
        //associated with the resolved URL.
        //This is useful when a site employs a video service that hosts the video and
        //its technical metadata, but that video service does not embed a useful
        //title, description etc.
    }
}
