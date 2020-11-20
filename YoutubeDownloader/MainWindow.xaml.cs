using Microsoft.WindowsAPICodePack.Dialogs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YoutubeDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        System.ComponentModel.BackgroundWorker addchannelworker = new System.ComponentModel.BackgroundWorker();
        System.ComponentModel.BackgroundWorker addplaylistworker = new System.ComponentModel.BackgroundWorker();
        System.ComponentModel.BackgroundWorker addvideoworker = new System.ComponentModel.BackgroundWorker();
        System.ComponentModel.BackgroundWorker downloadworker = new System.ComponentModel.BackgroundWorker();
        System.ComponentModel.BackgroundWorker clearworker = new System.ComponentModel.BackgroundWorker();
        ObservableCollection<videoitem> videoitems = new ObservableCollection<videoitem>();
        string mainpath = string.Empty;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null).FileExists("youtubedownloader.dat"))
                using (IsolatedStorageFileStream isoStream = new IsolatedStorageFileStream("youtubedownloader.dat", FileMode.Open, IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null)))
                using (StreamReader reader = new StreamReader(isoStream))
                {
                    var x = JsonConvert.DeserializeObject<settings>(reader.ReadToEnd());
                    audio.IsChecked = x.audio;
                    video.IsChecked = x.video;
                    zip.IsChecked = x.zip;
                    delete.IsChecked = x.delete;
                    open.IsChecked = x.open;
                    mainpath = x.path;
                }
            addchannelworker.DoWork += Addchannelworker_DoWork;
            addplaylistworker.DoWork += Addplaylistworker_DoWork;
            addvideoworker.DoWork += Addvideoworker_DoWork;
            clearworker.DoWork += Clearworker_DoWork;
            addchannelworker.RunWorkerCompleted += Addchannelworker_RunWorkerCompleted;
            addplaylistworker.RunWorkerCompleted += Addchannelworker_RunWorkerCompleted;
            addvideoworker.RunWorkerCompleted += Addchannelworker_RunWorkerCompleted;
            downloadworker.DoWork += Downloadworker_DoWork;
        }

        private void Clearworker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            videoitems = new ObservableCollection<videoitem>();
            Application.Current.Dispatcher.Invoke(() =>
            {
                data.ItemsSource = null;
            });
        }

        private void Addchannelworker_RunWorkerCompleted(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.Default;
            download.IsEnabled = true;
            go.IsEnabled = true;
            clear.IsEnabled = true;
        }

        private async void Downloadworker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                bool video = bool.Parse(((string)e.Argument).Split(':')[0]);
                bool audio = bool.Parse(((string)e.Argument).Split(':')[1]);
                bool zip = bool.Parse(((string)e.Argument).Split(':')[2]);
                bool delete = bool.Parse(((string)e.Argument).Split(':')[3]);
                bool open = bool.Parse(((string)e.Argument).Split(':')[4]);
                List<string> urlstodelete = new List<string>();
                string startpath = string.Empty;
                var dd = Directory.CreateDirectory(Path.Combine(mainpath, Regex.Replace(videoitems[0].channel, @"[^A-Za-z0-9 ]", "").Trim()));
                int count = 0;
                foreach (var x in videoitems)
                {
                    try
                    {
                        var youtube = new YoutubeClient();
                        var streamManifest = await youtube.Videos.Streams.GetManifestAsync(x.id);
                        IStreamInfo streamInfo = null;
                        if (audio && video)
                            streamInfo = streamManifest.GetMuxed().WithHighestVideoQuality();
                        else if (audio)
                            streamInfo = streamManifest.GetAudioOnly().WithHighestBitrate();
                        else if (video)
                            streamInfo = streamManifest.GetVideoOnly().Where(s => s.Container == Container.Mp4).WithHighestVideoQuality();
                        if (streamInfo != null)
                        {
                            var stream = await youtube.Videos.Streams.GetAsync(streamInfo);
                            string nametouse = x.title;
                            if (!string.IsNullOrWhiteSpace(x.playlist))
                                nametouse = x.playlist;
                            startpath = dd.FullName;
                            var d = Directory.CreateDirectory(Path.Combine(mainpath, dd.FullName, Regex.Replace(nametouse, @"[^A-Za-z0-9 ]", "").Trim()));
                            await youtube.Videos.Streams.DownloadAsync(streamInfo, Path.Combine(d.FullName, $"{Regex.Replace(nametouse, @"[^A-Za-z0-9 ]", "").Trim()}.{streamInfo.Container}"));
                            if (zip)
                            {
                                CompressFile(Path.Combine(d.FullName, $"{Regex.Replace(nametouse, @"[^A-Za-z0-9 ]", "").Trim()}.{streamInfo.Container}"));
                                File.Move($"{Path.Combine(d.FullName, $"{Regex.Replace(nametouse, @"[^A-Za-z0-9 ]", "").Trim()}.{streamInfo.Container}")}.gz", Path.Combine(d.FullName, "..", $"{Regex.Replace(nametouse, @"[^A-Za-z0-9 ]", "").Trim()}.{streamInfo.Container}.gz"));
                                File.Delete(Path.Combine(d.FullName, $"{Regex.Replace(nametouse, @"[^A-Za-z0-9 ]", "").Trim()}.{streamInfo.Container}"));
                                d.Delete(true);
                            }
                        }
                        if (open)
                            Process.Start(startpath);
                        urlstodelete.Add(x.url);
                        count++;
                    }
                    catch
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            link.Text = "ERROR IN ONE OR MORE VIDEOS";
                        });
                    }
                }
                if (delete)
                    foreach (var x in urlstodelete)
                        Application.Current.Dispatcher.Invoke(() =>
                            {
                                videoitems.Remove(videoitems.Where(a => a.url.Equals(x)).FirstOrDefault());
                            });
                urlstodelete = null;
                Application.Current.Dispatcher.Invoke(() =>
                    {
                        data.ItemsSource = new ObservableCollection<videoitem>(videoitems);
                        stopwatch.Stop();
                        speed.Content = $"Downloaded {count} video(s) in {stopwatch.Elapsed.TotalSeconds} secs";
                        download.IsEnabled = true;
                        go.IsEnabled = true;
                        clear.IsEnabled = true;
                    });
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(() =>
      {
          link.Text = "ERROR";
          download.IsEnabled = true;
          go.IsEnabled = true;
          clear.IsEnabled = true;
      });
            }
        }

        private async void Addvideoworker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var youtube = new YoutubeClient();
                var video = await youtube.Videos.GetAsync((string)e.Argument);
                if (videoitems.Where(a => a.url.Equals(video.Url)).Count() == 0)
                    videoitems.Add(new videoitem()
                    {
                        thumbnailpic = ImageSourceFromBitmap(UrlToBitmap(video.Thumbnails.LowResUrl)),
                        thumbnail = video.Thumbnails.LowResUrl,
                        title = video.Title,
                        date = video.UploadDate.ToString(),
                        length = video.Duration.ToString(@"mm\:ss"),
                        url = video.Url,
                        id = video.Id,
                        channel = video.Author,
                        download = true
                    });
            Application.Current.Dispatcher.Invoke(() =>
                {
                    data.ItemsSource = new ObservableCollection<videoitem>(videoitems);
                    stopwatch.Stop();
                    speed.Content = $"Fetched 1 video in {stopwatch.Elapsed.TotalSeconds} secs";
                });
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    link.Text = "ERROR";
                    download.IsEnabled = true;
                    go.IsEnabled = true;
                    clear.IsEnabled = true;
                });
            }
        }

        private async void Addplaylistworker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var youtube = new YoutubeClient();
                var videos = await youtube.Playlists.GetVideosAsync($"https://youtube.com/channel/{getChannelID((string)e.Argument)}");
                int added = 0;
                foreach (var video in videos)
                {
                    try
                    {
                        if (videoitems.Where(a => a.url.Equals(video.Url)).Count() == 0)
                            videoitems.Add(new videoitem()
                            {
                                thumbnailpic = ImageSourceFromBitmap(UrlToBitmap(video.Thumbnails.LowResUrl)),
                                thumbnail = video.Thumbnails.LowResUrl,
                                title = video.Title,
                                date = video.UploadDate.ToString(),
                                length = video.Duration.ToString(@"mm\:ss"),
                                url = video.Url,
                                id = video.Id,
                                channel = video.Author,
                                playlist = (await youtube.Playlists.GetAsync($"https://youtube.com/channel/{getChannelID((string)e.Argument)}")).Title,
                                download = true
                            });
                        added++;
                    } catch
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            link.Text = "ERROR IN ONE OR MORE VIDEOS";
                        });
                    }
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    data.ItemsSource = videoitems;
                    stopwatch.Stop();
                    speed.Content = $"Fetched {added} videos in {stopwatch.Elapsed.TotalSeconds} secs";
                });
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    link.Text = "ERROR";
                    download.IsEnabled = true;
                    go.IsEnabled = true;
                    clear.IsEnabled = true;
                });
            }
        }

        private async void Addchannelworker_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var youtube = new YoutubeClient();
                var videos = await youtube.Channels.GetUploadsAsync($"https://youtube.com/channel/{getChannelID((string)e.Argument)}");
                int added = 0;
                foreach (var video in videos)
                {
                    try
                    {
                        if (videoitems.Where(a => a.url.Equals(video.Url)).Count() == 0)
                            videoitems.Add(new videoitem()
                            {
                                thumbnailpic = ImageSourceFromBitmap(UrlToBitmap(video.Thumbnails.LowResUrl)),
                                thumbnail = video.Thumbnails.LowResUrl,
                                title = video.Title,
                                date = video.UploadDate.ToString(),
                                length = video.Duration.ToString(@"mm\:ss"),
                                url = video.Url,
                                id = video.Id,
                                channel = video.Author,
                                download = true
                            });
                        added++;
                    } catch
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            link.Text = "ERROR IN ONE OR MORE VIDEOS";
                        });
                    }
                }
                Application.Current.Dispatcher.Invoke(() =>
                    {
                        data.ItemsSource = videoitems;
                        stopwatch.Stop();
                        speed.Content = $"Fetched {added} videos in {stopwatch.Elapsed.TotalSeconds} secs";
                    });
            }
            catch
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    link.Text = "ERROR";
                    download.IsEnabled = true;
                    go.IsEnabled = true;
                    clear.IsEnabled = true;
                });
            }
        }

        public string getChannelID(string url)
        {
            string x = new WebClient().DownloadString(url);
            return x.Substring("<link rel=\"canonical\" href=\"https://www.youtube.com/channel/", "\">");
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.Cursor.Current = System.Windows.Forms.Cursors.WaitCursor;
            download.IsEnabled = false;
            go.IsEnabled = false;
            clear.IsEnabled = false;
            if (state.SelectedValue.ToString().Equals("Video"))
            {
                addvideoworker.RunWorkerAsync(link.Text);
            }
            else if (state.SelectedValue.ToString().Equals("Playlist"))
            {
                addplaylistworker.RunWorkerAsync(link.Text);
            }
            else if (state.SelectedValue.ToString().Equals("Channel"))
            {
                addchannelworker.RunWorkerAsync(link.Text);
            }
        }

        void DataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

        public Bitmap UrlToBitmap(string url)
        {
            System.Net.WebRequest request =
       System.Net.WebRequest.Create(
       url);
            System.Net.WebResponse response = request.GetResponse();
            System.IO.Stream responseStream =
                response.GetResponseStream();
            return new Bitmap(responseStream);
        }

        //https://stackoverflow.com/a/35274172/14147191
        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);
        public ImageSource ImageSourceFromBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                var x = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                x.Freeze();
                return x;
            }
            finally { DeleteObject(handle); }
        }

        //https://stackoverflow.com/a/11153588/14147191
        public static void CompressFile(string path)
        {
            FileStream sourceFile = File.OpenRead(path);
            FileStream destinationFile = File.Create(path + ".gz");
            byte[] buffer = new byte[sourceFile.Length];
            sourceFile.Read(buffer, 0, buffer.Length);
            using (GZipStream output = new GZipStream(destinationFile,
                CompressionMode.Compress))
                output.Write(buffer, 0, buffer.Length);
            sourceFile.Close();
            destinationFile.Close();
        }

        private void clear_Click(object sender, RoutedEventArgs e)
        {
            clearworker.RunWorkerAsync();
        }

        private void clear_Copy_Click(object sender, RoutedEventArgs e)
        {
            if (addchannelworker.IsBusy)
                addchannelworker.CancelAsync();
            if (addplaylistworker.IsBusy)
                addplaylistworker.CancelAsync();
            if (addvideoworker.IsBusy)
                addvideoworker.CancelAsync();
            if (downloadworker.IsBusy)
                downloadworker.CancelAsync();
            download.IsEnabled = true;
            go.IsEnabled = true;
            clear.IsEnabled = true;
        }

        private void download_Click(object sender, RoutedEventArgs e)
        {
            download.IsEnabled = false;
            go.IsEnabled = false;
            clear.IsEnabled = false;
            downloadworker.RunWorkerAsync($"{video.IsChecked.Value}:{audio.IsChecked.Value}:{zip.IsChecked.Value}:{delete.IsChecked.Value}:{open.IsChecked.Value}");
        }

        private void dir_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                mainpath = dialog.FileName;
        }

        private void OnChecked(object sender, RoutedEventArgs e)
        {
            try
            {
                videoitems[data.SelectedIndex].download = true;
            }
            catch { }
        }

        private void OnUnchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                videoitems[data.SelectedIndex].download = false;
            }
            catch { }
        }

        private void save_Click(object sender, RoutedEventArgs e)
        {
            using (IsolatedStorageFileStream isoStream = new IsolatedStorageFileStream("youtubedownloader.dat", FileMode.CreateNew, IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null)))
            using (StreamWriter writer = new StreamWriter(isoStream))
                writer.WriteLine(JsonConvert.SerializeObject(new settings
                {
                    audio = audio.IsChecked.Value,
                    video = video.IsChecked.Value,
                    zip = zip.IsChecked.Value,
                    delete = delete.IsChecked.Value,
                    open = open.IsChecked.Value,
                    path = mainpath
                }));
        }

        private void export_Click(object sender, RoutedEventArgs e)
        {
            string name = $"export.{DateTime.Now.DayOfWeek}.{DateTime.Now.Day}.json";
            File.WriteAllText(name, JsonConvert.SerializeObject(videoitems.ToList(), Formatting.Indented));
            string[] lines = File.ReadAllLines(name).Where(a => !a.Contains("System.Windows.Interop.InteropBitmap")).ToArray();
            File.WriteAllLines(name, lines);
            Process.Start(name);
        }

        private void import_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                openFileDialog.Filter = "All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var fileStream = openFileDialog.OpenFile();
                    using (StreamReader reader = new StreamReader(fileStream))
                    {
                        var x = JsonConvert.DeserializeObject<List<videoitem>>(reader.ReadToEnd());
                        videoitems = new ObservableCollection<videoitem>(x);
                        data.ItemsSource = (new ObservableCollection<videoitem>(x));
                    }
                }
            }
        }
    }

    //https://stackoverflow.com/a/17253735/14147191
    public static class StringExtensions
    {
        /// <summary>
        /// takes a substring between two anchor strings (or the end of the string if that anchor is null)
        /// </summary>
        /// <param name="this">a string</param>
        /// <param name="from">an optional string to search after</param>
        /// <param name="until">an optional string to search before</param>
        /// <param name="comparison">an optional comparison for the search</param>
        /// <returns>a substring based on the search</returns>
        public static string Substring(this string @this, string from = null, string until = null, StringComparison comparison = StringComparison.InvariantCulture)
        {
            var fromLength = (from ?? string.Empty).Length;
            var startIndex = !string.IsNullOrEmpty(from)
                ? @this.IndexOf(from, comparison) + fromLength
                : 0;

            if (startIndex < fromLength) { throw new ArgumentException("from: Failed to find an instance of the first anchor"); }

            var endIndex = !string.IsNullOrEmpty(until)
            ? @this.IndexOf(until, startIndex, comparison)
            : @this.Length;

            if (endIndex < 0) { throw new ArgumentException("until: Failed to find an instance of the last anchor"); }

            var subString = @this.Substring(startIndex, endIndex - startIndex);
            return subString;
        }
    }

    public class videoitem
    {
        public bool download { get; set; }
        public string thumbnail { get; set; }
        public ImageSource thumbnailpic { get; set; }
        public string title { get; set; }
        public string channel { get; set; }
        public string playlist { get; set; }
        public string date { get; set; }
        public string length { get; set; }
        public string url { get; set; }
        public string id { get; set; }
    }

    public class settings
    {
        public bool audio { get; set; }
        public bool video { get; set; }
        public bool zip { get; set; }
        public bool delete { get; set; }
        public bool open { get; set; }
        public string path { get; set; }
    }
}
