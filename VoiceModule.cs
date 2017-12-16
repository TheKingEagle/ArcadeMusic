using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.WebSocket;
using NAudio.Codecs;
using NAudio;
using NAudio.Wave;
using MediaToolkit.Util;
using VideoLibrary;
using MediaToolkit.Model;
using MediaToolkit;
using YoutubeSearch;

namespace RMSoftware.ModularBot.DCModules
{
    public class VoicemodEntity//entity
    {

        YouTubeVideo uvid(string search, string source, out string thm, out TimeSpan dur, out string Message)
        {


            System.IO.Directory.CreateDirectory(source);
            bool fromurl = false;

            var items = new VideoSearch();
            var searchResults = items.SearchQuery(search, 1);
            string timewithhours = "";

            YouTube youtube = YouTube.Default;
            string url = "";
            search = search.Replace("https://", "http://");
            YouTubeVideo vide = null;
            if (searchResults.Count > 0)
            {


                if (search.StartsWith("http://"))
                {
                    foreach (var item in searchResults)
                    {
                        if (item.Url == search)
                        {
                            string[] timespansplit = item.Duration.Split(':');
                            timewithhours = "";
                            if (timespansplit.Length < 3)
                            {
                                timewithhours = "0:" + item.Duration;
                            }
                            else
                            {
                                timewithhours = item.Duration;
                            }
                            if (TimeSpan.Parse(timewithhours).TotalMinutes > 30)
                            {
                                //Console.WriteLine(items.SearchQuery(search, 1).First().Duration);
                                //Console.WriteLine(TimeSpan.Parse(string.Format("{0:hh:mm:ss}", item.Duration)));
                                Message = "The resulting video was too long.";
                                thm = "";
                                dur = TimeSpan.Zero;
                                return null;
                            }
                            url = item.Url;
                            thm = item.Thumbnail;
                            dur = TimeSpan.Parse(timewithhours);
                            vide = youtube.GetVideo(item.Url);
                            fromurl = true;
                            Message = "Success";
                            return vide;
                        }
                    }
                }

                if (!fromurl)
                {
                    VideoInformation firstvid = searchResults[0];
                    string[] timespansplit = firstvid.Duration.Split(':');

                    if (timespansplit.Length < 3)
                    {
                        timewithhours = "0:" + firstvid.Duration;
                    }
                    else
                    {
                        timewithhours = firstvid.Duration;
                    }
                    if (TimeSpan.Parse(timewithhours).TotalMinutes > 30)
                    {

                        Message = "The resulting video was too long";
                        thm = "";
                        dur = TimeSpan.Zero;
                        return null;
                    }
                    url = firstvid.Url;
                    vide = youtube.GetVideo(url);
                    thm = firstvid.Thumbnail;
                    dur = TimeSpan.Parse(timewithhours);
                    Message = "Success";
                    return vide;
                }






            }
            thm = "";
            dur = TimeSpan.Zero;
            Message = "No result";
            return null;
        }
        public class Pauser
        {
            public static bool isPaused = false;
        }
        public ulong GuildID { get; set; }
        public ulong ChannelID { get; set; }
        public IAudioClient aclient { get; set; }
        public AudioStream streamRemainder { get; set; }
        EmbedBuilder NowPlayingInfo { get; set; }
        public bool paused = false;
        bool Skipped = false;
        long position { get; set; }
        public Queue<MusicQueueItem> Queue = new Queue<MusicQueueItem>();
        public VoicemodEntity()
        {
            IsPlaying = false;
        }
        public bool IsPlaying { get; set; }
       
        
        public async Task Play(ICommandContext Context, [Remainder]string youtubevid = null)
        {
            try
            {

                if (string.IsNullOrEmpty(youtubevid) && IsPlaying)
                {
                    await Context.Channel.SendMessageAsync("You did not specify a link or search.");
                    return;
                }
                if (string.IsNullOrEmpty(youtubevid) && !IsPlaying && Queue.Count == 0)
                {
                    await Context.Channel.SendMessageAsync("You did not specify a link or search.");
                    return;
                }
                if (IsPlaying && !string.IsNullOrEmpty(youtubevid))
                {
                    TimeSpan dur = TimeSpan.Zero;
                    string thm = "";
                    string message = "";
                    YouTubeVideo v = uvid(youtubevid, @"queue", out thm, out dur, out message);
                    if (v == null)
                    {
                        await Context.Channel.SendMessageAsync("Hmm, that didn't work. info: " + message);
                        return;
                    }
                    MusicQueueItem item = new MusicQueueItem(v, @"queue\" + v.FullName, dur, thm);
                    Queue.Enqueue(item);
                    EmbedBuilder qvidinfo = new EmbedBuilder();
                    qvidinfo.WithAuthor("Added to Queue!");
                    qvidinfo.AddField(v.Title, dur);
                    qvidinfo.ImageUrl = thm;
                    await Context.Channel.SendMessageAsync("", false, qvidinfo);
                    return;

                }
                if (!IsPlaying && !string.IsNullOrEmpty(youtubevid))//You will fucking obey me this time;;
                {
                    TimeSpan dur = TimeSpan.Zero;
                    string thm = "";
                    string message = "";
                    YouTubeVideo v = uvid(youtubevid, @"queue", out thm, out dur, out message);
                    if (v == null)
                    {
                        await Context.Channel.SendMessageAsync("Hmm, that didn't work. info: " + message);
                        return;
                    }
                    MusicQueueItem item = new MusicQueueItem(v, @"queue" + v.FullName, dur, thm);
                    Queue.Enqueue(item);
                    if (Queue.Count == 1)
                    {

                        await Context.Channel.SendMessageAsync("Added to queue, but wait, Only one song. Let's play it now.");
                    }

                }
                var vidinfo = Queue.Dequeue();

                string source = @"queue";
                var vide = vidinfo.video;
                byte[] b = vidinfo.video.GetBytes();
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(source) + "\\" + vide.FullName, b);

                var inputFile = new MediaFile { Filename = source + "\\" + vide.FullName };
                var outputFile = new MediaFile { Filename = $"{source + "\\" + vide.FullName}.wav" };

                using (var engine = new Engine())
                {
                    engine.GetMetadata(inputFile);

                    engine.Convert(inputFile, outputFile);

                }
                NowPlayingInfo = new EmbedBuilder();
                NowPlayingInfo.WithAuthor("Now Playing", Context.Client.CurrentUser.GetAvatarUrl(ImageFormat.Auto));
                NowPlayingInfo.AddField(vide.Title, vidinfo.duration);
                NowPlayingInfo.AddField("Voice Channel", "`" + (await Context.Guild.GetVoiceChannelAsync(ChannelID)).Name + "`");
                NowPlayingInfo.ImageUrl = vidinfo.ThumbnailURL;
                NowPlayingInfo.Color = Color.Purple;

                await Context.Channel.SendMessageAsync("", false, NowPlayingInfo.Build());
                using (var output = new NAudio.Wave.WaveFileReader(outputFile.Filename))
                {
                    using (var ms = new System.IO.MemoryStream())
                    {
                        using (var PlayingStream = aclient.CreatePCMStream(AudioApplication.Mixed, null, 150, 0))
                        {
                            using (var resampledAudio = new MediaFoundationResampler(output, new WaveFormat(48000, 16, 2)))
                            {
                                resampledAudio.ResamplerQuality = 50;
                                WaveFileWriter.WriteWavFileToStream(ms, resampledAudio);
                                using (var cvt = new NAudio.Wave.RawSourceWaveStream(ms, new WaveFormat(48000, 2)))
                                {
                                    IsPlaying = true;//stupid fuck
                                    while (true)
                                    {


                                        if (paused)
                                        {
                                            await Task.Delay(20);
                                            continue;
                                        }
                                        
                                        byte[] buffer = new byte[81920];
                                        int r = await cvt.ReadAsync(buffer, 0, buffer.Length);
                                        await PlayingStream.WriteAsync(buffer, 0, r);
                                        if (Skipped)
                                        {
                                            Skipped = false;
                                            break;
                                        }
                                        //await PlayingStream.FlushAsync(new CancellationToken(paused));
                                        if (r == 0)
                                        {
                                            break;
                                        }
                                        
                                    }
                                    await PlayingStream.FlushAsync();
                                }
                            }
                        }
                    }
                }
                IsPlaying = false;//stupid fuck
                LogMessage log = new LogMessage(LogSeverity.Info, "VoiceMOD", "End of Stream. Off to next one");
                Console.WriteLine(log);

                while (Queue.Count >= 1)
                {
                    if (Queue.Count == 0)
                    {
                        break;
                    }
                    //System.Threading.SpinWait.SpinUntil(Stopped.);//wait until it is stopped for sure before calling the next play loop.


                    if (IsPlaying)
                    {
                        await Task.Delay(20);
                        continue;
                    }

                    await Play(Context);
                }
            }
            catch (Exception ex)
            {

                await Context.Channel.SendMessageAsync(ex.Message);
                Console.WriteLine(ex.ToString());
            }

        }

