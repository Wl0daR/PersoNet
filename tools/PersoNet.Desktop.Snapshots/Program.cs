using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PersoNet.Desktop;
using PersoNet.Desktop.Presentation.Access;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Company;
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
            var dialogs = new SnapshotDialogs();
            var user = new UserAccess();
            var employees = new EmployeesViewModel(api, api, api, api, user, time, dialogs);
            var leave = new LeaveViewModel(api, api, time, dialogs);
            var departments = new DepartmentsViewModel(api, dialogs);
            var positions = new PositionsViewModel(api, dialogs);
            var roles = new RolesViewModel(api, dialogs);
            var company = new CompanyViewModel(api, dialogs);
            var shell = new ShellViewModel(api, new HomeViewModel(api, api, api, user, time), employees, leave, departments, positions, roles, company, api, user, api);

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
            dialogs.Owner = window;

            foreach (var theme in new[] { ThemeMode.Light, ThemeMode.Dark })
            {
                app.ThemeMode = theme;
                shell.ChangeTheme(shell.Themes.Single(choice => choice.Theme == (theme == ThemeMode.Dark ? AppTheme.Dark : AppTheme.Light)));
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

                // The card of an employee with contracts, in its own window over the list.
                shell.NavigateCommand.Execute(shell.EmployeesItem);
                employees.Selected = employees.Items.First(item => item.Id == api.FirstEmployeeWithContracts);
                dialogs.Script = async (dialog, dialogWindow) =>
                {
                    var card = (EmployeeCardViewModel)dialog;
                    foreach (var size in Sizes)
                    {
                        await RenderDialogAsync(window, dialogWindow, size, Path.Combine(output, $"10-karta-pracownika-{size.Name}-{suffix}.png"));
                    }

                    // The contracts tab, and a new contract in a window over the card.
                    Tabs(dialogWindow).SelectedIndex = 1;
                    await RenderDialogAsync(window, dialogWindow, Sizes[1], Path.Combine(output, $"12-umowy-srodek-{suffix}.png"));
                    await RenderDialogAsync(window, dialogWindow, Sizes[2], Path.Combine(output, $"12-umowy-laptop150-{suffix}.png"));
                    dialogs.Script = async (_, contractWindow) =>
                    {
                        await RenderDialogAsync(window, contractWindow, Sizes[1], Path.Combine(output, $"19-nowa-umowa-srodek-{suffix}.png"), dialogWindow);
                        await RenderDialogAsync(window, contractWindow, Sizes[2], Path.Combine(output, $"19-nowa-umowa-laptop150-{suffix}.png"), dialogWindow);
                    };
                    await card.Contracts.NewAsync(CancellationToken.None);

                    // The portal account tab.
                    Tabs(dialogWindow).SelectedIndex = 3;
                    await RenderDialogAsync(window, dialogWindow, Sizes[2], Path.Combine(output, $"21-karta-konto-laptop150-{suffix}.png"));

                    // The history of changes.
                    Tabs(dialogWindow).SelectedIndex = 4;
                    await card.History.RefreshAsync(CancellationToken.None);
                    await RenderDialogAsync(window, dialogWindow, Sizes[1], Path.Combine(output, $"22-karta-historia-srodek-{suffix}.png"));
                    await RenderDialogAsync(window, dialogWindow, Sizes[2], Path.Combine(output, $"22-karta-historia-laptop150-{suffix}.png"));
                    Tabs(dialogWindow).SelectedIndex = 0;

                    // The same card with validation errors.
                    card.EditFirstName = string.Empty;
                    card.EditEmail = "zly adres";
                    await card.SaveAsync(CancellationToken.None);
                    await RenderDialogAsync(window, dialogWindow, Sizes[2], Path.Combine(output, $"11-karta-bledy-laptop150-{suffix}.png"));
                };
                await employees.OpenAsync(null, CancellationToken.None);

                // The leave tab of an employee with requests of every state, a new request from the calendar and the entitlement.
                employees.Selected = employees.Items.First(item => item.Id == api.FirstEmployeeWithLeave);
                dialogs.Script = async (dialog, dialogWindow) =>
                {
                    var calendar = ((EmployeeCardViewModel)dialog).Leave;
                    Tabs(dialogWindow).SelectedIndex = 2;
                    await RenderDialogAsync(window, dialogWindow, Sizes[1], Path.Combine(output, $"13-karta-urlop-srodek-{suffix}.png"));
                    await RenderDialogAsync(window, dialogWindow, Sizes[2], Path.Combine(output, $"13-karta-urlop-laptop150-{suffix}.png"));

                    calendar.SelectedDay = calendar.Days.First(day => day.IsInMonth && !day.IsDayOff && !day.HasLeave && day.Date.DayOfWeek == DayOfWeek.Monday && day.Date.Day > 14);
                    dialogs.Script = async (_, formWindow) =>
                    {
                        await RenderDialogAsync(window, formWindow, Sizes[1], Path.Combine(output, $"18-kalendarz-nowy-wniosek-srodek-{suffix}.png"), dialogWindow);
                        await RenderDialogAsync(window, formWindow, Sizes[2], Path.Combine(output, $"18-kalendarz-nowy-wniosek-laptop150-{suffix}.png"), dialogWindow);
                    };
                    await calendar.AddLeaveAsync(CancellationToken.None);

                    dialogs.Script = async (_, entitlementWindow) =>
                        await RenderDialogAsync(window, entitlementWindow, Sizes[1], Path.Combine(output, $"20-wymiar-urlopu-srodek-{suffix}.png"), dialogWindow);
                    await calendar.EditEntitlementAsync(CancellationToken.None);
                };
                await employees.OpenAsync(null, CancellationToken.None);
                employees.Selected = null;

                // A pending request waiting for a decision, in its window over the list.
                shell.NavigateCommand.Execute(shell.LeaveItem);
                leave.Selected = leave.Items.First(row => row.IsPending);
                dialogs.Script = async (_, dialogWindow) =>
                {
                    foreach (var size in Sizes)
                    {
                        await RenderDialogAsync(window, dialogWindow, size, Path.Combine(output, $"14-wniosek-{size.Name}-{suffix}.png"));
                    }
                };
                await leave.OpenAsync(null, CancellationToken.None);

                // All requests of the year.
                leave.StateFilter = LeaveViewModel.States[1];
                await RenderAsync(window, Sizes[1], Path.Combine(output, $"15-wnioski-wszystkie-srodek-{suffix}.png"));

                // The form of a new request, then the same form for leave in hours.
                dialogs.Script = async (dialog, dialogWindow) =>
                {
                    foreach (var size in Sizes)
                    {
                        await RenderDialogAsync(window, dialogWindow, size, Path.Combine(output, $"16-nowy-wniosek-{size.Name}-{suffix}.png"));
                    }

                    var form = (LeaveFormViewModel)dialog;
                    form.EditEmployee = form.Employees[1];
                    form.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == PersoNet.Contracts.Leave.LeaveKind.ChildCare);
                    form.IsHourly = true;
                    form.EditHours = "3";
                    await RenderDialogAsync(window, dialogWindow, Sizes[1], Path.Combine(output, $"17-wniosek-na-godziny-srodek-{suffix}.png"));
                    await RenderDialogAsync(window, dialogWindow, Sizes[2], Path.Combine(output, $"17-wniosek-na-godziny-laptop150-{suffix}.png"));
                };
                await leave.NewAsync(CancellationToken.None);
                leave.StateFilter = LeaveViewModel.States[0];

                // A new department and a position, each in its window.
                shell.NavigateCommand.Execute(shell.DepartmentsItem);
                dialogs.Script = async (_, dialogWindow) =>
                    await RenderDialogAsync(window, dialogWindow, Sizes[2], Path.Combine(output, $"21-nowy-dzial-laptop150-{suffix}.png"));
                await departments.NewAsync(CancellationToken.None);

                shell.NavigateCommand.Execute(shell.PositionsItem);
                dialogs.Script = async (_, dialogWindow) =>
                    await RenderDialogAsync(window, dialogWindow, Sizes[1], Path.Combine(output, $"22-nowe-stanowisko-srodek-{suffix}.png"));
                await positions.NewAsync(CancellationToken.None);

                // A role with its permissions, in its window over the list of roles.
                shell.NavigateCommand.Execute(shell.RolesItem);
                roles.Selected = roles.Items.First();
                dialogs.Script = async (_, dialogWindow) =>
                {
                    await RenderDialogAsync(window, dialogWindow, Sizes[1], Path.Combine(output, $"23-rola-srodek-{suffix}.png"));
                    await RenderDialogAsync(window, dialogWindow, Sizes[2], Path.Combine(output, $"23-rola-laptop150-{suffix}.png"));
                };
                await roles.EditAsync(null, CancellationToken.None);

                // The company's data and the kinds of leave in the portal, each in its window over the settings.
                shell.NavigateCommand.Execute(shell.CompanyItem);
                dialogs.Script = async (_, dialogWindow) =>
                {
                    await RenderDialogAsync(window, dialogWindow, Sizes[1], Path.Combine(output, $"24-dane-firmy-srodek-{suffix}.png"));
                    await RenderDialogAsync(window, dialogWindow, Sizes[2], Path.Combine(output, $"24-dane-firmy-laptop150-{suffix}.png"));
                };
                await company.EditDetailsAsync();
                dialogs.Script = async (_, dialogWindow) =>
                    await RenderDialogAsync(window, dialogWindow, Sizes[1], Path.Combine(output, $"25-urlopy-w-portalu-srodek-{suffix}.png"));
                await company.EditLeaveAsync();
            }

            window.Close();
        }

        private static TabControl Tabs(Window dialogWindow) => FindAll<TabControl>(dialogWindow).First(tabs => tabs.Items.Count == 5);

        internal static IntPtr AllowLargerThanScreen(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
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
            await SettleAsync();

            var content = (FrameworkElement)window.Content;
            content.UpdateLayout();

            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(Background(window, content.ActualWidth, content.ActualHeight));
            bitmap.Render(content);
            Save(bitmap, path);
        }

        /// <summary>
        /// Renders a dialog window centered over the main window (and over <paramref name="parent"/>, a dialog it was opened
        /// from), as the user sees it: the window frame and title bar are drawn here, since Windows draws them, not WPF.
        /// </summary>
        private static async Task RenderDialogAsync(
            Window window, Window dialog, (string Name, double Width, double Height) size, string path, Window? parent = null)
        {
            window.Width = size.Width;
            window.Height = size.Height;

            var layers = new List<Window>();
            if (parent is not null)
            {
                layers.Add(parent);
            }

            layers.Add(dialog);

            // Each window opens at its view's size, at most 95 % of the screen (as DialogService does), here the window size.
            foreach (var layer in layers)
            {
                var view = (FrameworkElement)layer.Content;
                var natural = SnapshotDialogs.NaturalSize(layer);
                view.Width = Math.Min(natural.Width, (size.Width * 0.95) - 16);
                view.Height = Math.Min(natural.Height, (size.Height * 0.95) - TitleBarHeight - 8);

                // The window shrinks with the view; a larger window would centre the view and push its footer out.
                layer.SizeToContent = SizeToContent.WidthAndHeight;
            }

            await SettleAsync();

            var content = (FrameworkElement)window.Content;
            content.UpdateLayout();
            var width = content.ActualWidth;
            var height = content.ActualHeight;

            var scene = new DrawingVisual();
            using (var context = scene.RenderOpen())
            {
                var main = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
                main.Render(Background(window, width, height));
                main.Render(content);
                context.DrawImage(main, new Rect(0, 0, width, height));

                foreach (var layer in layers)
                {
                    DrawWindow(context, layer, width, height);
                }
            }

            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(scene);
            Save(bitmap, path);
        }

        private const double TitleBarHeight = 32;

        private static void DrawWindow(DrawingContext context, Window layer, double screenWidth, double screenHeight)
        {
            var view = (FrameworkElement)layer.Content;
            view.UpdateLayout();
            var frame = new Rect(
                Math.Round((screenWidth - view.ActualWidth) / 2),
                Math.Round((screenHeight - view.ActualHeight - TitleBarHeight) / 2),
                view.ActualWidth,
                view.ActualHeight + TitleBarHeight);

            var background = layer.TryFindResource("SolidBackgroundFillColorBaseBrush") as Brush ?? Brushes.White;
            var stroke = layer.TryFindResource("CardStrokeColorDefaultBrush") as Brush ?? Brushes.Gray;
            var text = layer.TryFindResource("TextFillColorPrimaryBrush") as Brush ?? Brushes.Black;

            // Shadow, frame and title bar.
            for (var i = 1; i <= 6; i++)
            {
                var shadow = new SolidColorBrush(Color.FromArgb((byte)(28 - (i * 4)), 0, 0, 0));
                context.DrawRoundedRectangle(shadow, null, new Rect(frame.X - i, frame.Y - i + 4, frame.Width + (2 * i), frame.Height + (2 * i)), 8 + i, 8 + i);
            }

            context.DrawRoundedRectangle(background, new Pen(stroke, 1), frame, 8, 8);
            var title = new FormattedText(layer.Title, System.Globalization.CultureInfo.GetCultureInfo("pl-PL"), FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12, text, 1.0);
            context.DrawText(title, new Point(frame.X + 16, frame.Y + ((TitleBarHeight - title.Height) / 2)));
            var close = new FormattedText("\uE8BB", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe Fluent Icons"), 10, text, 1.0);
            context.DrawText(close, new Point(frame.Right - 30, frame.Y + ((TitleBarHeight - close.Height) / 2)));

            // A brush draws the view from its own top-left corner; rendering it directly would also apply its offset inside
            // the off-screen window, which is larger than the view on small screens.
            var image = new VisualBrush(view)
            {
                Stretch = Stretch.None,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect(0, 0, view.ActualWidth, view.ActualHeight),
            };
            context.DrawRectangle(image, null, new Rect(frame.X, frame.Y + TitleBarHeight, view.ActualWidth, view.ActualHeight));
        }

        // The Fluent window background is Mica (drawn by Windows, not WPF), so paint the theme's solid base colour under the content.
        private static DrawingVisual Background(Window window, double width, double height)
        {
            var background = new DrawingVisual();
            using var context = background.RenderOpen();
            var themeBackground = window.TryFindResource("SolidBackgroundFillColorBaseBrush") as Brush ?? Brushes.White;
            context.DrawRectangle(themeBackground, null, new Rect(0, 0, width, height));
            return background;
        }

        internal static async Task SettleAsync()
        {
            for (var i = 0; i < 5; i++)
            {
                await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                await Task.Delay(50);
            }
        }

        private static void Save(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file = File.Create(path);
            encoder.Save(file);
            Console.WriteLine($"{path} ({bitmap.PixelWidth}x{bitmap.PixelHeight})");
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
