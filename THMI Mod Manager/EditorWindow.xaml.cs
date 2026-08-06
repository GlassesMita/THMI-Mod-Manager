using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;

namespace THMI_Mod_Manager;

/// <summary>基于 AvalonEdit 的轻量配置文件编辑器窗口。</summary>
public partial class EditorWindow : Window
{
    private string _filePath;
    private Encoding _encoding = new UTF8Encoding(false);
    private bool _dirty;
    private readonly bool _readOnly;

    /// <summary>保存成功后置 true，供调用方判断是否需要刷新设置页面。</summary>
    public bool Saved { get; private set; }

    private sealed record LanguageOption(string Display, string Definition);

    private static readonly object IniHighlightingLock = new();
    private static bool _iniHighlightingRegistered;

    public EditorWindow(string? filePath = null, bool readOnly = false)
    {
        InitializeComponent();
        EnsureIniHighlighting();
        _readOnly = readOnly;

        if (readOnly)
        {
            SaveButton.Visibility = Visibility.Collapsed;
            LanguageCombo.Visibility = Visibility.Collapsed;
            Editor.IsReadOnly = true;
        }

        LanguageCombo.ItemsSource = new[]
        {
            new LanguageOption("Plain Text", "Plain Text"),
            new LanguageOption("INI", "INI"),
            new LanguageOption("XML", "XML"),
            new LanguageOption("C#", "C#"),
            new LanguageOption("JavaScript", "JavaScript"),
            new LanguageOption("CSS", "CSS"),
            new LanguageOption("SQL", "SQL"),
            new LanguageOption("HTML", "HTML"),
            new LanguageOption("Java", "Java"),
            new LanguageOption("Python", "Python"),
            new LanguageOption("PHP", "PHP"),
        };
        LanguageCombo.DisplayMemberPath = nameof(LanguageOption.Display);

        ApplyTheme();
        LoadFile(filePath ?? Path.Combine(AppContext.BaseDirectory, "AppConfig.Schale"));
    }

    /// <summary>注册 INI 语法高亮（AvalonEdit 无内置 INI 定义，以 Xshd 方式注册）。</summary>
    private static void EnsureIniHighlighting()
    {
        if (_iniHighlightingRegistered) return;
        lock (IniHighlightingLock)
        {
            if (_iniHighlightingRegistered) return;
            const string xshd = """
                <SyntaxDefinition name="INI" extensions=".ini;.cfg;.schale;.conf;.properties"
                                  xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
                  <Color name="Comment" foreground="#2E7D32" />
                  <Color name="Section" foreground="#1F4E79" fontWeight="bold" />
                  <Color name="Key" foreground="#7030A0" />
                  <RuleSet>
                    <Rule color="Comment">^[;#].*$</Rule>
                    <Rule color="Section">^\[[^\]]*\]</Rule>
                    <Rule color="Key">^[^=\[\r\n;#][^=\r\n]*=</Rule>
                  </RuleSet>
                </SyntaxDefinition>
                """;
            using var reader = XmlReader.Create(new StringReader(xshd));
            var definition = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            HighlightingManager.Instance.RegisterHighlighting("INI", new[] { ".ini", ".cfg", ".schale", ".conf", ".properties" }, definition);
            _iniHighlightingRegistered = true;
        }
    }

    private void LoadFile(string path)
    {
        _filePath = path;
        if (File.Exists(path))
        {
            var bytes = File.ReadAllBytes(path);
            _encoding = DetectEncoding(bytes);
            Editor.Text = _encoding.GetString(bytes);
        }
        else
        {
            _encoding = new UTF8Encoding(false);
            Editor.Text = string.Empty;
        }

        var language = DetectLanguage(path);
        LanguageCombo.SelectedItem = LanguageCombo.Items.OfType<LanguageOption>().FirstOrDefault(o => o.Display == language)
                                     ?? LanguageCombo.Items.OfType<LanguageOption>().First();
        _dirty = false;
        UpdateTitle();
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return new UTF8Encoding(true);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return Encoding.Unicode;
        return new UTF8Encoding(false);
    }

