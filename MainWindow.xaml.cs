using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FilesToClipboard
{
    public partial class MainWindow : Window
    {
        private Point _dragStart;

        public ObservableCollection<CollectorTab> Tabs { get; } = new ObservableCollection<CollectorTab>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            AddNewTab();
            UpdateTabUnderlineBrushes();
        }

        private CollectorTab GetCurrentTab()
        {
            return tabControl.SelectedItem as CollectorTab;
        }

        private void AddNewTab()
        {
            var tab = new CollectorTab();
            Tabs.Add(tab);
            tabControl.SelectedItem = tab;
            UpdateTabUnderlineBrushes();
        }

        private void UpdateTabUnderlineBrushes()
        {
            if (Tabs.Count == 0)
                return;

            var accentBrush = TryFindResource("Brush.Accent") as SolidColorBrush
                              ?? new SolidColorBrush(Color.FromRgb(0x5B, 0x9B, 0xFF));

            var accentColor = accentBrush.Color;

            var ordered = Tabs.OrderBy(t => t.CreatedAt).ToList();
            var oldest = ordered.First();
            var newest = ordered.Last();

            if (oldest == newest)
            {
                foreach (var t in Tabs)
                    t.UnderlineBrush = new SolidColorBrush(accentColor);
                return;
            }

            long minTicks = oldest.CreatedAt.Ticks;
            long maxTicks = newest.CreatedAt.Ticks;
            double range = maxTicks - minTicks;
            if (range <= 0)
                range = 1;

            foreach (var t in Tabs)
            {
                double factor = (t.CreatedAt.Ticks - minTicks) / range; // 0..1
                if (t == newest)
                    factor = 1.0;
                else if (t == oldest)
                    factor = 0.0;

                byte a = (byte)(accentColor.A * factor);
                var c = Color.FromArgb(a, accentColor.R, accentColor.G, accentColor.B);
                t.UnderlineBrush = new SolidColorBrush(c);
            }
        }

        private void AddFilesFromDirectoryRecursively(
            ObservableCollection<string> target,
            string directory,
            int? insertIndex = null,
            bool reinsertExistingAtIndex = false)
        {
            if (!Directory.Exists(directory)) return;

            var files = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);

            if (insertIndex == null)
            {
                foreach (var file in files)
                {
                    if (!target.Contains(file))
                        target.Add(file);
                }
            }
            else
            {
                int idx = insertIndex.Value;

                foreach (var file in files)
                {
                    if (reinsertExistingAtIndex)
                    {
                        int existingIndex;
                        while ((existingIndex = target.IndexOf(file)) >= 0)
                        {
                            if (existingIndex < idx)
                                idx--;
                            target.RemoveAt(existingIndex);
                        }
                    }
                    else
                    {
                        if (target.Contains(file))
                            continue;
                    }

                    target.Insert(idx++, file);
                }
            }
        }

        private void btnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var tab = (sender as FrameworkElement)?.DataContext as CollectorTab;
            if (tab == null) return;

            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true
            };
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var filePath in openFileDialog.FileNames)
                {
                    if (!tab.SelectedPaths.Contains(filePath))
                        tab.SelectedPaths.Add(filePath);
                }
            }
        }

        private void btnSelectDirs_Click(object sender, RoutedEventArgs e)
        {
            var tab = (sender as FrameworkElement)?.DataContext as CollectorTab;
            if (tab == null) return;

            var dialog = new VistaFolderBrowserDialog
            {
                Multiselect = true,
                UseDescriptionForTitle = true,
                Description = "Select one or more directories"
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (string dir in dialog.SelectedPaths)
                {
                    AddFilesFromDirectoryRecursively(tab.SelectedPaths, dir);
                }
            }
        }

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            var tab = (sender as FrameworkElement)?.DataContext as CollectorTab;
            if (tab == null) return;

            if (tab.SelectedPaths.Count == 0) return;

            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(tab.PrefixText))
                sb.AppendLine(tab.PrefixText);

            foreach (var filePath in tab.SelectedPaths)
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    sb.AppendLine($"\"{Path.GetFileName(filePath)}\":");
                    sb.AppendLine(content);
                    sb.AppendLine();
                }
            }

            if (!string.IsNullOrWhiteSpace(tab.SuffixText))
                sb.AppendLine(tab.SuffixText);

            Clipboard.SetText(sb.ToString());
            tab.IsResolved = true;
        }

        private void btnUnresolve_Click(object sender, RoutedEventArgs e)
        {
            var tab = (sender as FrameworkElement)?.DataContext as CollectorTab;
            if (tab == null) return;

            tab.IsResolved = false;
        }

        private void btnRemovePath_Click(object sender, RoutedEventArgs e)
        {
            var button = (FrameworkElement)sender;
            var listBox = FindParent<ListBox>(button);
            var tab = listBox?.DataContext as CollectorTab;
            if (tab == null) return;

            var pathToRemove = button.DataContext as string;
            if (!string.IsNullOrEmpty(pathToRemove))
                tab.SelectedPaths.Remove(pathToRemove);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            var tab = (sender as FrameworkElement)?.DataContext as CollectorTab;
            if (tab == null) return;

            tab.SelectedPaths.Clear();
        }

        private void btnClearTexts_Click(object sender, RoutedEventArgs e)
        {
            var tab = (sender as FrameworkElement)?.DataContext as CollectorTab;
            if (tab == null) return;

            tab.PrefixText = string.Empty;
            tab.SuffixText = string.Empty;
        }

        private void btnPastePath_Click(object sender, RoutedEventArgs e)
        {
            var tab = (sender as FrameworkElement)?.DataContext as CollectorTab;
            if (tab == null) return;

            string clipboardText = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                MessageBox.Show("Clipboard is empty or does not contain text.",
                                "No Path Found",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            string[] lines = clipboardText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            bool hasValidPath = false;

            foreach (string line in lines)
            {
                string path = line.Trim().Trim('"').Trim('\'');
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (File.Exists(path))
                {
                    if (!tab.SelectedPaths.Contains(path))
                        tab.SelectedPaths.Add(path);
                    hasValidPath = true;
                }
                else if (Directory.Exists(path))
                {
                    AddFilesFromDirectoryRecursively(tab.SelectedPaths, path);
                    hasValidPath = true;
                }
            }

            if (!hasValidPath)
            {
                MessageBox.Show("Clipboard does not contain a valid file or directory path.",
                                "Invalid Path",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var tab = GetCurrentTab();
            if (tab == null) return;

            string[] droppedPaths = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var path in droppedPaths)
            {
                if (File.Exists(path))
                {
                    if (!tab.SelectedPaths.Contains(path))
                        tab.SelectedPaths.Add(path);
                }
                else if (Directory.Exists(path))
                {
                    AddFilesFromDirectoryRecursively(tab.SelectedPaths, path);
                }
            }

            e.Handled = true;
        }

        private void lstSelectedPaths_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStart = e.GetPosition(null);
        }

        private void lstSelectedPaths_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;

            var pos = e.GetPosition(null);
            if (Math.Abs(pos.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            var lb = (ListBox)sender;
            var files = lb.SelectedItems.Cast<string>()
                           .Where(p => File.Exists(p))
                           .ToArray();
            if (files.Length == 0) return;

            var sc = new StringCollection();
            sc.AddRange(files);

            var data = new DataObject();
            data.SetFileDropList(sc);
            data.SetText(string.Join(Environment.NewLine, files));

            DragDrop.DoDragDrop(lb, data, DragDropEffects.Copy | DragDropEffects.Move);
        }

        private void lstSelectedPaths_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Move;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }

        private void lstSelectedPaths_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var listBox = (ListBox)sender;
            var tab = listBox.DataContext as CollectorTab;
            if (tab == null) return;

            var point = e.GetPosition(listBox);
            int insertIndex = tab.SelectedPaths.Count;

            for (int i = 0; i < listBox.Items.Count; i++)
            {
                var item = listBox.ItemContainerGenerator.ContainerFromIndex(i) as FrameworkElement;
                if (item == null) continue;

                var itemPos = item.TransformToAncestor(listBox).Transform(new Point(0, 0));
                var itemRect = new Rect(itemPos, item.RenderSize);

                if (point.Y < itemRect.Top + itemRect.Height / 2)
                {
                    insertIndex = i;
                    break;
                }
            }

            var droppedPaths = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var path in droppedPaths)
            {
                if (File.Exists(path))
                {
                    int existingIndex;
                    while ((existingIndex = tab.SelectedPaths.IndexOf(path)) >= 0)
                    {
                        if (existingIndex < insertIndex)
                            insertIndex--;
                        tab.SelectedPaths.RemoveAt(existingIndex);
                    }

                    tab.SelectedPaths.Insert(insertIndex++, path);
                }
                else if (Directory.Exists(path))
                {
                    AddFilesFromDirectoryRecursively(tab.SelectedPaths, path, insertIndex, reinsertExistingAtIndex: true);
                    int addedCount = Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Count();
                    insertIndex += addedCount;
                }
            }

            e.Handled = true;
        }

        private void btnInsertName_Click(object sender, RoutedEventArgs e)
        {
            var filePath = (string)((FrameworkElement)sender).DataContext;
            if (string.IsNullOrEmpty(filePath)) return;

            var target = Keyboard.FocusedElement as TextBox;
            if (target == null) return;

            string nameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            int caret = target.CaretIndex;

            string original = target.Text ?? string.Empty;
            target.Text = original.Insert(caret, nameWithoutExt);
            target.CaretIndex = caret + nameWithoutExt.Length;
            target.Focus();
        }

        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewTab();
        }

        private void TabCloseButton_Click(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            var tabItem = FindParent<TabItem>(button);
            if (tabItem?.DataContext is CollectorTab tab)
            {
                Tabs.Remove(tab);

                if (Tabs.Count == 0)
                {
                    AddNewTab();
                }

                if (tabControl.SelectedItem == null && Tabs.Count > 0)
                {
                    tabControl.SelectedItem = Tabs.Last();
                }

                UpdateTabUnderlineBrushes();
            }
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject current = child;
            while (current != null)
            {
                if (current is T typed)
                    return typed;

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }
    }

    public class CollectorTab : INotifyPropertyChanged
    {
        public ObservableCollection<string> SelectedPaths { get; } = new ObservableCollection<string>();

        private string _prefixText;
        private string _suffixText;
        private bool _isResolved;
        private Brush _underlineBrush = Brushes.Transparent;

        public CollectorTab()
        {
            CreatedAt = DateTime.UtcNow;
            SelectedPaths.CollectionChanged += SelectedPaths_CollectionChanged;
        }

        public DateTime CreatedAt { get; }

        public string PrefixText
        {
            get => _prefixText;
            set
            {
                if (_prefixText == value) return;
                _prefixText = value;
                OnPropertyChanged(nameof(PrefixText));
            }
        }

        public string SuffixText
        {
            get => _suffixText;
            set
            {
                if (_suffixText == value) return;
                _suffixText = value;
                OnPropertyChanged(nameof(SuffixText));
            }
        }

        public bool IsResolved
        {
            get => _isResolved;
            set
            {
                if (_isResolved == value) return;
                _isResolved = value;
                OnPropertyChanged(nameof(IsResolved));
            }
        }

        public Brush UnderlineBrush
        {
            get => _underlineBrush;
            set
            {
                if (_underlineBrush == value) return;
                _underlineBrush = value;
                OnPropertyChanged(nameof(UnderlineBrush));
            }
        }

        public string Header
        {
            get
            {
                int count = SelectedPaths.Count;
                string firstName = count > 0 ? Path.GetFileName(SelectedPaths[0]) : "Empty";
                return $"({count})-{firstName}";
            }
        }

        private void SelectedPaths_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(Header));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class FileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string path = value as string;
            if (string.IsNullOrEmpty(path)) return string.Empty;
            return Path.GetFileName(path);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
