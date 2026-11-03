// Music Room. Copyright (C) 2026 Marko Idžan. Licensed under GPL-3.0-or-later. Modified November 3, 2026.
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Media;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace MusicRoom;

public sealed class Track
{
    public string Path { get; set; } = "";
    public string Title { get; set; } = "";
    public string Album { get; set; } = "—";
    public string Folder { get; set; } = "";
    public string Artist { get; set; } = "";
    public string DurationLabel { get; set; } = "—";
}

public sealed class Playlist
{
    public string Name { get; set; } = "";
    public List<string> Paths { get; set; } = [];
}

public sealed class LibraryData
{
    public List<string> Tracks { get; set; } = [];
    public List<Playlist> Playlists { get; set; } = [];
}

public partial class MainWindow : Window
{
    private static readonly string[] Formats = [".mp3", ".m4a", ".wav", ".aac", ".wma", ".flac"];
    private static readonly string DataDirectory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicRoom");
    private static readonly string DataFile = System.IO.Path.Combine(DataDirectory, "library.json");
    private readonly MediaElement _media = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Stop, Volume = .7 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly ObservableCollection<Playlist> _playlists = [];
    private List<Track> _tracks = [];
    private List<Track> _queue = [];
    private int _currentIndex = -1;
    private int _displayIndex = 0;
    private bool _isPlaylistView;
    private bool _seeking;
    private bool _shuffle;
    private bool _repeat;
    private string _activePlaylistName = "";

    public MainWindow()
    {
        InitializeComponent();
        if (Content is Grid root) root.Children.Add(_media);
        _media.Visibility = Visibility.Collapsed;
        _media.MediaOpened += (_, _) => { Dispatcher.Invoke(() => { var len = _media.NaturalDuration.HasTimeSpan ? _media.NaturalDuration.TimeSpan : TimeSpan.Zero; ProgressSlider.Maximum = Math.Max(1, len.TotalSeconds); DurationLabel.Text = FormatTime(len); PlayIcon(true); }); };
        _media.MediaEnded += (_, _) => { if (_repeat) PlayTrack(_queue.ElementAtOrDefault(_currentIndex) ?? _tracks.FirstOrDefault()!); else NextTrack(); };
        _media.MediaFailed += (_, e) => { MessageBox.Show(this, $"Windows couldn't play this audio file. Check that its format is supported by the audio codecs installed on this PC.\n\n{e.ErrorException?.Message}", "Can't play this track", MessageBoxButton.OK, MessageBoxImage.Information); PlayIcon(false); };
        _timer.Tick += (_, _) => { if (!_seeking && _media.NaturalDuration.HasTimeSpan) { ProgressSlider.Value = _media.Position.TotalSeconds; ElapsedLabel.Text = FormatTime(_media.Position); } };
        _timer.Start(); _media.Volume = VolumeSlider.Value;
        VolumeSlider.ValueChanged += (_, _) => _media.Volume = VolumeSlider.Value;
        NavList.SelectedIndex = 0;
        LoadLibrary();
        TrackList.ItemsSource = _tracks;
        RefreshDisplay();
    }