        public async Task disconnect(ICommandContext Context)
        {
            await aclient.StopAsync();
            await Context.Channel.SendMessageAsync("`Leaving voice channel & clearing the queue`");
        }
        public async Task Pause(ICommandContext Context)
        {
            if (paused)
            {
                await Context.Channel.SendMessageAsync("`... Can't pause a paused stream... Can we? I didn't think so...`");
                return;
            }
            paused = true;
            await Context.Channel.SendMessageAsync("`Stream Paused`");

        }
        public async Task Resume(ICommandContext Context)
        {
            if (!paused)
            {
                await Context.Channel.SendMessageAsync("`Check your sound settings m8. The music is already a-playin'`");
                //the voice module suffers from multiple personality disorder.
                return;
            }

            paused = false;
            await Context.Channel.SendMessageAsync("`Stream resumed`");
        }

        public async Task Skip(ICommandContext Context)
        {
            Skipped = true;
            IsPlaying = false;
            await Context.Channel.SendMessageAsync("`Skipping this song`");
        }

        public async Task ShowQueue(ICommandContext Context)
        {
            EmbedBuilder builder = new EmbedBuilder();
            builder.WithAuthor("Up Next!", Context.Client.CurrentUser.GetAvatarUrl(ImageFormat.Auto));
            int i = 0;
            TimeSpan totaltime = new TimeSpan(0, 0, 0, 0, 0);
            foreach (MusicQueueItem item in Queue)
            {
                if (i == 0)
                {
                    builder.ThumbnailUrl = item.ThumbnailURL;
                    builder.AddField("***" + item.video.Title + "***", "Duration: " + item.duration);
                    builder.Color = Color.Green;
                    await Context.Channel.SendMessageAsync("", false, builder.Build());
                    if (Queue.Count > 1)
                    {

                        i++;
                        builder = new EmbedBuilder();
                        builder.WithAuthor("In the Queue!", Context.Client.CurrentUser.GetAvatarUrl(ImageFormat.Auto));
                        builder.Color = Color.DarkBlue;
                    }
                    else
                    {
                        return;
                    }
                    continue;
                }
                if (i > 7)
                {
                    builder.AddField("---", "Plus " + (Queue.Count - i) + " additional track(s)...");
                    break;
                }
                builder.AddField(item.video.Title, "Duration: " + item.duration);
                i++;

            }
            foreach (MusicQueueItem item in Queue)
            {
                totaltime += item.duration;
            }
            if (i == 0)
            {
                builder.WithTitle("In the Queue!");
                builder.AddField("What?", "There is nothing in here!");
            }
            builder.WithFooter("Total duration: " + totaltime.ToString());
            await Context.Channel.SendMessageAsync("", false, builder.Build());
        }
        public async Task NowPlaying(ICommandContext Context)
        {
            if(IsPlaying)
            {
                await Context.Channel.SendMessageAsync("", false, NowPlayingInfo.Build());
            }
            else
            {
                EmbedBuilder notplaying = new EmbedBuilder();
                notplaying.WithAuthor("Nothing is playing", Context.Client.CurrentUser.GetAvatarUrl(ImageFormat.Auto));
                notplaying.WithColor(Color.Red);
                notplaying.WithDescription("There is nothing currently playing on this guild");
                notplaying.AddField("ArcadeBOT Music", "*Only You can prevent the silence*");
                await Context.Channel.SendMessageAsync("", false, notplaying.Build());
            }
        }

    }