    private static string DetectLanguage(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".log" or ".txt" => "Plain Text",
            ".xml" or ".xaml" or ".html" or ".htm" or ".config" or ".csproj" => "XML",
            ".cs" => "C#",
            ".js" or ".ts" => "JavaScript",
            ".css" => "CSS",
            ".sql" => "SQL",
            ".py" => "Python",
            ".java" => "Java",
            ".php" => "PHP",
            _ => "INI",
        };
    }

    private void ApplyTheme()
    {
        var isDark = Wpf.Ui.Appearance.ApplicationThemeManager.GetAppTheme() == Wpf.Ui.Appearance.ApplicationTheme.Dark;
        Editor.Background = new SolidColorBrush(isDark ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Colors.White);
        Editor.Foreground = new SolidColorBrush(isDark ? Color.FromRgb(0xD4, 0xD4, 0xD4) : Colors.Black);
        Editor.LineNumbersForeground = new SolidColorBrush(isDark ? Color.FromRgb(0x85, 0x85, 0x85) : Color.FromRgb(0x80, 0x80, 0x80));
    }

    private bool SaveFile()
    {
        try
        {
            File.WriteAllText(_filePath, Editor.Text, _encoding);
            _dirty = false;
            Saved = true;
            UpdateTitle();
            return true;
        }
        catch (Exception exception)
        {
            ShowSaveErrorAsync(exception.Message);
            return false;
        }
    }

    private void UpdateTitle()
    {
        var name = string.IsNullOrEmpty(_filePath) ? "未命名" : Path.GetFileName(_filePath);
        var editorName = _readOnly && Path.GetExtension(_filePath).Equals(".log", StringComparison.OrdinalIgnoreCase)
            ? "异常日志查看器"
            : "配置文件编辑器";
        Title = $"{( _dirty ? "*" : "")}{name} - {editorName}{(_readOnly ? "（只读）" : "")}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_filePath)) return;
        SaveFile();
    }

    /// <summary>保存失败时弹出 WPF-UI 错误对话框（不阻断保存流程）。</summary>
    private async void ShowSaveErrorAsync(string message)
    {
        var dialog = new Wpf.Ui.Controls.ContentDialog(RootDialogHost)
        {
            Title = "保存失败",
            Content = $"保存失败：{message}",
            CloseButtonText = "确定",
            DefaultButton = Wpf.Ui.Controls.ContentDialogButton.Close,
        };
        await dialog.ShowAsync();
    }

    /// <summary>询问是否保存：Primary=保存，Secondary=不保存，None=取消。</summary>
    private async Task<Wpf.Ui.Controls.ContentDialogResult> PromptSaveAsync()
    {
        var dialog = new Wpf.Ui.Controls.ContentDialog(RootDialogHost)
        {
            Title = "配置文件编辑器",
            Content = "文件已修改，是否保存？",
            PrimaryButtonText = "保存",
            SecondaryButtonText = "不保存",
            CloseButtonText = "取消",
            DefaultButton = Wpf.Ui.Controls.ContentDialogButton.Primary,
        };
        return await dialog.ShowAsync();
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LanguageCombo.SelectedItem is LanguageOption option)
            Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(option.Definition);
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        if (_readOnly) return;
        _dirty = true;
        UpdateTitle();
    }

    private bool _closingConfirmed;

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_readOnly || !_dirty || _closingConfirmed)
        {
            base.OnClosing(e);
            return;
        }
        // 未保存：先取消关闭，异步询问后再决定是否真正关闭
        e.Cancel = true;
        _ = PromptSaveOnCloseAsync();
    }

    private async Task PromptSaveOnCloseAsync()
    {
        var result = await PromptSaveAsync();
        switch (result)
        {
            case Wpf.Ui.Controls.ContentDialogResult.Primary: // 保存后关闭
                if (!SaveFile()) return;                      // 保存失败则留在编辑器
                break;
            case Wpf.Ui.Controls.ContentDialogResult.None:    // 取消关闭
                return;
            default: break;                                   // Secondary = 不保存，直接关闭
        }
        _closingConfirmed = true;
        _dirty = false;
        Close();
    }
}
