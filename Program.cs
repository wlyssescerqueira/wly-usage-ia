using System;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Color = System.Windows.Media.Color;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;

namespace WlyUsage;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--smart-check"))
        {
            QuietPlacement.Verify(); DodgePlacement.Verify();
            var bounds = Forms.Screen.PrimaryScreen!.WorkingArea;
            var screen = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            var cursor = Forms.Cursor.Position;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var selected = QuietPlacement.CaptureAndChoose(screen, new Rect(screen.Left, screen.Top, 680, 76), new Point(cursor.X, cursor.Y), bounds);
            Directory.CreateDirectory("artifacts");
            File.WriteAllText("artifacts/smart-check.json", JsonSerializer.Serialize(new { passed = true, selectedEmptyRegion = selected.HasValue, milliseconds = watch.ElapsedMilliseconds })); return;
        }
        if (args.Contains("--dodge-check"))
        {
            DodgePlacement.Verify(); Directory.CreateDirectory("artifacts");
            File.WriteAllText("artifacts/dodge-check.json", "{\"passed\":true,\"scenarios\":4}"); return;
        }
        if (args.Contains("--live-check"))
        {
            Directory.CreateDirectory("artifacts");
            File.WriteAllText("artifacts/live-check.json", JsonSerializer.Serialize(LocalUsage.Read())); return;
        }
        using var singleton = new System.Threading.Mutex(true, args.Contains("--preview") || args.Contains("--live-preview") ? "WlyUsage.Preview" : "WlyUsage.Prototype", out bool first);
        if (!first) return;
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.Run(new Island(args.Contains("--preview"), args.Contains("--privacy-check"), args.Contains("--live-preview")));
    }
}

internal sealed class Preferences
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = 12;
    public bool Pinned { get; set; }
    public bool PresentationMode { get; set; }
    public bool DodgeMouse { get; set; } = true;
    public bool SmartPlacement { get; set; } = true;
}

internal sealed class Island : Window
{
    private static readonly Brush Ink = Brush("#eef0f8"), Muted = Brush("#a1a9bd"), Accent = Brush("#8490ff"), Wash = Brush("#303548");
    private readonly StackPanel details = new();
    private readonly Grid agents = new();
    private readonly DispatcherTimer live = new() { Interval = TimeSpan.FromSeconds(5) };
    private bool reading, closing;
    private readonly MatrixRain matrixBackground = new() { Opacity = .55 };
    private readonly TextBlock claudeState = Text("Trabalhando · wly-contas", 12), codexState = Text("Disponível", 12);
    private readonly TextBlock summary = Text("Claude trabalhando", 12);
    private readonly DispatcherTimer fade = new() { Interval = TimeSpan.FromSeconds(4) };
    private readonly DispatcherTimer pointer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon trayIcon;
    private readonly string settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WlyUsage", "preferences.json");
    private readonly Preferences preferences;
    private readonly Button pin;
    private readonly Button hideButton;
    private readonly bool preview;
    private bool hovering, expanded = true, dragging;
    private bool manuallyHidden;
    private int phase;
    private nint handle;
    private string? saveError;
    private DateTimeOffset lastDodge = DateTimeOffset.MinValue;
    private bool analyzingScreen;