    public class VoiceMODService
    {
        public List<VoicemodEntity> VMODEntities = new List<VoicemodEntity>();
       
        public VoicemodEntity GetByGuildID(ulong id)
        {
            try
            {
                LogMessage l = new LogMessage(LogSeverity.Info, "VoiceMOD", VMODEntities.Count.ToString());
                LogMessage le = new LogMessage(LogSeverity.Info, "VoiceMOD", VMODEntities.FirstOrDefault(x => x.GuildID == id)?.GuildID.ToString());
                Console.WriteLine(l);
                Console.WriteLine(le);
                return VMODEntities.FirstOrDefault(x => x.GuildID == id);
            }
            catch (Exception ex)
            {
                LogMessage l = new LogMessage(LogSeverity.Error, "VOICEMOD", ex.Message, ex);
                Console.WriteLine(l);
                return null;
            }
        }

        public async Task<VoicemodEntity> Join(IVoiceChannel channel)
        {
            VoicemodEntity entity = GetByGuildID(channel.GuildId);
            if (entity == null)
            {
                entity = new VoicemodEntity();
            }
            else if (entity.ChannelID != channel.Id && entity.GuildID == channel.GuildId)
            {
                await entity.aclient.StopAsync();
                VMODEntities.Remove(entity);
            }
            entity.ChannelID = channel.Id;
            entity.GuildID = channel.GuildId;
            entity.aclient = await channel.ConnectAsync();

            entity.Queue = new Queue<MusicQueueItem>();
            VMODEntities.Add(entity);
            return entity;
        }
    }

