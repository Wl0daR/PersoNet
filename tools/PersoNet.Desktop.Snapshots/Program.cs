using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersoNet.Desktop;
using PersoNet.Desktop.Presentation.Employees;
using PersoNet.Desktop.Presentation.Home;
using PersoNet.Desktop.Presentation.Leave;
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

    try
    {
        work.GetAwaiter().GetResult();
    }
    catch (Exception exception)
    {
        // As a GitHub Actions annotation, so the cause is readable on the check run without opening the log.
        Console.WriteLine("::error title=Desktop screenshots::" + exception.ToString().Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", "%0A", StringComparison.Ordinal));
        Environment.ExitCode = 1;
    }
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
            var employees = new EmployeesViewModel(api, api, api, time);
            var leave = new LeaveViewModel(api, api, time);
            var shell = new ShellViewModel(api, new HomeViewModel(api, api, api, time), employees, leave, new DepartmentsViewModel(api), new PositionsViewModel(api));

            var window = new MainWindow(shell)
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = -20000,
                Top = -20000,
                ShowActivated = false,
                ShowInTaskbar = false,
            };

            // CI machines have a small screen (1024 x 768): open normal, not maximized, and let the window grow past the screen.
            window.WindowState = WindowState.Normal;
            window.SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(window).Handle).AddHook(AllowLargerThanScreen);
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

                var pages = Pages(shell);
                for (var index = 0; index < pages.Count; index++)
                {
                    pages[index].Select();

                    foreach (var size in Sizes)
                    {
                        await RenderAsync(window, size, Path.Combine(output, $"{index + 1:00}-{pages[index].Name}-{size.Name}-{suffix}.png"));
                    }
                }

                // An open employee card with contracts, on the employees page.
                shell.NavigateCommand.Execute(shell.EmployeesItem);
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
                employees.CloseCardCommand.Execute(null);

                // The contracts tab of the same card.
                await employees.OpenAsync(api.ListItem(api.FirstEmployeeWithContracts), CancellationToken.None);
                FindAll<TabControl>(window).First(tabs => tabs.Items.Count == 3).SelectedIndex = 1;
                await RenderAsync(window, Sizes[1], Path.Combine(output, $"12-umowy-srodek-{suffix}.png"));
                await RenderAsync(window, Sizes[2], Path.Combine(output, $"12-umowy-laptop150-{suffix}.png"));
                FindAll<TabControl>(window).First(tabs => tabs.Items.Count == 3).SelectedIndex = 0;
                employees.CloseCardCommand.Execute(null);

                // The leave tab of an employee with requests of every state.
                await employees.OpenAsync(api.ListItem(api.FirstEmployeeWithLeave), CancellationToken.None);
                FindAll<TabControl>(window).First(tabs => tabs.Items.Count == 3).SelectedIndex = 2;
                await RenderAsync(window, Sizes[1], Path.Combine(output, $"13-karta-urlop-srodek-{suffix}.png"));
                await RenderAsync(window, Sizes[2], Path.Combine(output, $"13-karta-urlop-laptop150-{suffix}.png"));
                FindAll<TabControl>(window).First(tabs => tabs.Items.Count == 3).SelectedIndex = 0;
                employees.CloseCardCommand.Execute(null);

                // A pending request waiting for a decision, all requests of the year, and the form of a new one.
                shell.NavigateCommand.Execute(shell.LeaveItem);
                leave.Selected = leave.Items.First(row => row.IsPending);
                foreach (var size in Sizes)
                {
                    await RenderAsync(window, size, Path.Combine(output, $"14-wniosek-{size.Name}-{suffix}.png"));
                }

                leave.StateFilter = LeaveViewModel.States[1];
                leave.Selected = leave.Items.First(row => row.Request.State == PersoNet.Contracts.Leave.LeaveState.Rejected);
                await RenderAsync(window, Sizes[1], Path.Combine(output, $"15-wnioski-wszystkie-srodek-{suffix}.png"));

                leave.NewCommand.Execute(null);
                foreach (var size in Sizes)
                {
                    await RenderAsync(window, size, Path.Combine(output, $"16-nowy-wniosek-{size.Name}-{suffix}.png"));
                }

                leave.ClosePaneCommand.Execute(null);
                leave.StateFilter = LeaveViewModel.States[0];
            }

            window.Close();
        }

        private static IntPtr AllowLargerThanScreen(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WmGetMinMaxInfo = 0x0024;
            if (message == WmGetMinMaxInfo)
            {
                // MINMAXINFO is five POINTs; the last one is ptMaxTrackSize. Not marked handled, so WPF's own handler
                // (which runs next) reads the raised limit instead of the screen size.
                Marshal.WriteInt32(lParam, 8 * 4, 4000);
                Marshal.WriteInt32(lParam, (8 * 4) + 4, 4000);
            }

            return IntPtr.Zero;
        }

        private static List<(string Name, Action Select)> Pages(ShellViewModel shell) =>
            shell.Navigation.Select(item => (Slug(item.Title), (Action)(() => shell.NavigateCommand.Execute(item)))).ToList();

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
                // The Fluent window background is Mica (drawn by Windows, not WPF), so paint the theme's solid base colour under the content.
                var themeBackground = window.TryFindResource("SolidBackgroundFillColorBaseBrush") as Brush ?? Brushes.White;
                context.DrawRectangle(themeBackground, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
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