    public Island(bool preview, bool privacyCheck = false, bool livePreview = false)
    {
        this.preview = preview;
        preferences = ReadPreferences();
        Title = "WLY Usage · protótipo";
        Width = 680; SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false; Background = Brush("#181b23");
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(0),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8)
        });
        Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        FontFamily = new FontFamily("Segoe UI");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        Foreground = Ink;
        var shell = new Border { Background = Brush("#181b23"), BorderBrush = Brush("#353a4b"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 4, 8, 4), ToolTip = preview ? "Demonstração · dados simulados" : "Dados locais · botão direito para opções" };
        Content = shell;
        var layers = new Grid { ClipToBounds = true }; shell.Child = layers;
        layers.Children.Add(matrixBackground);
        var root = new Grid(); layers.Children.Add(root);
        root.ColumnDefinitions.Add(new ColumnDefinition());
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var controls = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
        Grid.SetColumn(controls, 1); root.Children.Add(controls);
        pin = IconAction("Fixar painel", "M16 3 21 8 17 9 14 12 14 16 12 18 6 12 8 10 12 10 15 7Z M9 15 3 21", TogglePin); controls.Children.Add(pin);
        hideButton = IconAction("Ocultar na bandeja", "M4 12 H20", HidePanel); controls.Children.Add(hideButton);
        root.Children.Add(details);
        agents.ColumnDefinitions.Add(new()); agents.ColumnDefinitions.Add(new());
        if (!preview) { claudeState.Text = "Lendo dados locais…"; codexState.Text = "Lendo dados locais…"; }
        var left = Agent("Claude", preview ? "Conta A" : "Local", claudeState, preview ? 64 : null, preview ? 89 : null, preview ? "Renova em 37 min" : "Sem coleta");
        var right = Agent("Codex", preview ? "Plus" : "Local", codexState, preview ? 49 : null, preview ? 8 : null, preview ? "Renova em 1h22" : "Sem coleta");
        UpdateMatrix();
        left.Margin = new Thickness(0, 0, 4, 0); right.Margin = new Thickness(4, 0, 0, 0);
        Grid.SetColumn(right, 1); agents.Children.Add(left); agents.Children.Add(right); details.Children.Add(agents);
        UpdateControls();
        MouseEnter += (_, _) => { if (TryDodge()) return; hovering = true; Expand(); fade.Stop(); UpdateControls(); };
        MouseLeave += (_, _) => { hovering = false; UpdateControls(); fade.Stop(); fade.Start(); };
        IsKeyboardFocusWithinChanged += (_, _) => UpdateControls();
        ContextMenu = BuildMenu();
        using (var iconStream = typeof(Island).Assembly.GetManifestResourceStream("WlyUsage.Assets.usage.ico")!)
            trayIcon = new System.Drawing.Icon(iconStream, new System.Drawing.Size(32, 32));
        Icon = Imaging.CreateBitmapSourceFromHIcon(trayIcon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "WLY Usage · dados locais", Visible = !preview && !livePreview };
        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("Mostrar painel / encerrar apresentação", null, (_, _) => Dispatcher.Invoke(ShowManually));
        var presentationItem = new Forms.ToolStripMenuItem("Modo apresentação") { CheckOnClick = false };
        presentationItem.Click += (_, _) => Dispatcher.Invoke(TogglePresentation);
        trayMenu.Items.Add(presentationItem);
        trayMenu.Opening += (_, _) => presentationItem.Checked = preferences.PresentationMode;
        trayMenu.Items.Add("Atualizar dados", null, (_, _) => Dispatcher.Invoke(RefreshLive));
        trayMenu.Items.Add("Sair", null, (_, _) => Dispatcher.Invoke(Close));
        tray.ContextMenuStrip = trayMenu; tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Dispatcher.Invoke(ShowManually); };
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x08000000 | 0x80);
            bool protectedCapture = SetWindowDisplayAffinity(handle, 0x11);
            tray.Text = protectedCapture ? "WLY Usage · exclusão de captura ativa" : "WLY Usage · exclusão de captura indisponível";
        };
        Loaded += (_, _) =>
        {
            handle = new WindowInteropHelper(this).EnsureHandle();
            SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x08000000 | 0x80);
            Left = double.IsFinite(preferences.Left) ? preferences.Left : SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
            Top = preferences.Top; ClampToScreen(); UpdatePin();
            bool captureApplied = SetWindowDisplayAffinity(handle, 0x11);
            int captureError = captureApplied ? 0 : Marshal.GetLastWin32Error();
            tray.Text = captureApplied ? "WLY Usage · exclusão de captura ativa" : "WLY Usage · exclusão de captura indisponível";
            if (privacyCheck)
            {
                bool originalPresentation = preferences.PresentationMode;
                preferences.PresentationMode = false;
                HidePanel(); NextPhase();
                bool hiddenAfterActivity = !IsVisible;
                manuallyHidden = false; preferences.PresentationMode = true; Reveal();
                bool hiddenInPresentation = !IsVisible;
                preferences.PresentationMode = false; Reveal();
                bool restored = IsVisible;
                bool captureProtected = GetWindowDisplayAffinity(handle, out uint affinity) && affinity == 0x11;
                Directory.CreateDirectory("artifacts");
                File.WriteAllText("artifacts/privacy-check.json", JsonSerializer.Serialize(new { hiddenAfterActivity, hiddenInPresentation, restored, captureProtected, captureError }));
                preferences.PresentationMode = originalPresentation;
                Close(); return;
            }
            if (livePreview)
            {
                RefreshLive();
                var capture = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                capture.Tick += (_, _) => { capture.Stop(); ExportPreview("preview-live.png"); Close(); };
                capture.Start(); return;
            }
            if (preview)
            {
                var capture = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(750) };
                capture.Tick += (_, _) =>
                {
                    capture.Stop(); hovering = false; UpdateControls(); ExportPreview();
                    hovering = true; UpdateControls(); ExportPreview("preview-hover.png"); Close();
                };
                capture.Start();
                return;
            }
            if (preferences.PresentationMode) HidePanel();
            fade.Start(); live.Start(); RefreshLive(); pointer.Start();
        };
        fade.Tick += (_, _) => { fade.Stop(); if (!preferences.Pinned && !hovering && !dragging) Collapse(); };
        live.Tick += (_, _) => RefreshLive();
        pointer.Tick += (_, _) =>
        {
            if (TryDodge()) return;
            var p = Forms.Cursor.Position; var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            var dip = transform.Transform(new Point(p.X, p.Y));
            bool inside = IsVisible && dip.X >= Left && dip.X <= Left + ActualWidth && dip.Y >= Top && dip.Y <= Top + ActualHeight;
            if (inside == hovering) return; hovering = inside; UpdateControls();
            if (inside) { SetTransparent(false); Expand(); fade.Stop(); } else { fade.Stop(); fade.Start(); }
        };
        Closed += (_, _) => { closing = true; fade.Stop(); live.Stop(); pointer.Stop(); tray.Dispose(); trayIcon.Dispose(); if (!preview) Save(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { preferences.Pinned = false; UpdatePin(); Collapse(true); } };
    }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private static TextBlock Text(string value, double size, bool bold = false) => new() { Text = value, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = bold ? Ink : Muted, VerticalAlignment = VerticalAlignment.Center };
    private static Button Action(string label, System.Action callback)
    {
        var button = new Button { Content = label, FontSize = 11, Foreground = Muted, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 4, 8, 4), Cursor = Cursors.Hand, MinHeight = 24 };
        var template = new ControlTemplate(typeof(Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
        border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(PaddingProperty));
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(presenter); template.VisualTree = border; button.Template = template;
        button.MouseEnter += (_, _) => { button.Background = Wash; button.Foreground = Ink; };
        button.MouseLeave += (_, _) => { button.Background = Brushes.Transparent; button.Foreground = Muted; };
        button.GotKeyboardFocus += (_, _) => button.Background = Wash;
        button.LostKeyboardFocus += (_, _) => button.Background = Brushes.Transparent;
        System.Windows.Automation.AutomationProperties.SetName(button, label);
        button.Click += (_, _) => callback(); return button;
    }
    private static Button IconAction(string label, string geometry, System.Action callback)
    {
        var button = Action(label, callback);
        button.Width = 32; button.Height = 32; button.Padding = new Thickness(8);
        button.ToolTip = label;
        var icon = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(geometry),
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            StrokeThickness = 1.6,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            RenderTransformOrigin = new Point(.5, .5)
        };
        icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new System.Windows.Data.Binding("Foreground") { Source = button });
        button.Content = icon;
        return button;
    }
    private Border Agent(string name, string account, TextBlock status, int? five, int? week, string reset)
    {
        if (status.Parent is Panel previousParent) previousParent.Children.Remove(status);
        var layout = new DockPanel();
        var icon = AgentIcon(name); icon.VerticalAlignment = VerticalAlignment.Center;
        AttachDrag(icon);
        DockPanel.SetDock(icon, Dock.Left); layout.Children.Add(icon);
        var panel = new StackPanel();
        var heading = new DockPanel();
        status.FontSize = 10;
        status.TextWrapping = TextWrapping.NoWrap;
        status.TextTrimming = TextTrimming.CharacterEllipsis;
        status.MaxWidth = 135;
        status.Margin = new Thickness(8, 0, 0, 0);
        status.SetBinding(ToolTipProperty, new System.Windows.Data.Binding("Text") { Source = status });
        DockPanel.SetDock(status, Dock.Right); heading.Children.Add(status);
        var identity = new StackPanel { Orientation = Orientation.Horizontal };
        AttachDrag(identity);
        identity.Children.Add(Text(name, 12, true));
        var badge = Text(account, 10); badge.Margin = new Thickness(6, 0, 0, 0); identity.Children.Add(badge);
        heading.Children.Add(identity); panel.Children.Add(heading);
        panel.Children.Add(Meter("5h", five, reset.Replace("Renova em ", "↻ ")));
        panel.Children.Add(Meter("Semana", week));
        layout.Children.Add(panel);
        return new Border { Background = Brush("#b820242f"), BorderBrush = Brush("#2b3040"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(8), Child = layout, ToolTip = reset };
    }
    private void AttachDrag(FrameworkElement target)
    {
        target.Cursor = Cursors.SizeAll;
        target.MouseLeftButtonDown += (_, e) =>
        {
            if (preferences.DodgeMouse && !ControlHeld()) return;
            if (e.ClickCount == 2) { TogglePin(); return; }
            dragging = true; SetTransparent(false);
            try { DragMove(); }
            catch (InvalidOperationException) { }
            finally { dragging = false; ClampToScreen(); Save(); }
        };
    }
    private static bool ControlHeld() => (GetAsyncKeyState(0x11) & 0x8000) != 0;
    private bool TryDodge()
    {
        if (preview || closing || !IsVisible || !preferences.DodgeMouse || ControlHeld() || dragging || ContextMenu?.IsOpen == true) return false;
        var cursor = Forms.Cursor.Position;
        var point = PointFromScreen(new Point(cursor.X, cursor.Y));
        var localArea = new Rect(-16, -16, ActualWidth + 32, ActualHeight + 32);
        if (!localArea.Contains(point)) return false;
        // Nunca captura o clique que se destinava à janela de baixo.
        SetTransparent(true);
        if (analyzingScreen) return true;
        if ((DateTimeOffset.UtcNow - lastDodge).TotalMilliseconds < 600) return true;
        var current = new Rect(Left, Top, ActualWidth, ActualHeight);
        var mouse = new Point(Left + point.X, Top + point.Y);
        if (preferences.SmartPlacement)
        {
            FindQuietPosition(current, mouse); return true;
        }
        MoveAside(DodgePlacement.Choose(WorkArea(), current, mouse));
        return true;
    }
    private async void FindQuietPosition(Rect panel, Point mouse)
    {
        analyzingScreen = true; lastDodge = DateTimeOffset.UtcNow;
        var area = WorkArea();
        var pixels = Forms.Screen.FromHandle(handle).WorkingArea;
        try
        {
            var destination = await System.Threading.Tasks.Task.Run(() => QuietPlacement.CaptureAndChoose(area, panel, mouse, pixels));
            if (closing || !IsVisible || ControlHeld() || dragging || !preferences.DodgeMouse || ContextMenu?.IsOpen == true) return;
            if (!preferences.SmartPlacement) destination = null;
            // Revalida com a posição atual do mouse: ele pode ter andado durante a análise.
            var cursor = Forms.Cursor.Position;
            var local = PointFromScreen(new Point(cursor.X, cursor.Y));
            var currentMouse = new Point(Left + local.X, Top + local.Y);
            if (destination is { } point)
            {
                var safety = new Rect(point, panel.Size); safety.Inflate(64, 64);
                if (safety.Contains(currentMouse)) destination = null;
            }
            MoveAside(destination ?? DodgePlacement.Choose(area, panel, currentMouse));
        }
        finally { analyzingScreen = false; }
    }
    private void MoveAside(Point? destination)
    {
        if (destination is { } target)
        {
            Left = target.X; Top = target.Y; lastDodge = DateTimeOffset.UtcNow;
            hovering = false; UpdateControls(); fade.Stop(); fade.Start();
        }
    }
    private void UpdateControls()
    {
        pin.Visibility = hovering || preferences.Pinned || IsKeyboardFocusWithin ? Visibility.Visible : Visibility.Hidden;
        hideButton.Visibility = hovering || IsKeyboardFocusWithin ? Visibility.Visible : Visibility.Hidden;
    }
    private static Border AgentIcon(string name)
    {
        string file = name == "Claude" ? "claude" : "openai";
        using var stream = typeof(Island).Assembly.GetManifestResourceStream($"WlyUsage.Assets.{file}.svg")!;
        var svg = System.Xml.Linq.XDocument.Load(stream);
        var data = svg.Descendants().First(element => element.Name.LocalName == "path").Attribute("d")!.Value;
        var icon = new System.Windows.Shapes.Path { Data = Geometry.Parse(data), Fill = name == "Claude" ? Brush("#e3ad91") : Ink, Stretch = Stretch.Uniform, Width = 20, Height = 20 };
        System.Windows.Automation.AutomationProperties.SetName(icon, name == "Claude" ? "Claude" : "OpenAI · Codex");
        return new Border { Child = icon, Width = 30, Height = 30, Background = Brush("#2c3140"), CornerRadius = new CornerRadius(4), Margin = new Thickness(0, 0, 8, 0) };
    }
    private static StackPanel Meter(string label, int? value, string renewal = "")
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var fill = Brush(value >= 85 ? "#e58c98" : value >= 60 ? "#e9b975" : "#82c8ac");
        var caption = Text(label, 10); caption.Width = 40; panel.Children.Add(caption);
        var track = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1, Columns = 28, Height = 8, Width = 110, VerticalAlignment = VerticalAlignment.Center };
        System.Windows.Automation.AutomationProperties.SetName(track, value.HasValue ? $"{label}: {value}% consumido" : $"{label}: sem dados");
        for (int i = 0; i < 28; i++)
        {
            double fraction = Math.Clamp((value ?? 0) / 100d * 28 - i, 0, 1);
            var segment = new Grid { Background = Wash, Margin = new Thickness(0, 0, i == 27 ? 0 : 2, 0) };
            segment.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(fraction, GridUnitType.Star) });
            segment.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - fraction, GridUnitType.Star) });
            if (fraction > 0) segment.Children.Add(new Border { Background = fill });
            track.Children.Add(segment);
        }
        panel.Children.Add(track);
        var percent = Text(value.HasValue ? $"{value}%" : "—", 10, true); percent.Foreground = value.HasValue ? fill : Muted; percent.Width = 32; percent.TextAlignment = TextAlignment.Right; panel.Children.Add(percent);
        var reset = Text(renewal, 10); reset.Margin = new Thickness(8, 0, 0, 0); panel.Children.Add(reset);
        return panel;
    }
    private void AnimateOpacity(double value) => BeginAnimation(OpacityProperty, new DoubleAnimation(value, TimeSpan.FromMilliseconds(350)));
    private void Expand() { details.Visibility = Visibility.Visible; expanded = true; SetTransparent(false); AnimateOpacity(1); }
    private void Collapse(bool force = false)
    {
        if (!force && (claudeState.Text.StartsWith("Trabalhando", StringComparison.Ordinal) || codexState.Text.StartsWith("Trabalhando", StringComparison.Ordinal))) return;
        expanded = false; AnimateOpacity(.35); SetTransparent(true);
    }
    private void Reveal() { if (manuallyHidden || preferences.PresentationMode) return; Show(); Expand(); fade.Stop(); fade.Start(); }
    private void HidePanel() { manuallyHidden = true; hovering = false; fade.Stop(); ContextMenu?.SetCurrentValue(ContextMenu.IsOpenProperty, false); Hide(); }
    private void ShowManually() { manuallyHidden = false; preferences.PresentationMode = false; Save(); Reveal(); }
    private void TogglePresentation()
    {
        preferences.PresentationMode = !preferences.PresentationMode;
        if (preferences.PresentationMode) HidePanel();
        else { manuallyHidden = false; Reveal(); }
        Save();
    }
    private void TogglePin() { preferences.Pinned = !preferences.Pinned; UpdatePin(); Save(); Reveal(); }
    private void UpdatePin()
    {
        string label = preferences.Pinned ? "Desafixar painel" : "Fixar painel";
        pin.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(pin, label);
        if (pin.Content is System.Windows.Shapes.Path icon) icon.RenderTransform = new RotateTransform(preferences.Pinned ? 0 : -45);
        UpdateControls();
    }
    private void NextPhase()
    {
        phase = (phase + 1) % 4;
        (claudeState.Text, codexState.Text, summary.Text) = phase switch
        {
            1 => ("Concluído · wly-contas", "Trabalhando · wly-usage-ia", "Codex trabalhando"),
            2 => ("Aguardando você", "Trabalhando · wly-usage-ia", "Claude aguarda você"),
            3 => ("Disponível", "Concluído · wly-usage-ia", "Tudo concluído"),
            _ => ("Trabalhando · wly-contas", "Disponível", "Claude trabalhando")
        };
        UpdateMatrix();
        Reveal();
    }
    private void UpdateMatrix()
    {
        matrixBackground.SetWorking(claudeState.Text.StartsWith("Trabalhando", StringComparison.Ordinal) || codexState.Text.StartsWith("Trabalhando", StringComparison.Ordinal));
    }
    private async void RefreshLive()
    {
        if (preview || reading || closing) return;
        reading = true;
        try
        {
            var readings = await System.Threading.Tasks.Task.Run(LocalUsage.Read);
            if (closing) return;
            bool wasWorking = claudeState.Text.StartsWith("Trabalhando") || codexState.Text.StartsWith("Trabalhando");
            agents.Children.Clear();
            foreach (var (entry, index) in readings.Select((entry, index) => (entry, index)))
            {
                var status = index == 0 ? claudeState : codexState;
                status.Text = entry.Activity;
                bool stale = entry.Collected < DateTimeOffset.UtcNow.AddMinutes(-30);
                string renewal = entry.Five?.Reset is { } reset ? "Renova em " + FormatRemaining(reset) : "Sem coleta 5h";
                var card = Agent(entry.Name, stale ? "desatualizado" : entry.Account, status,
                    entry.Five is { } five ? (int)Math.Round(five.Used) : null,
                    entry.Week is { } week ? (int)Math.Round(week.Used) : null, renewal);
                card.ToolTip = entry.Source + "\nColeta: " + (entry.Collected?.ToLocalTime().ToString("dd/MM HH:mm") ?? "indisponível") +
                    "\n5h: " + renewal + "\nSemana: " + (entry.Week?.Reset?.ToLocalTime().ToString("dd/MM HH:mm") ?? "sem dados") +
                    "\nAtividade inferida dos eventos locais recentes; sessões sem eventos por 3 min ficam inativas.";
                card.Margin = index == 0 ? new Thickness(0, 0, 4, 0) : new Thickness(4, 0, 0, 0);
                Grid.SetColumn(card, index); agents.Children.Add(card);
            }
            UpdateMatrix();
            if (wasWorking != readings.Any(entry => entry.Working)) Reveal();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { claudeState.Text = codexState.Text = "Fonte indisponível"; }
        finally { reading = false; }
    }
    private static string FormatRemaining(DateTimeOffset reset)
    {
        var remaining = reset - DateTimeOffset.UtcNow;
        return remaining.TotalHours >= 1 ? $"{(int)remaining.TotalHours}h{remaining.Minutes:00}" : $"{Math.Max(0, (int)Math.Ceiling(remaining.TotalMinutes))} min";
    }
    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu { Background = Brush("#20242f"), Foreground = Ink, BorderBrush = Brush("#586380"), Padding = new Thickness(8), FontSize = 14 };
        // Template próprio evita a coluna branca de ícones do tema nativo do Windows.
        var menuTemplate = new ControlTemplate(typeof(ContextMenu));
        var menuBorder = new FrameworkElementFactory(typeof(Border));
        menuBorder.SetValue(Border.BackgroundProperty, Brush("#20242f"));
        menuBorder.SetValue(Border.BorderBrushProperty, Brush("#586380"));
        menuBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        menuBorder.SetValue(Border.PaddingProperty, new Thickness(8));
        menuBorder.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        menuTemplate.VisualTree = menuBorder;
        menu.Template = menuTemplate;
        var menuStyle = new Style(typeof(MenuItem));
        menuStyle.Setters.Add(new Setter(Control.ForegroundProperty, Ink));
        menuStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        menuStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8)));
        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var itemBorder = new FrameworkElementFactory(typeof(Border));
        itemBorder.SetBinding(Border.BackgroundProperty, new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        itemBorder.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        var header = new FrameworkElementFactory(typeof(ContentPresenter));
        header.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        header.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
        itemBorder.AppendChild(header);
        itemTemplate.VisualTree = itemBorder;
        menuStyle.Setters.Add(new Setter(Control.TemplateProperty, itemTemplate));
        var highlight = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        highlight.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White));
        highlight.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#4255ff")));
        menuStyle.Triggers.Add(highlight); menu.Resources.Add(typeof(MenuItem), menuStyle);
        void Item(string label, System.Action action) { var item = new MenuItem { Header = label }; item.Click += (_, _) => action(); menu.Items.Add(item); }
        Item(preferences.Pinned ? "Desafixar painel" : "Fixar painel", TogglePin);
        Item(preferences.PresentationMode ? "Encerrar modo apresentação" : "Modo apresentação · ocultar até voltar", TogglePresentation);
        Item("Atualizar dados locais", RefreshLive);
        Item(preferences.DodgeMouse ? "Desativar desvio do mouse" : "Ativar desvio do mouse · Ctrl para interagir", () => { preferences.DodgeMouse = !preferences.DodgeMouse; Save(); });
        Item(preferences.SmartPlacement ? "Desativar posicionamento inteligente (experimental)" : "Ativar posicionamento inteligente (experimental)", () => { preferences.SmartPlacement = !preferences.SmartPlacement; Save(); });
        menu.Items.Add(new Separator());
        Item("Topo · esquerda", () => Snap(0)); Item("Topo · centro", () => Snap(.5)); Item("Topo · direita", () => Snap(1));
        foreach (var screen in Forms.Screen.AllScreens) { var target = screen; Item("Mover para " + screen.DeviceName, () => { var dpi = VisualTreeHelper.GetDpi(this); Left = target.WorkingArea.Left / dpi.DpiScaleX + 16; Top = target.WorkingArea.Top / dpi.DpiScaleY + 12; ClampToScreen(); Save(); Reveal(); }); }
        menu.Items.Add(new Separator()); Item("Ocultar na bandeja", HidePanel); Item("Sair", Close);
        menu.Closed += (_, _) => { fade.Stop(); fade.Start(); };
        menu.PlacementTarget = this;
        return menu;
    }
    protected override void OnContextMenuOpening(ContextMenuEventArgs e) { ContextMenu = BuildMenu(); base.OnContextMenuOpening(e); }
    private Rect WorkArea()
    {
        var dpi = VisualTreeHelper.GetDpi(this); var screen = Forms.Screen.FromHandle(handle).WorkingArea;
        return new Rect(screen.Left / dpi.DpiScaleX, screen.Top / dpi.DpiScaleY, screen.Width / dpi.DpiScaleX, screen.Height / dpi.DpiScaleY);
    }
    private void Snap(double alignment) { var area = WorkArea(); Left = area.Left + 16 + Math.Max(0, area.Width - Width - 32) * alignment; Top = area.Top + 12; Save(); Reveal(); }
    private void ClampToScreen() { var area = WorkArea(); Left = Math.Clamp(Left, area.Left, Math.Max(area.Left, area.Right - Width)); Top = Math.Clamp(Top, area.Top, Math.Max(area.Top, area.Bottom - (expanded ? 300 : ActualHeight))); }
    private Preferences ReadPreferences() { try { return File.Exists(settingsPath) ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(settingsPath)) ?? new() : new(); } catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException) { return new(); } }
    private void Save()
    {
        preferences.Left = Left; preferences.Top = Top;
        try { Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!); File.WriteAllText(settingsPath, JsonSerializer.Serialize(preferences)); saveError = null; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { saveError = "Não foi possível salvar a posição."; tray.Text = "WLY Usage · " + saveError; }
    }
    private void SetTransparent(bool enabled) { if (handle != 0) { int style = GetWindowLong(handle, -20); SetWindowLong(handle, -20, enabled ? style | 0x20 : style & ~0x20); } }
    private void ExportPreview(string filename = "preview.png")
    {
        UpdateLayout(); var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(this);
        Directory.CreateDirectory("artifacts"); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine("artifacts", filename)); encoder.Save(file);
    }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(nint hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(nint hwnd, int index, int value);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowDisplayAffinity(nint hwnd, out uint affinity);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}