    public class MusicQueueItem
    {
        public YouTubeVideo video { get; set; }
        public string Filepath { get; set; }

        public TimeSpan duration { get; set; }

        public string ThumbnailURL { get; set; }

        public MusicQueueItem(YouTubeVideo v, string path, TimeSpan dur, string thurl)
        {
            video = v;
            Filepath = path;
            duration = dur;
            ThumbnailURL = thurl;
        }
    }

    [Group("music"),Alias("vmod","vm","song","sr","songrequest","mp")]
    public class VoiceModule : ModuleBase
    {
        YouTubeVideo uvid(string search, string source, out string thm, out TimeSpan dur)
        {


            System.IO.Directory.CreateDirectory(source);
            bool fromurl = false;

            var items = new VideoSearch();
            string timewithhours = "";

            YouTube youtube = YouTube.Default;
            string url = "";
            search = search.Replace("https://", "http://");
            YouTubeVideo vide = null;
            if (items.SearchQuery(search, 1).Count > 0)
            {


                if (search.StartsWith("http://"))
                {
                    foreach (var item in items.SearchQuery(search, 1))
                    {
                        if (item.Url == search)
                        {
                            string[] timespansplit = item.Duration.Split(':');
                            timewithhours = "";
                            if (timespansplit.Length < 3)
                            {
                                timewithhours = "0:" + item.Duration;
                            }
                            else
                            {
                                timewithhours = item.Duration;
                            }
                            if (TimeSpan.Parse(timewithhours).TotalMinutes > 30)
                            {
                                //Console.WriteLine(items.SearchQuery(search, 1).First().Duration);
                                //Console.WriteLine(TimeSpan.Parse(string.Format("{0:hh:mm:ss}", item.Duration)));
                                Context.Channel.SendMessageAsync("In order to prevent me from crashing, the video must be under 30 minutes... ");
                                thm = "";
                                dur = TimeSpan.Zero;
                                return null;
                            }
                            url = item.Url;
                            thm = item.Thumbnail;
                            dur = TimeSpan.Parse(timewithhours);
                            vide = youtube.GetVideo(item.Url);
                            fromurl = true;
                            return vide;
                        }
                    }
                }

                if (!fromurl)
                {
                    string[] timespansplit = items.SearchQuery(search, 1).First().Duration.Split(':');

                    if (timespansplit.Length < 3)
                    {
                        timewithhours = "0:" + items.SearchQuery(search, 1).First().Duration;
                    }
                    else
                    {
                        timewithhours = items.SearchQuery(search, 1).First().Duration;
                    }
                    if (TimeSpan.Parse(timewithhours).TotalMinutes > 30)
                    {


                        Context.Channel.SendMessageAsync("In order to prevent me from crashing, the video must be under 30 minutes... ");
                        thm = "";
                        dur = TimeSpan.Zero;
                        return null;
                    }
                    url = items.SearchQuery(search, 1).First().Url;
                    vide = youtube.GetVideo(url);
                    thm = items.SearchQuery(search, 1).First().Thumbnail;
                    dur = TimeSpan.Parse(timewithhours);
                    return vide;
                }






            }
            thm = "";
            dur = TimeSpan.Zero;
            return null;
        }
        VoiceMODService _service { get; set; }