    private void LoadLibrary()
    {
        try
        {
            if (!File.Exists(DataFile)) return;
            var data = JsonSerializer.Deserialize<LibraryData>(File.ReadAllText(DataFile));
            if (data is null) return;
            _tracks = data.Tracks.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists).Select(MakeTrack).ToList();
            foreach (var playlist in data.Playlists) { playlist.Paths = playlist.Paths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList(); _playlists.Add(playlist); }
            PlaylistList.ItemsSource = _playlists;
        }
        catch (Exception ex) { MessageBox.Show(this, $"Your saved music library couldn't be loaded.\n\n{ex.Message}", "Micro Music Room", MessageBoxButton.OK, MessageBoxImage.Information); }
    }

    private void SaveLibrary()
    {
        try { Directory.CreateDirectory(DataDirectory); File.WriteAllText(DataFile, JsonSerializer.Serialize(new LibraryData { Tracks = _tracks.Select(t => t.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), Playlists = _playlists.ToList() }, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { MessageBox.Show(this, $"Couldn't save your playlists.\n\n{ex.Message}", "Micro Music Room", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private static Track MakeTrack(string path)
    {
        var file = System.IO.Path.GetFileNameWithoutExtension(path);
        var parts = file.Split(" - ", 2, StringSplitOptions.TrimEntries);
        var artist = parts.Length == 2 ? parts[0] : new DirectoryInfo(System.IO.Path.GetDirectoryName(path)!).Name;
        var title = parts.Length == 2 ? parts[1] : file;
        return new Track { Path = path, Title = title, Artist = artist, Album = "—", Folder = new DirectoryInfo(System.IO.Path.GetDirectoryName(path)!).Name };
    }

    private void ImportFolder(string folder)
    {
        List<string> files;
        try { files = Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Where(p => Formats.Contains(System.IO.Path.GetExtension(p), StringComparer.OrdinalIgnoreCase)).ToList(); }
        catch (Exception ex) { MessageBox.Show(this, $"Couldn't read that folder.\n\n{ex.Message}", "Micro Music Room", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (files.Count == 0) { MessageBox.Show(this, "No supported audio files were found in that folder.", "No music found", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        AddToLibrary(files);
        var name = new DirectoryInfo(folder).Name;
        var playlist = _playlists.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (playlist is null) { playlist = new Playlist { Name = name }; _playlists.Add(playlist); PlaylistList.ItemsSource = null; PlaylistList.ItemsSource = _playlists; }
        foreach (var file in files) if (!playlist.Paths.Contains(file, StringComparer.OrdinalIgnoreCase)) playlist.Paths.Add(file);
        SaveLibrary(); PlaylistList.SelectedItem = playlist; RefreshDisplay();
    }

    private void AddToLibrary(IEnumerable<string> files)
    {
        foreach (var f in files) if (File.Exists(f) && Formats.Contains(System.IO.Path.GetExtension(f), StringComparer.OrdinalIgnoreCase) && !_tracks.Any(t => t.Path.Equals(f, StringComparison.OrdinalIgnoreCase))) _tracks.Add(MakeTrack(f));
        SaveLibrary(); RefreshDisplay();
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Choose a music folder", Multiselect = true };
        if (dialog.ShowDialog(this) == true) foreach (var folder in dialog.FolderNames) ImportFolder(folder);
    }

    private void AddSongs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Add music to your library", Multiselect = true, Filter = "Audio files|*.mp3;*.m4a;*.wav;*.aac;*.wma;*.flac|All files|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        AddToLibrary(dialog.FileNames);
        if (_isPlaylistView && _playlists.FirstOrDefault(p => p.Name == _activePlaylistName) is { } playlist)
        { foreach (var file in dialog.FileNames) if (!playlist.Paths.Contains(file, StringComparer.OrdinalIgnoreCase)) playlist.Paths.Add(file); SaveLibrary(); RefreshDisplay(); }
    }

    private void CreatePlaylist_Click(object sender, RoutedEventArgs e)
    {
        var name = Prompt("Create a playlist", "Playlist name", "My mix");
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_playlists.Any(p => p.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))) { MessageBox.Show(this, "A playlist with that name already exists.", "Micro Music Room"); return; }
        var playlist = new Playlist { Name = name.Trim() }; _playlists.Add(playlist); PlaylistList.ItemsSource = null; PlaylistList.ItemsSource = _playlists; SaveLibrary(); PlaylistList.SelectedItem = playlist;
    }

    private static string? Prompt(string title, string label, string initial)
    {
        var dialog = new Window { Title = title, Width = 375, Height = 175, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)), Foreground = Brushes.White, FontFamily = new FontFamily("Segoe UI") };
        var panel = new StackPanel { Margin = new Thickness(22) }; panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 10) }); var input = new TextBox { Text = initial }; panel.Children.Add(input); var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) }; var ok = new Button { Content = "Create", IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0), Background = new SolidColorBrush(Color.FromRgb(121, 103, 232)) }; var cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 75 }; buttons.Children.Add(ok); buttons.Children.Add(cancel); panel.Children.Add(buttons); dialog.Content = panel; ok.Click += (_, _) => dialog.DialogResult = true; input.SelectAll(); input.Focus(); return dialog.ShowDialog() == true ? input.Text.Trim() : null;
    }

    private void RenamePlaylist_Click(object sender, RoutedEventArgs e)
    {
        var p = _playlists.FirstOrDefault(p => p.Name == _activePlaylistName); if (p is null) return;
        var name = Prompt("Rename playlist", "Playlist name", p.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_playlists.Any(x => x != p && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { MessageBox.Show(this, "A playlist with that name already exists.", "Micro Music Room"); return; }
        p.Name = name; var selected = p; PlaylistList.ItemsSource = null; PlaylistList.ItemsSource = _playlists; PlaylistList.SelectedItem = selected; _activePlaylistName = p.Name; SaveLibrary(); RefreshDisplay();
    }

    private void DeletePlaylist_Click(object sender, RoutedEventArgs e)
    {
        var p = _playlists.FirstOrDefault(x => x.Name == _activePlaylistName); if (p is null) return;
        if (MessageBox.Show(this, $"Delete the playlist \"{p.Name}\"? Your original music files won't be affected.", "Delete playlist", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _playlists.Remove(p); SaveLibrary(); PlaylistList.SelectedItem = null; NavList.SelectedIndex = 0; ShowLibrary("All songs");
    }

    private void AddSelectedToPlaylist_Click(object sender, RoutedEventArgs e)
    {
        var selected = TrackList.SelectedItems.Cast<Track>().Select(t => t.Path).ToList();
        var options = _playlists.ToList();
        var menu = new ContextMenu();
        if (selected.Count == 0) { menu.Items.Add(new MenuItem { Header = "Select songs to add first", IsEnabled = false }); }
        else if (options.Count == 0) { menu.Items.Add(new MenuItem { Header = "Create a playlist first", IsEnabled = false }); }
        else foreach (var p in options) { var item = new MenuItem { Header = p.Name }; item.Click += (_, _) => { foreach (var file in selected) if (!p.Paths.Contains(file, StringComparer.OrdinalIgnoreCase)) p.Paths.Add(file); SaveLibrary(); if (_isPlaylistView && p.Name == _activePlaylistName) RefreshDisplay(); }; menu.Items.Add(item); }
        menu.PlacementTarget = AddToPlaylistButton; menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom; menu.IsOpen = true;
    }

    private void ContextPlay_Click(object sender, RoutedEventArgs e) { if (TrackList.SelectedItem is Track t) PlayTrack(t); }
    private void ContextAdd_Click(object sender, RoutedEventArgs e) { AddSelectedToPlaylist_Click(sender, e); }
    private void PlayAll_Click(object sender, RoutedEventArgs e) { if (TrackList.Items.Count > 0) PlayTrack((Track)TrackList.Items[0]); }
    private void TrackList_MouseDoubleClick(object sender, MouseButtonEventArgs e) { if (TrackList.SelectedItem is Track t) PlayTrack(t); }

    private void PlayTrack(Track track)
    {
        var view = TrackList.Items.Cast<Track>().ToList();
        _queue = _shuffle ? view.OrderBy(_ => Guid.NewGuid()).ToList() : view;
        _currentIndex = _queue.FindIndex(t => t.Path.Equals(track.Path, StringComparison.OrdinalIgnoreCase));
        if (_currentIndex < 0) { _queue.Insert(0, track); _currentIndex = 0; }
        StartCurrentTrack();
    }

    private void StartCurrentTrack()
    {
        if (_currentIndex < 0 || _currentIndex >= _queue.Count) return;
        var t = _queue[_currentIndex];
        try { _media.Stop(); _media.Source = new Uri(t.Path); _media.Position = TimeSpan.Zero; NowTitle.Text = t.Title; NowArtist.Text = t.Artist; ElapsedLabel.Text = "0:00"; DurationLabel.Text = t.DurationLabel; ProgressSlider.Value = 0; _media.Play(); PlayIcon(true); }
        catch (Exception ex) { MessageBox.Show(this, $"Couldn't open this track.\n\n{ex.Message}", "Micro Music Room", MessageBoxButton.OK, MessageBoxImage.Information); }
    }

    private void NextTrack()
    {
        if (_queue.Count == 0) return;
        _currentIndex++;
        if (_currentIndex >= _queue.Count) { if (!_repeat) { _currentIndex = 0; PlayIcon(false); return; } _currentIndex = 0; }
        StartCurrentTrack();
    }
    private void Previous_Click(object sender, RoutedEventArgs e) { if (_media.Position.TotalSeconds > 3) { _media.Position = TimeSpan.Zero; return; } if (_queue.Count == 0) return; _currentIndex = (_currentIndex - 1 + _queue.Count) % _queue.Count; StartCurrentTrack(); }
    private void Next_Click(object sender, RoutedEventArgs e) => NextTrack();
    private void TogglePlay_Click(object sender, RoutedEventArgs e) { if (_media.Source is null) { PlayAll_Click(sender, e); return; } if (_media.CanPause && _media.Position < _media.NaturalDuration.TimeSpan) { _media.Pause(); PlayIcon(false); } else { _media.Play(); PlayIcon(true); } }
    private void PlayIcon(bool playing) { if (FindButtonWithText("▶") is { } b) b.Content = playing ? "Ⅱ" : "▶"; }
    private Button? FindButtonWithText(string text) => FindVisualChildren<Button>(this).FirstOrDefault(b => b.Content?.ToString() == text || b.Content?.ToString() == (text == "▶" ? "Ⅱ" : ""));
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject { for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var child = VisualTreeHelper.GetChild(root, i); if (child is T match) yield return match; foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant; } }
    private void Shuffle_Click(object sender, RoutedEventArgs e) { _shuffle = !_shuffle; ShuffleButton.Foreground = _shuffle ? Brushes.White : (Brush)FindResource("Muted"); }
    private void Repeat_Click(object sender, RoutedEventArgs e) { _repeat = !_repeat; RepeatButton.Foreground = _repeat ? Brushes.White : (Brush)FindResource("Muted"); }
    private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_seeking && _media.NaturalDuration.HasTimeSpan) _media.Position = TimeSpan.FromSeconds(ProgressSlider.Value); }
    private void ProgressSlider_DragStart(object sender, MouseButtonEventArgs e) => _seeking = true;
    private void ProgressSlider_DragEnd(object sender, MouseButtonEventArgs e) { if (_media.NaturalDuration.HasTimeSpan) _media.Position = TimeSpan.FromSeconds(ProgressSlider.Value); _seeking = false; }
    private static string FormatTime(TimeSpan time) => $"{(int)time.TotalMinutes}:{time.Seconds:00}";

    private void NavList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (!IsInitialized || NavList.SelectedItem is not ListBoxItem item) return; PlaylistList.SelectedItem = null; ShowLibrary(item.Tag?.ToString() ?? "All songs"); }
    private void PlaylistList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    { if (PlaylistList.SelectedItem is Playlist p) { NavList.SelectedItem = null; _isPlaylistView = true; _activePlaylistName = p.Name; Breadcrumb.Text = "YOUR LIBRARY  /  PLAYLIST"; PageTitle.Text = p.Name; RenameButton.Visibility = DeleteButton.Visibility = Visibility.Visible; RefreshDisplay(); } }

    private void ShowLibrary(string name)
    {
        _isPlaylistView = false; _activePlaylistName = ""; _displayIndex = name == "Recently added" ? 1 : 0; Breadcrumb.Text = $"YOUR LIBRARY  /  {name.ToUpperInvariant()}"; PageTitle.Text = name; RenameButton.Visibility = DeleteButton.Visibility = Visibility.Collapsed; RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        if (TrackList is null) return;
        var source = _tracks.AsEnumerable();
        if (_isPlaylistView) { var p = _playlists.FirstOrDefault(p => p.Name == _activePlaylistName); var paths = p?.Paths ?? []; source = paths.Select(path => _tracks.FirstOrDefault(t => t.Path.Equals(path, StringComparison.OrdinalIgnoreCase))).Where(t => t is not null).Cast<Track>(); }
        else if (_displayIndex == 1) source = source.OrderByDescending(t => File.Exists(t.Path) ? File.GetLastWriteTimeUtc(t.Path) : DateTime.MinValue);
        var query = SearchBox?.Text?.Trim(); if (!string.IsNullOrEmpty(query)) source = source.Where(t => t.Title.Contains(query, StringComparison.OrdinalIgnoreCase) || t.Artist.Contains(query, StringComparison.OrdinalIgnoreCase) || t.Folder.Contains(query, StringComparison.OrdinalIgnoreCase));
        var tracks = source.ToList(); TrackList.ItemsSource = tracks; TrackCount.Text = tracks.Count == 1 ? "1 song" : $"{tracks.Count} songs"; EmptyState.Visibility = tracks.Count == 0 ? Visibility.Visible : Visibility.Collapsed; TrackList.Visibility = tracks.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        AddToPlaylistButton.Visibility = Visibility.Visible;
    }
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) { if (IsInitialized) RefreshDisplay(); }

    private void AboutLicense_Click(object sender, RoutedEventArgs e)
    {
        const string notice = "Micro Music Room\nCopyright (C) 2026 Marko Idžan\n\nThis program comes with ABSOLUTELY NO WARRANTY. This is free software, and you are welcome to redistribute it under the terms of the GNU General Public License, version 3 or (at your option) any later version.\n\nSelect ‘View GNU GPL’ to read the complete license.";
        var dialog = new Window { Title = "About Micro Music Room", Width = 510, Height = 280, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)), Foreground = Brushes.White, FontFamily = new FontFamily("Segoe UI"), Owner = this };
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = notice, TextWrapping = TextWrapping.Wrap, LineHeight = 23 });
        var iconCredit = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };
        var iconLink = new System.Windows.Documents.Hyperlink(new System.Windows.Documents.Run("DJ icon by Icons8")) { NavigateUri = new Uri("https://icons8.com") };
        iconLink.RequestNavigate += (_, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        iconCredit.Inlines.Add(iconLink); panel.Children.Add(iconCredit);
        var viewLicense = new Button { Content = "View GNU GPL", MinWidth = 120, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        panel.Children.Add(viewLicense); dialog.Content = panel;
        viewLicense.Click += (_, _) =>
        {
            var licensePath = System.IO.Path.Combine(AppContext.BaseDirectory, "LICENSE");
            if (!File.Exists(licensePath)) { MessageBox.Show(dialog, "The GPL license file could not be found beside the app.", "License", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var licenseWindow = new Window { Title = "GNU General Public License, version 3", Width = 760, Height = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)), Foreground = Brushes.White, Owner = dialog };
            licenseWindow.Content = new TextBox { Text = File.ReadAllText(licensePath), IsReadOnly = true, TextWrapping = TextWrapping.NoWrap, AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), FontSize = 12, Margin = new Thickness(10) };
            licenseWindow.ShowDialog();
        };
        dialog.ShowDialog();
    }
}

