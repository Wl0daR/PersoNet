using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersoNet.Desktop;
using PersoNet.Desktop.Presentation.Employees;
using PersoNet.Desktop.Presentation.Organization;
using PersoNet.Desktop.Presentation.Shell;
using PersoNet.Desktop.Snapshots;

// Usage: PersoNet.Desktop.Snapshots [output directory]
var output = Path.GetFullPath(args.Length > 0 ? args[0] : "snapshots");
Directory.CreateDirectory(output);

var thread = new Thread(() =>
{
    var app = new App();
    app.InitializeComponent();

    SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
    var frame = new DispatcherFrame();
    var work = Snapshots.RenderAllAsync(app, output);
    work.ContinueWith(_ => frame.Continue = false, TaskScheduler.FromCurrentSynchronizationContext());
    Dispatcher.PushFrame(frame);

    work.GetAwaiter().GetResult();
});
thread.SetApartmentState(ApartmentState.STA);
thread.Start();
thread.Join();

namespace PersoNet.Desktop.Snapshots
{
    internal static class Snapshots
    {
        // Window sizes in device-independent pixels: a typical monitor, the window's minimum, and a 1366x768 laptop at 150 % scaling.
        private static readonly (string Name, double Width, double Height)[] Sizes =
        [
            ("duzy", 1440, 900),
            ("srodek", 1100, 700),
            ("laptop150", 911, 512),
        ];

        public static async Task RenderAllAsync(Application app, string output)
        {
            var api = new DemoApi();
            var time = TimeProvider.System;
            var employees = new EmployeesViewModel(api, api, time);
            var shell = new ShellViewModel(api, employees, new DepartmentsViewModel(api), new PositionsViewModel(api));

            var window = new MainWindow(shell)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };
            window.Show();

            foreach (var theme in new[] { ThemeMode.Light, ThemeMode.Dark })
            {
                app.ThemeMode = theme;
                var suffix = theme == ThemeMode.Dark ? "ciemny" : "jasny";

                if (shell.IsSignedIn)
                {
                    await shell.SignOutAsync(CancellationToken.None);
                }

                await RenderAsync(window, Sizes[0], Path.Combine(output, $"00-logowanie-{suffix}.png"));

                await shell.SignInAsync(CancellationToken.None);

                var pages = Pages(window);
                for (var index = 0; index < pages.Count; index++)
                {
                    pages[index].Select();

                    foreach (var size in Sizes)
                    {
                        await RenderAsync(window, size, Path.Combine(output, $"{index + 1:00}-{pages[index].Name}-{size.Name}-{suffix}.png"));
                    }
                }

                // An open employee card with contracts, on the employees page.
                pages[0].Select();
                await employees.OpenAsync(api.ListItem(api.FirstEmployeeWithContracts), CancellationToken.None);
                foreach (var size in Sizes)
                {
                    await RenderAsync(window, size, Path.Combine(output, $"10-karta-pracownika-{size.Name}-{suffix}.png"));
                }

                // The same card with validation errors.
                employees.EditFirstName = string.Empty;
                employees.EditEmail = "zly adres";
                await employees.SaveAsync(CancellationToken.None);
                await RenderAsync(window, Sizes[2], Path.Combine(output, $"11-karta-bledy-laptop150-{suffix}.png"));
                employees.NewCommand.Execute(null);
            }

            window.Close();
        }

        private static List<(string Name, Action Select)> Pages(Window window)
        {
            var tabs = FindAll<TabControl>(window).First();
            return tabs.Items.Cast<TabItem>()
                .Select((tab, index) => (Slug(tab.Header?.ToString() ?? $"strona{index}"), (Action)(() => tabs.SelectedIndex = index)))
                .ToList();
        }

        private static async Task RenderAsync(Window window, (string Name, double Width, double Height) size, string path)
        {
            window.Width = size.Width;
            window.Height = size.Height;

            for (var i = 0; i < 5; i++)
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                await Task.Delay(50);
            }

            var content = (FrameworkElement)window.Content;
            content.UpdateLayout();

            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var context = background.RenderOpen())
            {
                context.DrawRectangle(window.Background ?? Brushes.White, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            }

            bitmap.Render(background);
            bitmap.Render(content);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(path);
            encoder.Save(file);
            Console.WriteLine($"{path} ({content.ActualWidth:0}x{content.ActualHeight:0})");
        }

        private static IEnumerable<T> FindAll<T>(DependencyObject root)
            where T : DependencyObject
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T match)
                {
                    yield return match;
                }

                foreach (var nested in FindAll<T>(child))
                {
                    yield return nested;
                }
            }
        }

        private static string Slug(string text) => text.Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant()
            .Replace('ł', 'l').Replace('ą', 'a').Replace('ę', 'e').Replace('ó', 'o').Replace('ś', 's').Replace('ż', 'z').Replace('ź', 'z').Replace('ć', 'c').Replace('ń', 'n')
            .Replace(' ', '-');
    }
}