        public VoiceModule(VoiceMODService service)
        {
            _service = service;
        }
        [Command("join", RunMode = RunMode.Async)]
        public async Task join(IVoiceChannel channel = null)
        {

            // Get the audio channel
            channel = channel ?? (Context.Message.Author as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Context.Channel.SendMessageAsync("You need to be in a voice channel!"); return; }
            await _service.Join(channel);
            await Context.Channel.SendMessageAsync("Successfully joined `" + channel.Name + "`.");

        }

        [Command("play", RunMode = RunMode.Async), Alias("add")]
        public async Task play([Remainder]string youtubevid = null)
        {
            var channel = (Context.Message.Author as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Context.Channel.SendMessageAsync("You need to be in a voice channel!"); return; }
            Console.WriteLine(channel.Name);
            VoicemodEntity entity = _service.GetByGuildID(channel.GuildId);
            if (entity == null)
            {
                await Context.Channel.SendMessageAsync("I need to be in a voice channel first.");
                return;
            }
            
            await entity.Play(Context, youtubevid);

        }
        [Command("stop", RunMode = RunMode.Async), Alias("pause")]
        public async Task pause()
        {
            var channel = (Context.Message.Author as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Context.Channel.SendMessageAsync("You need to be in a voice channel!"); return; }
            Console.WriteLine(channel.Name);
            await _service.GetByGuildID(channel.GuildId)?.Pause(Context);

        }
        [Command("skip", RunMode = RunMode.Async), Alias("next")]
        public async Task skip()
        {
            var channel = (Context.Message.Author as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Context.Channel.SendMessageAsync("You need to be in a voice channel!"); return; }
            Console.WriteLine(channel.Name);
            await _service.GetByGuildID(channel.GuildId)?.Skip(Context);

        }
        [Command("resume", RunMode = RunMode.Async)]
        public async Task resume()
        {
            var channel = (Context.Message.Author as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Context.Channel.SendMessageAsync("You need to be in a voice channel!"); return; }
            Console.WriteLine(channel.Name);
            await _service.GetByGuildID(channel.GuildId)?.Resume(Context);

        }
        [Command("disconnect", RunMode = RunMode.Async), Alias("quit","leave")]
        public async Task endConnection()
        {
            var channel = (Context.Message.Author as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Context.Channel.SendMessageAsync("You need to be in a voice channel!"); return; }
            Console.WriteLine(channel.Name);
            await _service.GetByGuildID(channel.GuildId)?.disconnect(Context);
            _service.VMODEntities.Remove(_service.GetByGuildID(channel.GuildId));

        }
        [Command("queue", RunMode = RunMode.Async), Alias("list", "upnext")]
        public async Task ShowQueue()
        {
            var channel = (Context.Message.Author as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Context.Channel.SendMessageAsync("You need to be in a voice channel!"); return; }
            Console.WriteLine(channel.Name);
            await _service.GetByGuildID(channel.GuildId)?.ShowQueue(Context);

        }
        [Command("nowplaying", RunMode = RunMode.Async), Alias("np", "currentsong","csong")]
        public async Task ShowNP()
        {
            var channel = (Context.Message.Author as IGuildUser)?.VoiceChannel;
            if (channel == null) { await Context.Channel.SendMessageAsync("You need to be in a voice channel!"); return; }
            Console.WriteLine(channel.Name);
            await _service.GetByGuildID(channel.GuildId)?.NowPlaying(Context);

        }

        [Command("", RunMode = RunMode.Async)]
        public async Task About()
        {
            EmbedBuilder b = new EmbedBuilder();
            b.WithAuthor("ArcadeBOT Music module", Context.Client.CurrentUser.GetAvatarUrl(ImageFormat.Auto));
            b.WithColor(Color.LightOrange);
            b.Description = "A simple YouTube music player extension for the ArcadeBOT. This module uses your bot's default command prefix.";
            b.AddField("Version", System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
            string commands = "• music join\r\n• music play\r\n• music pause\r\n• music resume\r\n• music disconnect\r\n• music skip\r\n• music queue\r\n• music nowplaying";
            string aliases1 = "• Main group:  vmod, vm, song, sr, songrequest , mp";
            string aliases2 = "• Play:  add\r\n• Pause: stop\r\n• Skip: next\r\n• Queue: list, upnext\r\n• Disconnect: leave, quit\r\n• Nowplaying: np, currentsong, csong";
            b.AddField("Commands", commands);
            b.AddField("Group aliases", aliases1);
            b.AddField("Command aliases", aliases2);
            await Context.Channel.SendMessageAsync("", false, b.Build());

        }

    }
}
