using Microsoft.Win32;
using Ookii.Dialogs.Wpf; // Ensure you have installed Ookii.Dialogs.Wpf
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Collections.Specialized;     // StringCollection
using System.Windows.Controls;            // ListBox
using System.Windows.Input;               // Mouse*, DragDrop

namespace FilesToClipboard
{
    public partial class MainWindow : Window
    {
        private Point _dragStart;

        public ObservableCollection<string> SelectedPaths { get; }
            = new ObservableCollection<string>();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private void btnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true
            };
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (var filePath in openFileDialog.FileNames)
                {
                    if (!SelectedPaths.Contains(filePath))
                        SelectedPaths.Add(filePath);
                }
            }
        }

        private void btnSelectDirs_Click(object sender, RoutedEventArgs e)
        {
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
                    AddFilesFromDirectoryRecursively(dir);
                }
            }
        }

        private void AddFilesFromDirectoryRecursively(string directory)
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (!SelectedPaths.Contains(file))
                    SelectedPaths.Add(file);
            }
        }

        private void btnCopy_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPaths.Count == 0) return;

            var sb = new StringBuilder();
            if(!string.IsNullOrWhiteSpace(prefixText.Text))
                sb.AppendLine(prefixText.Text);
            foreach (var filePath in SelectedPaths)
            {
                if (File.Exists(filePath))
                {
                    string content = File.ReadAllText(filePath);
                    sb.AppendLine($"\"{Path.GetFileName(filePath)}\":");
                    sb.AppendLine(content);
                    sb.AppendLine();
                }
            }
            if (!string.IsNullOrWhiteSpace(suffixText.Text))
                sb.AppendLine(suffixText.Text);

            Clipboard.SetText(sb.ToString());
        }

        private void btnRemovePath_Click(object sender, RoutedEventArgs e)
        {
            var pathToRemove = (string)((FrameworkElement)sender).DataContext;
            SelectedPaths.Remove(pathToRemove);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            SelectedPaths.Clear();
        }
        private void btnClearTexts_Click(object sender, RoutedEventArgs e)
        {
            prefixText.Text = suffixText.Text = string.Empty;
        }

        // ** New Paste Path button handler **
        private void btnPastePath_Click(object sender, RoutedEventArgs e)
        {
            string clipboardText = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(clipboardText))
            {
                MessageBox.Show("Clipboard is empty or does not contain text.",
                                "No Path Found",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            // Split clipboard text into lines
            string[] lines = clipboardText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            bool hasValidPath = false;

            foreach (string line in lines)
            {
                string path = line.Trim().Trim('"').Trim('\'');
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                if (File.Exists(path))
                {
                    if (!SelectedPaths.Contains(path))
                        SelectedPaths.Add(path);
                    hasValidPath = true;
                }
                else if (Directory.Exists(path))
                {
                    AddFilesFromDirectoryRecursively(path);
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
            // Show the copy cursor if there’s a file drop
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effects = DragDropEffects.Copy;
            else
                e.Effects = DragDropEffects.None;

            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            // Retrieve all dropped paths
            string[] droppedPaths = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (var path in droppedPaths)
            {
                if (File.Exists(path))
                {
                    if (!SelectedPaths.Contains(path))
                        SelectedPaths.Add(path);
                }
                else if (Directory.Exists(path))
                {
                    AddFilesFromDirectoryRecursively(path);
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

            // Build CF_HDROP payload
            var sc = new StringCollection();
            sc.AddRange(files);

            var data = new DataObject();
            data.SetFileDropList(sc);                               // primary payload
            data.SetText(string.Join(Environment.NewLine, files));  // optional convenience

            // Topmost windows can cover Explorer during drag; temporarily lower.
            bool wasTopmost = this.Topmost;
            try
            {
                this.Topmost = false;
                DragDrop.DoDragDrop(lb, data, DragDropEffects.Copy);
            }
            finally
            {
                this.Topmost = wasTopmost;
            }
        }


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
