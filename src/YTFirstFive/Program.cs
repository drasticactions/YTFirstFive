using System.Text.RegularExpressions;
using ConsoleAppFramework;
using Xabe.FFmpeg;
using YoutubeExplode;
using YoutubeExplode.Playlists;
using YoutubeExplode.Videos.Streams;

var app = ConsoleApp.Create();

app.Add("download-handle-random",
    async ([Argument] string handle, int clipLength = 5, int totalClips = 50, CancellationToken cancellationToken = default) =>
    {
        var youtubeService = new YoutubeClient();
        Console.WriteLine($"Fetching ID for {handle}");
        var channel = await youtubeService.Channels.GetByUserAsync(handle);
        if (channel is null)
        {
            Console.WriteLine("Channel not found.");
            return;
        }
        Console.WriteLine($"Fetching uploads for {channel.Title}...");
        var videos = youtubeService.Channels.GetUploadsAsync(channel.Id);
        var youtubePath = FileHelpers.ConvertToValidFilename(handle);
        Directory.CreateDirectory(youtubePath);
        await DownloadVideoRandomAsync(youtubeService, videos, youtubePath, clipLength, totalClips);
    });


app.Add("download-handle",
    async ([Argument] string handle, int clipLength = 5, CancellationToken cancellationToken = default) =>
    {
        var youtubeService = new YoutubeClient();
        Console.WriteLine($"Fetching ID for {handle}");
        var channel = await youtubeService.Channels.GetByUserAsync(handle);
        if (channel is null)
        {
            Console.WriteLine("Channel not found.");
            return;
        }
        Console.WriteLine($"Fetching uploads for {channel.Title}...");
        var videos = youtubeService.Channels.GetUploadsAsync(channel.Id);
        var youtubePath = FileHelpers.ConvertToValidFilename(handle);
        Directory.CreateDirectory(youtubePath);
        await DownloadVideoAsync(youtubeService, videos, youtubePath, clipLength);
    });

app.Add("download-playlist", async ([Argument] string id, int clipLength = 5, CancellationToken cancellationToken = default) =>
{
    var youtubeService = new YoutubeClient();
    Console.WriteLine($"Fetching playlist {id}...");
    var videos = youtubeService.Playlists.GetVideosAsync(id);
    var youtubePath = FileHelpers.ConvertToValidFilename(id);
    Directory.CreateDirectory(FileHelpers.ConvertToValidFilename(id));
    await DownloadVideoAsync(youtubeService, videos, youtubePath, clipLength);
});

await app.RunAsync(args);

async Task DownloadVideoRandomAsync(YoutubeClient youtubeService, IAsyncEnumerable<PlaylistVideo> videos, string youtubePath, int clipLength, int count)
{
    var list = new List<PlaylistVideo>();
    Console.WriteLine("Fetching all videos...");
    await foreach (var video in videos)
    {
        list.Add(video);
    }
    Random random = new Random();
    for (int i = 0; i < count; i++)
    {
        int randomIndex = random.Next(list.Count);
        var vid = list[randomIndex];
        await DownloadVideoItem(random, youtubeService, vid, youtubePath, clipLength);
    }
}

async Task DownloadVideoAsync(YoutubeClient youtubeService, IAsyncEnumerable<PlaylistVideo> videos, string youtubePath, int clipLength)
{
    Random random = new Random();
    await foreach (var video in videos)
    {
        await DownloadVideoItem(random, youtubeService, video, youtubePath, clipLength);
    }
}

async Task DownloadVideoItem(Random random, YoutubeClient youtubeService, PlaylistVideo video, string youtubePath, int clipLength)
{
    Console.WriteLine($"Downloading {video.Title}...");
    var realFilename = FileHelpers.ConvertToValidFilename($"{video.Id}_{Guid.NewGuid()}.mp4");
    var realPath = Path.Combine(youtubePath, realFilename);
    try
    {
        var manifest = await youtubeService.Videos.Streams.GetManifestAsync(video.Id);
        var streams = manifest.GetMuxedStreams();
        var streamInfo = streams.OrderByDescending(x => x.VideoQuality).Where(n => n.VideoResolution.Width <= 720).FirstOrDefault();
        if (streamInfo is null)
            return;

        var seekTime = TimeSpan.FromSeconds(random.Next(0, (int)(video.Duration!.Value.TotalSeconds))).ToFFmpeg();
        string arguments = $"-ss {seekTime} -i \"{streamInfo.Url}\" -t {clipLength} {realPath}";
        var result = await FFmpeg.Conversions.New().Start(arguments);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex);
        return;
    }
}

public static class FileHelpers
{
    public static string ConvertToValidFilename(string input)
    {
        // Remove invalid characters
        string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string invalidReStr = string.Format(@"[{0}]+", invalidChars);
        string sanitizedInput = Regex.Replace(input, invalidReStr, "_");

        // Trim leading/trailing whitespaces and dots
        sanitizedInput = sanitizedInput.Trim().Trim('.');

        // Ensure filename is not empty
        if (string.IsNullOrEmpty(sanitizedInput))
        {
            return "_";
        }

        // Ensure filename is not too long
        int maxFilenameLength = 255;
        if (sanitizedInput.Length > maxFilenameLength)
        {
            sanitizedInput = sanitizedInput.Substring(0, maxFilenameLength);
        }

        return sanitizedInput;
    }
}