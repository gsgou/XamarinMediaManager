﻿using System;
using System.Threading.Tasks;
using System.Timers;
using Android.Content;
using Android.Media;
using Android.Runtime;
using Android.Support.V4.Media.Session;
using Com.Google.Android.Exoplayer2;
using Com.Google.Android.Exoplayer2.Ext.Mediasession;
using Com.Google.Android.Exoplayer2.Source;
using Com.Google.Android.Exoplayer2.Trackselection;
using Com.Google.Android.Exoplayer2.Upstream;
using Com.Google.Android.Exoplayer2.Util;
using MediaManager.Abstractions.Enums;
using MediaManager.Audio;
using MediaManager.Platforms.Android;
using MediaManager.Platforms.Android.Audio;
using MediaManager.Platforms.Android.Utils;

namespace MediaManager
{
    public class AudioPlayer : Java.Lang.Object, IAudioPlayer, IExoPlayerPlayer
    {
        public AudioPlayer()
        {
            StatusTimer.Elapsed += (object sender, ElapsedEventArgs e) => { OnPlaying(); };
            BufferedTimer.Elapsed += (object sender, ElapsedEventArgs e) => { OnBuffering(); };

            StatusTimer.AutoReset = BufferedTimer.AutoReset = true;
        }

        public AudioPlayer(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer)
        {
        }

        public virtual SimpleExoPlayer Player { get; set; }

        protected Context Context { get; set; } = CrossMediaManager.Current.GetContext();

        protected string UserAgent { get; set; }
        protected MediaSessionCompat MediaSession { get; set; }
        protected DefaultHttpDataSourceFactory HttpDataSourceFactory { get; set; }
        protected DefaultDataSourceFactory DataSourceFactory { get; set; }
        protected DefaultBandwidthMeter BandwidthMeter { get; set; }
        protected AdaptiveTrackSelection.Factory TrackSelectionFactory { get; set; }
        protected DefaultTrackSelector TrackSelector { get; set; }
        protected PlaybackController PlaybackController { get; set; }
        protected MediaSessionConnector MediaSessionConnector { get; set; }
        protected QueueNavigator QueueNavigator { get; set; }
        protected ConcatenatingMediaSource MediaSource { get; set; }
        protected QueueDataAdapter QueueDataAdapter { get; set; }
        protected MediaSourceFactory MediaSourceFactory { get; set; }
        protected TimelineQueueEditor TimelineQueueEditor { get; set; }
        protected PlaybackPreparer PlaybackPreparer { get; set; }
        protected PlayerEventListener PlayerEventListener { get; set; }

        //TODO: Remove with Exoplayer 2.9.0
        internal AudioFocusManager AudioFocusManager { get; set; }

        #region scheduled updates
        internal Timer StatusTimer = new Timer(1000), BufferedTimer = new Timer(1000);
        #endregion

        public MediaPlayerState State
        {
            get
            {
                if (!MediaSession.Active)
                    return MediaPlayerState.Stopped;

                return MediaSession.Controller.PlaybackState.ToMediaPlayerState();
            }
        }

        public TimeSpan Position => TimeSpan.FromTicks(Player.CurrentPosition);

        public TimeSpan Duration => TimeSpan.FromTicks(Player.Duration);

        public TimeSpan Buffered => TimeSpan.FromTicks(Player.BufferedPosition);

        public Task Pause()
        {
            Player.Stop();
            return Task.CompletedTask;
        }

        public virtual void Initialize(MediaSessionCompat mediaSession)
        {
            if (Player != null)
                return;

            MediaSession = mediaSession;

            UserAgent = Util.GetUserAgent(Context, "MediaManager");
            HttpDataSourceFactory = new DefaultHttpDataSourceFactory(UserAgent);

            var requestHeaders = CrossMediaManager.Current.RequestHeaders;
            if (requestHeaders?.Count > 0)
            {
                foreach (var item in requestHeaders)
                {
                    HttpDataSourceFactory.DefaultRequestProperties.Set(item.Key, item.Value);
                }
            }

            DataSourceFactory = new DefaultDataSourceFactory(Context, null, HttpDataSourceFactory);
            BandwidthMeter = new DefaultBandwidthMeter();
            TrackSelectionFactory = new AdaptiveTrackSelection.Factory(BandwidthMeter);
            TrackSelector = new DefaultTrackSelector(TrackSelectionFactory);
            MediaSource = new ConcatenatingMediaSource();

            //TODO: Replace with built-in AudioManager in Exoplayer 2.9.0
            AudioFocusManager = new AudioFocusManager(this);

            Player = ExoPlayerFactory.NewSimpleInstance(Context, TrackSelector);
            Player.AudioAttributes.ContentType = (int)AudioContentType.Music;
            Player.AudioAttributes.Usage = (int)AudioUsageKind.Media;

            PlayerEventListener = new PlayerEventListener(this);
            Player.AddListener(PlayerEventListener);

            PlaybackController = new PlaybackController(AudioFocusManager);
            MediaSessionConnector = new MediaSessionConnector(MediaSession, PlaybackController);

            QueueNavigator = new QueueNavigator(MediaSession);
            MediaSessionConnector.SetQueueNavigator(QueueNavigator);

            QueueDataAdapter = new QueueDataAdapter();
            MediaSourceFactory = new MediaSourceFactory(DataSourceFactory);
            TimelineQueueEditor = new TimelineQueueEditor(MediaSession.Controller, MediaSource, QueueDataAdapter, MediaSourceFactory);
            MediaSessionConnector.SetQueueEditor(TimelineQueueEditor);

            PlaybackPreparer = new PlaybackPreparer(Player, DataSourceFactory, MediaSource);
            MediaSessionConnector.SetPlayer(Player, PlaybackPreparer, null);
            Player.Prepare(MediaSource);
        }

        internal void Release()
        {
            Player.Release();
            Player = null;
            BufferedTimer.Stop();
            StatusTimer.Stop();
        }

        public Task Play(string Url)
        {
            return Task.CompletedTask;
        }

        public Task Play()
        {
            Player.PlayWhenReady = true;
            return Task.CompletedTask;
        }

        public Task Seek(TimeSpan position)
        {
            return Task.CompletedTask;
        }

        public Task Stop()
        {
            return Task.CompletedTask;
        }

        private void OnPlaying()
        {
            SimpleExoPlayer simpleExoPlayer = Player as SimpleExoPlayer;

            double progress = Math.Ceiling((double)(simpleExoPlayer.CurrentPosition / simpleExoPlayer.Duration) * 100);
            TimeSpan duration = TimeSpan.FromMilliseconds(simpleExoPlayer.Duration);
            TimeSpan position = TimeSpan.FromMilliseconds(simpleExoPlayer.CurrentPosition);

            CrossMediaManager.Current.OnPlayingChanged(this, new Abstractions.EventArguments.PlayingChangedEventArgs(progress, position, duration));
        }

        private void OnBuffering()
        {
            SimpleExoPlayer simpleExoPlayer = Player as SimpleExoPlayer;

            double progress = simpleExoPlayer.BufferedPercentage;
            TimeSpan bufferedTime = TimeSpan.FromMilliseconds(simpleExoPlayer.BufferedPosition);

            CrossMediaManager.Current.OnBufferingChanged(this, new Abstractions.EventArguments.BufferingChangedEventArgs(progress, bufferedTime));

            if (progress == 100)
                BufferedTimer.Stop();
        }
    }
}
