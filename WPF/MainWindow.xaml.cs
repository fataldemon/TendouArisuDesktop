using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace AliceBotSettings;

[StructLayout(LayoutKind.Sequential)]
internal struct COPYDATASTRUCT
{
    public IntPtr dwData;
    public int cbData;
    public IntPtr lpData;
}

public partial class MainWindow : Window
{
    private const int WM_COPYDATA = 0x004A;
    private readonly PipeClient _pipe = new();
    private InitData? _initData;
    private int _ttsMode;
    private string _gptSovitsUrl = "";
    private string _gradioUrl = "";
    private string _simpleVitsUrl = "";
    private string _animCatFilter = "All";
    private ExpressionMappingEntry? _exprEditing;
    private ActionGroupFullEntry? _groupEditing;
    private FacialPresetEntry? _facialEditing;
    private System.Drawing.Color _bubbleBgColor = System.Drawing.Color.FromArgb(224, 76, 201, 240);
    private System.Drawing.Color _bubbleTextColor = System.Drawing.Color.White;
    private bool _translationEnabled;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            var helper = new WindowInteropHelper(this);
            HwndSource.FromHwnd(helper.Handle)?.AddHook(WndProc);
        };
        _pipe.OnInitReceived += OnInit;
        _pipe.OnConnectionChanged += c => Dispatcher.Invoke(() => UpdateConnectionStatus(c));
        _pipe.OnMessageReceived += OnMessage;
        _pipe.OnError += e => Dispatcher.Invoke(() => MessageBox.Show(e, "连接错误", MessageBoxButton.OK, MessageBoxImage.Warning));
        Loaded += (_, _) => _pipe.StartReconnect();
        SldModelScale.ValueChanged += (_, _) => LblModelScale.Text = SldModelScale.Value.ToString("F2") + "x";
        SldEyeStrength.ValueChanged += (_, _) => LblEyeStrength.Text = SldEyeStrength.Value.ToString("F0");
        SldEyeHeadRot.ValueChanged += (_, _) => LblEyeHeadRot.Text = SldEyeHeadRot.Value.ToString("F0");
        Closing += async (_, _) =>
        {
            await _pipe.SendCommand("restore_expression");
            await _pipe.SendCommand("stop_preview");
        };
        Closed += (_, _) => _pipe.Dispose();
        TxtAnimSearch.TextChanged += (_, _) =>
        {
            if (_initData != null)
                PopulateAnimationList(_initData.AnimationList);
        };
        LstExprMappings.MouseDoubleClick += (_, _) =>
        {
            var sel = (LstExprMappings.SelectedItem as dynamic)?.Emotion;
            if (sel != null) { _exprEditing = _initData?.ExpressionMappings.FirstOrDefault(m => m.Emotion == sel); if (_exprEditing != null) BuildExprEditPanel(); }
        };
        LstPresets.MouseDoubleClick += (_, _) =>
        {
            var sel = (LstPresets.SelectedItem as dynamic)?.GroupName;
            if (sel != null) { _groupEditing = _initData?.ActionGroups.FirstOrDefault(g => g.GroupName == sel); if (_groupEditing != null) BuildActionGroupEditPanel(); }
        };
        LstFacialPresets.MouseDoubleClick += (_, _) =>
        {
            var sel = (LstFacialPresets.SelectedItem as dynamic)?.PresetName;
            if (sel != null) { _facialEditing = _initData?.FacialPresets.FirstOrDefault(p => p.PresetName == sel); if (_facialEditing != null) BuildFacialEditPanel(); }
        };
        SetTtsModeButtons(0);
    }

    public void NavigateToTab(int index)
    {
        if (index >= 0 && index < MainTabs.Items.Count)
            MainTabs.SelectedIndex = index;
    }

    private void OnInit(InitData data)
    {
        _initData = data;
        Dispatcher.Invoke(() =>
        {
            PopulateAll(data);
            if (_facialEditing != null)
                BuildFacialEditPanel();
        });
    }

    private void OnMessage(JsonElement root)
    {
        Dispatcher.Invoke(() =>
        {
            var type = root.GetProperty("type").GetString();
            if (type == "status")
            {
                if (root.TryGetProperty("connected", out var c))
                    UpdateConnectionStatus(c.GetBoolean());
            }
            else if (type == "tts_test_start")
            {
                LblTtsStatus.Content = "正在测试...";
                LblTtsStatus.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary");
            }
            else if (type == "tts_test_result")
            {
                bool success = false;
                if (root.TryGetProperty("success", out var s))
                    success = s.GetBoolean();
                if (success)
                {
                    LblTtsStatus.Content = "✓ 测试成功";
                    LblTtsStatus.Foreground = (System.Windows.Media.Brush)FindResource("Accent");
                }
                else
                {
                    string err = "";
                    if (root.TryGetProperty("error", out var e))
                        err = e.GetString();
                    LblTtsStatus.Content = $"✗ 测试失败{(string.IsNullOrEmpty(err) ? "" : ": " + err)}";
                    LblTtsStatus.Foreground = (System.Windows.Media.Brush)FindResource("TextError");
                }
            }
            else if (type == "ref_audio_imported")
            {
                string emotionKey = "";
                if (root.TryGetProperty("emotionKey", out var ek))
                    emotionKey = ek.GetString();
                string fileName = "";
                if (root.TryGetProperty("fileName", out var fn))
                    fileName = fn.GetString();
                string promptLang = "ja";
                if (root.TryGetProperty("promptLang", out var pl))
                    promptLang = pl.GetString();
                if (_initData?.RefAudioConfigs != null)
                {
                    var entry = _initData.RefAudioConfigs.FirstOrDefault(e => e.EmotionKey == emotionKey && e.PromptLang == promptLang);
                    if (entry != null)
                    {
                        entry.AudioFileName = fileName;
                        entry.AudioFullPath = System.IO.Path.Combine(TxtRefAudioBaseDir.Text, fileName);
                    }
                    PopulateRefAudioList(_initData.RefAudioConfigs, "ja");
                    PopulateRefAudioList(_initData.RefAudioConfigs, "zh");
                }
            }
        });
    }

    private void UpdateConnectionStatus(bool connected)
    {
        LblConnStatus.Content = connected ? "● 已连接" : "○ 未连接";
        LblConnStatus.Foreground = connected
            ? (System.Windows.Media.Brush)FindResource("Accent")
            : (System.Windows.Media.Brush)FindResource("TextError");
        BtnConnect.IsEnabled = !connected;
        BtnDisconnect.IsEnabled = connected;
    }

    private void PopulateAll(InitData d)
    {
        TxtWebsocketUrl.Text = d.WebsocketUrl;
        _gptSovitsUrl = d.GptSovitsUrl ?? "";
        _gradioUrl = d.GradioUrl ?? "";
        _simpleVitsUrl = d.SimpleVitsUrl ?? "";
        SetTtsModeButtons(d.TtsMode);
        TxtBangWavPath.Text = d.BangbangkabangWavPath ?? "";
        TxtRefAudioBaseDir.Text = d.RefAudioBaseDir ?? "";
        PopulateRefAudioList(d.RefAudioConfigs, "ja");
        PopulateRefAudioList(d.RefAudioConfigs, "zh");
        _translationEnabled = d.TranslationEnabled;
        UpdateTranslationToggleUI();
        TxtTranslationUrl.Text = d.TranslationUrl;
        TxtTranslationAppId.Text = d.TranslationAppId;
        TxtTranslationKey.Password = d.TranslationKey;
        TxtTranslationSalt.Text = d.TranslationSalt;
        TxtMsgWidth.Text = d.MsgMaxWidth.ToString();
        TxtMsgHeight.Text = d.MsgHeight.ToString();
        TxtDialogHold.Text = d.DialogMinHoldTime.ToString("F0");
        if (d.BubbleColor != null && d.BubbleColor.Count >= 4)
        {
            _bubbleBgColor = System.Drawing.Color.FromArgb(
                (int)(d.BubbleColor[3] * 255), (int)(d.BubbleColor[0] * 255),
                (int)(d.BubbleColor[1] * 255), (int)(d.BubbleColor[2] * 255));
            RectBubbleColor.Fill = ToMediaBrush(_bubbleBgColor);
        }
        if (d.BubbleTextColor != null && d.BubbleTextColor.Count >= 4)
        {
            _bubbleTextColor = System.Drawing.Color.FromArgb(
                (int)(d.BubbleTextColor[3] * 255), (int)(d.BubbleTextColor[0] * 255),
                (int)(d.BubbleTextColor[1] * 255), (int)(d.BubbleTextColor[2] * 255));
            RectTextColor.Fill = ToMediaBrush(_bubbleTextColor);
        }
        SldModelScale.Value = d.ModelScale;
        LblModelScale.Text = d.ModelScale.ToString("F2") + "x";
        UpdateConnectionStatus(d.Connected);
        PopulateModelHistory(d.ModelHistory);
        PopulateAnimationList(d.AnimationList);
        PopulateExpressionList(d.ExpressionMappings);
        PopulateActionGroupList(d.ActionGroups);
        PopulateFacialPresetList(d.FacialPresets);
        PopulateEyeTab();
        TxtHistory.Text = d.DialogueHistory;
    }

    private void PopulateModelHistory(List<string> history)
    {
        LstModelHistory.ItemsSource = history.Select((h, idx) =>
        {
            var parts = h.Split('|');
            return new { Name = parts.Length > 1 ? parts[1] : h, Index = idx };
        }).ToList();
    }

    private void OnModelHistoryDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int idx)
            _ = _pipe.SendCommand("remove_model_from_history", new { index = idx });
    }

    private void PopulateAnimationList(List<AnimationEntry> list)
    {
        var categories = new HashSet<string> { "All" };
        foreach (var a in list) categories.Add(a.Category);

        IcAnimCategories.Items.Clear();
        foreach (var cat in categories.OrderBy(c => c == "All" ? 0 : 1).ThenBy(c => c))
        {
            var btn = new Button
            {
                Content = cat, Width = 70,
                Style = (Style)FindResource("SmallButton"),
                Margin = new Thickness(0, 0, 4, 0)
            };
            btn.Tag = cat;
            if (cat == _animCatFilter) btn.IsEnabled = false;
            btn.Click += (_, _) => { _animCatFilter = cat; PopulateAnimationList(list); };
            IcAnimCategories.Items.Add(btn);
        }

        var search = TxtAnimSearch.Text ?? "";
        var filtered = list
            .Where(a => _animCatFilter == "All" || a.Category == _animCatFilter)
            .Where(a => string.IsNullOrEmpty(search) || a.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(a => new { a.Name, a.Category, a.Duration, a.ActionParam, Display = $"{a.Name}  [{a.Category}]  {a.Duration:F1}s" })
            .ToList();
        LstAnimations.ItemsSource = filtered;
    }

    private void PopulateExpressionList(List<ExpressionMappingEntry> mappings)
    {
        var displayList = mappings.Select(m => new
        {
            m.Emotion,
            m.IsRandomEvent,
            EmotionDisplay = m.IsRandomEvent ? "🎲 " + m.Emotion : (m.Emotion is "待机" or "触摸" or "拖拽") ? "★ " + m.Emotion : m.Emotion,
            FacialSummary = !string.IsNullOrEmpty(m.FacialOverride) ? m.FacialOverride : (m.FacialGroup?.Preset ?? "-"),
            ActionSummary = m.Steps.Count > 1
                ? string.Join(" → ", m.Steps.Select(s => s.ActionGroupName))
                : !string.IsNullOrEmpty(m.ActionGroupName) ? m.ActionGroupName : (m.ActionGroup?.AnimationName ?? "-"),
        }).ToList();
        LstExprMappings.ItemsSource = displayList;
    }

    private void PopulateActionGroupList(List<ActionGroupFullEntry> groups)
    {
        var displayList = groups.Select(g => new
        {
            g.GroupName,
            g.IsIdle,
            DisplayName = g.IsIdle ? "★ " + g.GroupName : g.GroupName,
            Summary = (g.Loop ? "循环" : "单播") + " | " + g.FacialPreset + " | " +
                      string.Join("+", g.BodyClips.Select(c => c.BodyPart + ":" + (string.IsNullOrEmpty(c.ClipName) ? "-" : c.ClipName.Length > 15 ? c.ClipName[..15] + ".." : c.ClipName)))
        }).ToList();
        LstPresets.ItemsSource = displayList;
    }

    private void PopulateFacialPresetList(List<FacialPresetEntry> presets)
    {
        LstFacialPresets.ItemsSource = presets.Select(p => new
        {
            p.PresetName,
            Summary = string.Join(", ", p.Targets.Select(t => $"[{t.Index}]={t.Weight:F0}"))
        }).ToList();
    }

    #region Connection Tab Events

    private void OnConnectClick(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("connect");
    private void OnDisconnectClick(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("disconnect");

    #endregion

    #region Voice Settings Tab Events

    private void OnTtsModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int mode))
        {
            SaveUrlForMode(_ttsMode, TxtTtsUrl.Text);
            SetTtsModeButtons(mode);
            _ = _pipe.SendCommand("update_config", new { ttsMode = mode });
        }
    }

    private void SetTtsModeButtons(int mode)
    {
        _ttsMode = mode;
        BtnTtsGptSovits.IsEnabled = mode != 0;
        BtnTtsGradio.IsEnabled = mode != 1;
        BtnTtsSimpleVits.IsEnabled = mode != 2;
        BtnTtsNone.IsEnabled = mode != 3;
        LblTtsUrl.Content = mode switch { 0 => "GPT-SoVITS API 地址", 1 => "Gradio API 地址", 2 => "Simple-Vits API 地址", _ => "API 地址" };
        TabRefAudio.IsEnabled = (mode == 0);
        TxtTtsUrl.Text = GetUrlForMode(mode);
    }

    private string GetUrlForMode(int mode) => mode switch
    {
        0 => _gptSovitsUrl,
        1 => _gradioUrl,
        2 => _simpleVitsUrl,
        _ => ""
    };

    private void SaveUrlForMode(int mode, string url)
    {
        if (string.IsNullOrEmpty(url)) return;
        switch (mode)
        {
            case 0: _gptSovitsUrl = url; break;
            case 1: _gradioUrl = url; break;
            case 2: _simpleVitsUrl = url; break;
        }
    }

    private void OnSaveVoiceClick(object sender, RoutedEventArgs e)
    {
        SaveUrlForMode(_ttsMode, TxtTtsUrl.Text);
        _ = _pipe.SendCommand("update_config", new { ttsMode = _ttsMode, ttsUrl = TxtTtsUrl.Text, gptSovitsUrl = _gptSovitsUrl, gradioUrl = _gradioUrl, simpleVitsUrl = _simpleVitsUrl });
    }

    private void OnTtsSendClick(object sender, RoutedEventArgs e)
    {
        SaveUrlForMode(_ttsMode, TxtTtsUrl.Text);
        _ = _pipe.SendCommand("update_config", new { ttsMode = _ttsMode, ttsUrl = TxtTtsUrl.Text, gptSovitsUrl = _gptSovitsUrl, gradioUrl = _gradioUrl, simpleVitsUrl = _simpleVitsUrl });
        _ = _pipe.SendCommand("test_tts", new { text = TxtTtsTestLine.Text });
    }

    private void OnBangWavBrowseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "WAV Files|*.wav", Title = "选择邦邦咔邦 WAV 文件" };
        if (dlg.ShowDialog() == true)
        {
            TxtBangWavPath.Text = dlg.FileName;
            _ = _pipe.SendCommand("update_bangbangkabang_wav", new { bangbangkabangWavPath = dlg.FileName });
        }
    }

    private void OnVoiceSubTabChanged(object sender, SelectionChangedEventArgs e) { }

    private void OnRefAudioDirBrowseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "选择参考音频基础目录" };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            TxtRefAudioBaseDir.Text = dlg.SelectedPath;
            _ = _pipe.SendCommand("update_ref_audio_base_dir", new { refAudioBaseDir = dlg.SelectedPath });
        }
    }

    private void PopulateRefAudioList(List<RefAudioEntryDto> entries, string lang)
    {
        var target = lang == "ja" ? IcRefAudioJa : IcRefAudioZh;
        target.Items.Clear();
        if (entries == null || entries.Count == 0) return;

        var filtered = entries.Where(e => e.PromptLang == lang).ToList();

        foreach (var entry in filtered)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };

            var lblEmotion = new TextBlock
            {
                Text = entry.EmotionKey, Width = 55, VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 6, 0)
            };
            panel.Children.Add(lblEmotion);

            bool hasFile = !string.IsNullOrEmpty(entry.AudioFileName);
            var foreColor = hasFile
                ? (System.Windows.Media.Brush)FindResource("TextPrimary")
                : (System.Windows.Media.Brush)FindResource("TextSecondary");

            var txtFile = new TextBox { Text = entry.AudioFileName, Width = 120, Margin = new Thickness(0, 0, 4, 0), FontSize = 11, Foreground = foreColor };
            txtFile.TextChanged += (_, _) =>
            {
                entry.AudioFileName = txtFile.Text;
                _ = _pipe.SendCommand("update_ref_audio_entry", new
                {
                    refAudioEmotion = entry.EmotionKey,
                    refAudioPath = entry.AudioFileName,
                    refAudioPrompt = entry.PromptText,
                    refAudioLang = entry.PromptLang
                });
            };
            panel.Children.Add(txtFile);

            var btnBrowse = new Button { Content = "...", Width = 28, Height = 22, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(0, 0, 4, 0), FontSize = 10 };
            btnBrowse.Click += (_, _) =>
            {
                var dlg = new OpenFileDialog { Filter = "WAV Files|*.wav", Title = $"选择 [{lang}] {entry.EmotionKey} 的参考音频" };
                if (dlg.ShowDialog() == true)
                {
                    _ = _pipe.SendCommand("import_ref_audio", new
                    {
                        refAudioSourcePath = dlg.FileName,
                        refAudioEmotion = entry.EmotionKey,
                        refAudioLang = entry.PromptLang
                    });
                }
            };
            panel.Children.Add(btnBrowse);

            if (hasFile)
            {
                var btnPlay = new Button { Content = "▶", Width = 28, Height = 22, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(0, 0, 6, 0), FontSize = 10 };
                btnPlay.Click += (_, _) =>
                {
                    string fullPath = System.IO.Path.Combine(TxtRefAudioBaseDir.Text, entry.AudioFileName);
                    if (System.IO.File.Exists(fullPath))
                    {
                        var player = new System.Media.SoundPlayer(fullPath);
                        player.Play();
                    }
                };
                panel.Children.Add(btnPlay);
            }

            var txtPrompt = new TextBox { Text = entry.PromptText, Width = 300, FontSize = 11, Foreground = foreColor };
            txtPrompt.TextChanged += (_, _) =>
            {
                entry.PromptText = txtPrompt.Text;
                _ = _pipe.SendCommand("update_ref_audio_entry", new
                {
                    refAudioEmotion = entry.EmotionKey,
                    refAudioPath = entry.AudioFileName,
                    refAudioPrompt = entry.PromptText,
                    refAudioLang = entry.PromptLang
                });
            };
            panel.Children.Add(txtPrompt);

            target.Items.Add(panel);
        }
    }

    #endregion

    #region Translation Settings Tab Events

    private void OnTranslationToggleClick(object sender, RoutedEventArgs e)
    {
        _translationEnabled = !_translationEnabled;
        UpdateTranslationToggleUI();
        _ = _pipe.SendCommand("update_translation_toggle", new { translationEnabled = _translationEnabled });
    }

    private void UpdateTranslationToggleUI()
    {
        BtnTranslationToggle.Content = _translationEnabled ? "关闭" : "开启";
        BtnTranslationToggle.Style = _translationEnabled
            ? (Style)FindResource("DangerButton")
            : (Style)FindResource("PrimaryButton");
        LblTranslationStatus.Content = _translationEnabled ? "翻译已开启" : "翻译已关闭";
        LblTranslationStatus.Foreground = _translationEnabled
            ? (System.Windows.Media.Brush)FindResource("Accent")
            : (System.Windows.Media.Brush)FindResource("TextError");
    }

    private void OnTranslationSaveClick(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("update_translation_config", new
        {
            translationUrl = TxtTranslationUrl.Text,
            translationAppId = TxtTranslationAppId.Text,
            translationKey = TxtTranslationKey.Password,
            translationSalt = TxtTranslationSalt.Text
        });
        _ = _pipe.SendCommand("update_translation_toggle", new { translationEnabled = _translationEnabled });
    }

    #endregion

    #region Dialog Settings Tab Events
    private void OnBubbleColorPickClick(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.ColorDialog { Color = _bubbleBgColor, FullOpen = true };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _bubbleBgColor = dlg.Color;
            RectBubbleColor.Fill = ToMediaBrush(_bubbleBgColor);
        }
    }
    private void OnBubbleTextColorPickClick(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.ColorDialog { Color = _bubbleTextColor, FullOpen = true };
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            _bubbleTextColor = dlg.Color;
            RectTextColor.Fill = ToMediaBrush(_bubbleTextColor);
        }
    }
    private static SolidColorBrush ToMediaBrush(System.Drawing.Color c)
        => new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B));

    private void OnDialogSettingsSave(object sender, RoutedEventArgs e)
    {
        float hold = 10f;
        float.TryParse(TxtDialogHold.Text, out hold);
        _ = _pipe.SendCommand("update_dialog", new { msgWidth = GetInt(TxtMsgWidth), msgHeight = GetInt(TxtMsgHeight), dialogHold = hold });
        _ = _pipe.SendCommand("update_bubble_color", new
        {
            bubbleR = _bubbleBgColor.R / 255f, bubbleG = _bubbleBgColor.G / 255f, bubbleB = _bubbleBgColor.B / 255f, bubbleA = _bubbleBgColor.A / 255f,
            bubbleTextR = _bubbleTextColor.R / 255f, bubbleTextG = _bubbleTextColor.G / 255f, bubbleTextB = _bubbleTextColor.B / 255f, bubbleTextA = _bubbleTextColor.A / 255f
        });
    }
    private int GetInt(TextBox tb) => int.TryParse(tb.Text, out int v) ? v : 0;
    #endregion

    #region Model Tab Events
    private void OnModelBrowseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "VRM Files|*.vrm", Title = "选择 VRM 模型文件" };
        if (dlg.ShowDialog() == true) TxtVrmPath.Text = dlg.FileName;
    }
    private void OnModelLoadClick(object sender, RoutedEventArgs e)
    {
        if (LstModelHistory.SelectedIndex >= 0)
            _ = _pipe.SendCommand("load_model_from_history", new { index = LstModelHistory.SelectedIndex });
        else if (!string.IsNullOrEmpty(TxtVrmPath.Text))
            _ = _pipe.SendCommand("load_model", new { path = TxtVrmPath.Text });
        else
            MessageBox.Show("请先在历史列表中选择条目，或浏览选择 VRM 文件。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void OnModelRestoreClick(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("restore_default_model");
    private void OnModelHistorySelect(object sender, SelectionChangedEventArgs e)
    {
        int idx = LstModelHistory.SelectedIndex;
        if (idx >= 0 && _initData != null && idx < _initData.ModelHistory.Count)
        {
            var parts = _initData.ModelHistory[idx].Split('|');
            if (parts.Length > 0) TxtVrmPath.Text = parts[0];
        }
    }
    private void OnModelHistoryDoubleClick(object sender, MouseButtonEventArgs e)
    {
        int idx = LstModelHistory.SelectedIndex;
        if (idx >= 0)
            _ = _pipe.SendCommand("load_model_from_history", new { index = idx });
    }
    private void OnModelScaleApply(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("update_model_scale", new { modelScale = (float)SldModelScale.Value });
    #endregion

    #region Animation Tab Events
    private void OnAnimRefreshClick(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("scan_animations");
    private void OnAnimImportClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "FBX Files|*.fbx", Title = "导入动画" };
        if (dlg.ShowDialog() == true)
            _ = _pipe.SendCommand("import_animation", new { path = dlg.FileName });
    }
    private void OnRootMotionChanged(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("set_root_motion", new { enable = ChkRootMotion.IsChecked == true });
    private void OnAnimPreviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
            _ = _pipe.SendCommand("preview_animation", new { name });
    }
    #endregion

    #region Expression Mapping Tab Events

    private void OnExprRestoreDefaults(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("restore_default_mappings");

    private void OnExprAdd(object sender, RoutedEventArgs e)
    {
        _exprEditing = new ExpressionMappingEntry
        {
            Emotion = "",
            ActionGroupName = "Speak Normal",
            FacialOverride = "fun"
        };
        BuildExprEditPanel();
    }

    private void OnExprEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string emotion && _initData != null)
        {
            _exprEditing = _initData.ExpressionMappings.FirstOrDefault(m => m.Emotion == emotion);
            if (_exprEditing != null) BuildExprEditPanel();
        }
    }

    private void OnExprDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string emotion)
        {
            if (emotion is "待机" or "触摸" or "拖拽") return;
            _ = _pipe.SendCommand("delete_expression_mapping", new { emotion });
        }
    }

    private void OnExprMappingSelect(object sender, SelectionChangedEventArgs e) { }

    private void BuildExprEditPanel()
    {
        PanelExprEdit.Children.Clear();
        if (_exprEditing == null) return;
        var sp = PanelExprEdit;
        var entry = _exprEditing;

        var emoPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        emoPanel.Children.Add(new TextBlock { Text = "情绪:", Foreground = Res("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var txtEmotion = new TextBox { Width = 120, Text = entry.Emotion };
        txtEmotion.TextChanged += (_, _) => entry.Emotion = txtEmotion.Text;
        emoPanel.Children.Add(txtEmotion);
        sp.Children.Add(emoPanel);

        if (entry.Steps.Count > 0)
            BuildExprStepsEditor(sp, entry);
        else
            BuildExprLegacyEditor(sp, entry);

        var rndRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var chkRnd = new CheckBox { Content = "随机事件", VerticalAlignment = VerticalAlignment.Center, IsChecked = entry.IsRandomEvent };
        chkRnd.Checked += (_, _) => entry.IsRandomEvent = true;
        chkRnd.Unchecked += (_, _) => entry.IsRandomEvent = false;
        rndRow.Children.Add(chkRnd);
        sp.Children.Add(rndRow);

        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var btnSave = new Button { Content = "保存", Width = 70, Style = (Style)FindResource("PrimaryButton") };
        btnSave.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(entry.Emotion)) { MessageBox.Show("请输入情绪名称"); return; }
            string stepsJson = entry.Steps.Count > 0 ? System.Text.Json.JsonSerializer.Serialize(entry.Steps, JsonConfig.Options) : "";
            string actionX = entry.Steps.Count > 0 ? entry.Steps[0].ActionGroupName : entry.ActionGroupName;
            string facialX = entry.Steps.Count > 0 ? entry.Steps[0].FacialOverride : (entry.FacialOverride ?? "");
            float facialW = entry.Steps.Count > 0 && entry.Steps[0].FacialWeightOverride > 0
                ? entry.Steps[0].FacialWeightOverride
                : (entry.FacialWeightOverride > 0 ? entry.FacialWeightOverride : 1f);
            _ = _pipe.SendCommand("update_expression_mapping", new
            {
                emotion = entry.Emotion, actionX, facialX, facialW,
                isRandom = entry.IsRandomEvent, stepsJson
            });
            _ = _pipe.SendCommand("restore_expression");
            _exprEditing = null;
            PanelExprEdit.Children.Clear();
            PanelExprEdit.Children.Add(new TextBlock { Text = "已保存", Foreground = Res("Accent"), FontSize = 13 });
        };
        actionRow.Children.Add(btnSave);
        var btnCancel = new Button { Content = "取消", Width = 70, Margin = new Thickness(8, 0, 0, 0) };
        btnCancel.Click += (_, _) =>
        {
            _ = _pipe.SendCommand("restore_expression");
            _exprEditing = null;
            PanelExprEdit.Children.Clear();
        };
        actionRow.Children.Add(btnCancel);
        sp.Children.Add(actionRow);
    }

    private void BuildExprLegacyEditor(StackPanel sp, ExpressionMappingEntry entry)
    {
        sp.Children.Add(new TextBlock { Text = "动作组:", Foreground = Res("TextSecondary"), Margin = new Thickness(0, 8, 0, 4) });
        var cboGroup = new ComboBox { Width = 180 };
        if (_initData?.ActionGroups != null)
            foreach (var g in _initData.ActionGroups.OrderBy(g => g.GroupName))
                cboGroup.Items.Add(g.GroupName);
        cboGroup.SelectedItem = entry.ActionGroupName;
        cboGroup.SelectionChanged += (_, _) => { if (cboGroup.SelectedItem != null) entry.ActionGroupName = cboGroup.SelectedItem.ToString()!; };
        sp.Children.Add(cboGroup);

        sp.Children.Add(new TextBlock { Text = "表情覆盖:", Foreground = Res("TextSecondary"), Margin = new Thickness(0, 12, 0, 4) });
        var facialPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 4) };
        var btnNoneF = new Button { Content = "(无)", Width = 50, Style = (Style)FindResource("SmallButton"), FontSize = 10, Margin = new Thickness(0, 0, 2, 2) };
        btnNoneF.IsEnabled = !string.IsNullOrEmpty(entry.FacialOverride);
        btnNoneF.Click += (_, _) => { entry.FacialOverride = ""; BuildExprEditPanel(); };
        facialPanel.Children.Add(btnNoneF);
        foreach (var preset in FacialPresetNames.All)
        {
            var btn = new Button { Content = preset, Width = 72, Style = (Style)FindResource("SmallButton"), FontSize = 10, Margin = new Thickness(0, 0, 2, 2) };
            btn.IsEnabled = preset != entry.FacialOverride;
            btn.Click += (_, _) => { entry.FacialOverride = preset; BuildExprEditPanel(); };
            facialPanel.Children.Add(btn);
        }
        sp.Children.Add(facialPanel);

        var weightRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        weightRow.Children.Add(new TextBlock { Text = "权重:", Foreground = Res("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        float initWeight = entry.FacialWeightOverride > 0 ? entry.FacialWeightOverride : 1f;
        var sliderW = new Slider { Width = 140, Minimum = 0, Maximum = 1, Value = initWeight, SmallChange = 0.05, TickFrequency = 0.1 };
        var lblW = new TextBlock { Text = initWeight.ToString("F1"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        sliderW.ValueChanged += (_, ev) => { entry.FacialWeightOverride = (float)ev.NewValue; lblW.Text = ev.NewValue.ToString("F1"); };
        weightRow.Children.Add(sliderW);
        weightRow.Children.Add(lblW);
        sp.Children.Add(weightRow);

        var btnPreviewFacial = new Button { Content = "预览表情", Width = 80, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(0, 4, 0, 4) };
        btnPreviewFacial.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(entry.FacialOverride))
                _ = _pipe.SendCommand("preview_facial", new { facialX = entry.FacialOverride, facialW = entry.FacialWeightOverride > 0 ? entry.FacialWeightOverride : 1f });
        };
        sp.Children.Add(btnPreviewFacial);

        var btnConvert = new Button { Content = "转为动作序列", Width = 110, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(0, 8, 0, 0) };
        btnConvert.Click += (_, _) =>
        {
            entry.Steps.Add(new EmotionStepDto
            {
                ActionGroupName = entry.ActionGroupName,
                FacialOverride = entry.FacialOverride ?? "",
                FacialWeightOverride = entry.FacialWeightOverride,
                BlendDuration = 0.35f
            });
            BuildExprEditPanel();
        };
        sp.Children.Add(btnConvert);
    }

    private void BuildExprStepsEditor(StackPanel sp, ExpressionMappingEntry entry)
    {
        var seqHeaderRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        seqHeaderRow.Children.Add(new TextBlock { Text = "── 动作序列 ──", Foreground = Res("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
        var btnPreviewSeq = new Button { Content = "预览序列", Width = 80, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(8, 0, 0, 0) };
        btnPreviewSeq.Click += (_, _) =>
        {
            if (entry.Steps.Count > 0)
            {
                string json = System.Text.Json.JsonSerializer.Serialize(entry.Steps, JsonConfig.Options);
                _ = _pipe.SendCommand("preview_sequence", new { stepsJson = json });
            }
        };
        seqHeaderRow.Children.Add(btnPreviewSeq);
        sp.Children.Add(seqHeaderRow);

        for (int idx = 0; idx < entry.Steps.Count; idx++)
        {
            int stepIdx = idx;
            var step = entry.Steps[stepIdx];
            var stepBorder = new Border
            {
                BorderBrush = (Brush)FindResource("BorderColor"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 0, 4)
            };
            var stepPanel = new StackPanel();

            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            headerRow.Children.Add(new TextBlock { Text = "步骤 " + (stepIdx + 1), FontWeight = FontWeights.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            var cboGroup = new ComboBox { Width = 140, FontSize = 11 };
            if (_initData?.ActionGroups != null)
                foreach (var g in _initData.ActionGroups.OrderBy(g => g.GroupName))
                    cboGroup.Items.Add(g.GroupName);
            cboGroup.SelectedItem = step.ActionGroupName;
            cboGroup.SelectionChanged += (_, _) => { if (cboGroup.SelectedItem != null) { step.ActionGroupName = cboGroup.SelectedItem.ToString()!; BuildExprEditPanel(); } };
            headerRow.Children.Add(cboGroup);
            var groupInfo = _initData?.ActionGroups.FirstOrDefault(g => g.GroupName == step.ActionGroupName);
            if (groupInfo != null && groupInfo.Loop)
                headerRow.Children.Add(new TextBlock { Text = "(循环)", Foreground = Res("Accent"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0), FontSize = 10 });
            var btnDel = new Button { Content = "✕", Width = 26, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(6, 0, 0, 0), FontSize = 9 };
            btnDel.Click += (_, _) => { entry.Steps.RemoveAt(stepIdx); if (entry.Steps.Count == 0) entry.Steps.Clear(); BuildExprEditPanel(); };
            headerRow.Children.Add(btnDel);
            stepPanel.Children.Add(headerRow);

            var facialRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            facialRow.Children.Add(new TextBlock { Text = "表情覆盖:", Foreground = Res("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Margin = new Thickness(0, 0, 4, 0) });
            var btnNone = new Button { Content = "(无)", Width = 36, Style = (Style)FindResource("SmallButton"), FontSize = 9, Margin = new Thickness(0, 0, 2, 0) };
            btnNone.IsEnabled = !string.IsNullOrEmpty(step.FacialOverride);
            btnNone.Click += (_, _) => { step.FacialOverride = ""; BuildExprEditPanel(); };
            facialRow.Children.Add(btnNone);
            foreach (var preset in FacialPresetNames.All)
            {
                var btn = new Button { Content = preset, Width = 60, Style = (Style)FindResource("SmallButton"), FontSize = 9, Margin = new Thickness(0, 0, 2, 0) };
                btn.IsEnabled = preset != step.FacialOverride;
                btn.Click += (_, _) => { step.FacialOverride = preset; BuildExprEditPanel(); };
                facialRow.Children.Add(btn);
            }
            stepPanel.Children.Add(facialRow);

            var wRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            wRow.Children.Add(new TextBlock { Text = "覆盖权重:", Foreground = Res("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Margin = new Thickness(0, 0, 4, 0) });
            float wInit = step.FacialWeightOverride > 0 ? step.FacialWeightOverride : 1f;
            var slW = new Slider { Width = 100, Minimum = 0, Maximum = 1, Value = wInit, SmallChange = 0.05, TickFrequency = 0.1 };
            var lblWt = new TextBlock { Text = wInit.ToString("F1"), VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Margin = new Thickness(4, 0, 0, 0) };
            slW.ValueChanged += (_, ev) => { step.FacialWeightOverride = (float)ev.NewValue; lblWt.Text = ev.NewValue.ToString("F1"); };
            wRow.Children.Add(slW);
            wRow.Children.Add(lblWt);
            stepPanel.Children.Add(wRow);

            var blendRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            blendRow.Children.Add(new TextBlock { Text = stepIdx == 0 ? "进入时间:" : "过渡时间:", Foreground = Res("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Margin = new Thickness(0, 0, 4, 0) });
            var slBlend = new Slider { Width = 100, Minimum = 0, Maximum = 2, Value = step.BlendDuration, SmallChange = 0.01, TickFrequency = 0.1 };
            var lblBl = new TextBlock { Text = step.BlendDuration <= 0.001f ? "0s (无缝)" : step.BlendDuration.ToString("F2") + "s", VerticalAlignment = VerticalAlignment.Center, FontSize = 11, Margin = new Thickness(4, 0, 0, 0) };
            slBlend.ValueChanged += (_, ev) => { step.BlendDuration = (float)ev.NewValue; lblBl.Text = ev.NewValue <= 0.001 ? "0s (无缝)" : ev.NewValue.ToString("F2") + "s"; };
            blendRow.Children.Add(slBlend);
            blendRow.Children.Add(lblBl);
            stepPanel.Children.Add(blendRow);

            stepBorder.Child = stepPanel;
            sp.Children.Add(stepBorder);
        }

        var btnAdd = new Button { Content = "+ 添加步骤", Width = 100, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(0, 2, 0, 4) };
        btnAdd.Click += (_, _) => { entry.Steps.Add(new EmotionStepDto { ActionGroupName = "Speak Normal", BlendDuration = 0.35f }); BuildExprEditPanel(); };
        sp.Children.Add(btnAdd);

        var btnRevert = new Button { Content = "恢复为单动作", Width = 110, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(0, 0, 0, 0) };
        btnRevert.Click += (_, _) =>
        {
            if (entry.Steps.Count > 0) { entry.ActionGroupName = entry.Steps[0].ActionGroupName; entry.FacialOverride = entry.Steps[0].FacialOverride; entry.FacialWeightOverride = entry.Steps[0].FacialWeightOverride; }
            entry.Steps.Clear();
            BuildExprEditPanel();
        };
        sp.Children.Add(btnRevert);
    }

    #endregion

    #region Action Group Tab Events

    private void OnPresetRestoreDefaults(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("restore_default_presets");
    private void OnPresetAdd(object sender, RoutedEventArgs e) { }
    private void OnPresetSelect(object sender, SelectionChangedEventArgs e) { }

    private void OnPresetEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string groupName && _initData != null)
        {
            _groupEditing = _initData.ActionGroups.FirstOrDefault(g => g.GroupName == groupName);
            if (_groupEditing != null) BuildActionGroupEditPanel();
        }
    }

    private void OnPresetDeleteClick(object sender, RoutedEventArgs e) { }

    private void OnGroupAdd(object sender, RoutedEventArgs e)
    {
        _groupEditing = new ActionGroupFullEntry
        {
            GroupName = "",
            FacialPreset = "",
            FacialWeight = 1f,
            Loop = false,
            AllowRootMotion = false,
            BlendInBody = 0.35f,
            BlendInFacial = 0.15f,
            BlendOutBody = 0.35f,
            BlendOutFacial = 0.2f,
            HoldAfterTTS = 3f,
            HoldNoTTS = 4f,
            IsIdle = false
        };
        _groupEditing.BodyClips.Add(new PartClipEntryDto { BodyPart = "fullBody", ClipName = "" });
        BuildActionGroupEditPanel();
    }

    private void OnGroupDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name && name != "Idle")
            _ = _pipe.SendCommand("delete_action_group", new { name });
    }

    private void BuildActionGroupEditPanel()
    {
        PanelPresetEdit.Children.Clear();
        if (_groupEditing == null) return;
        var sp = PanelPresetEdit;
        var g = _groupEditing;

        var txtName = new TextBox { Text = g.GroupName, FontSize = 14, FontWeight = FontWeights.Bold, Width = 250, Margin = new Thickness(0, 0, 0, 8) };
        txtName.TextChanged += (_, _) => g.GroupName = txtName.Text;
        sp.Children.Add(txtName);

        // Allow Root Motion (per-group)
        var armRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var chkARM = new CheckBox { Content = "Allow Root Motion", VerticalAlignment = VerticalAlignment.Center, IsChecked = g.AllowRootMotion };
        chkARM.Checked += (_, _) => g.AllowRootMotion = true;
        chkARM.Unchecked += (_, _) => g.AllowRootMotion = false;
        armRow.Children.Add(chkARM);
        sp.Children.Add(armRow);

        // Enable Eye Tracking (per-group)
        var etRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var chkET = new CheckBox { Content = "眼球/头部跟踪", VerticalAlignment = VerticalAlignment.Center, IsChecked = g.EnableEyeTracking };
        chkET.Checked += (_, _) => g.EnableEyeTracking = true;
        chkET.Unchecked += (_, _) => g.EnableEyeTracking = false;
        etRow.Children.Add(chkET);
        sp.Children.Add(etRow);

        // Facial preset (editable)
        sp.Children.Add(new TextBlock { Text = "默认表情:", Foreground = Res("TextSecondary"), Margin = new Thickness(0, 0, 0, 4) });
        var facialPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };
        var btnNone = new Button { Content = "(无)", Width = 50, Style = (Style)FindResource("SmallButton"), FontSize = 10, Margin = new Thickness(0, 0, 2, 2) };
        btnNone.IsEnabled = !string.IsNullOrEmpty(g.FacialPreset);
        btnNone.Click += (_, _) => { g.FacialPreset = ""; BuildActionGroupEditPanel(); };
        facialPanel.Children.Add(btnNone);
        foreach (var preset in FacialPresetNames.All)
        {
            var btn = new Button { Content = preset, Width = 72, Style = (Style)FindResource("SmallButton"), FontSize = 10, Margin = new Thickness(0, 0, 2, 2) };
            btn.IsEnabled = preset != g.FacialPreset;
            btn.Click += (_, _) => { g.FacialPreset = preset; BuildActionGroupEditPanel(); };
            facialPanel.Children.Add(btn);
        }
        sp.Children.Add(facialPanel);

        var fwRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
        fwRow.Children.Add(new TextBlock { Text = "表情权重:", Foreground = Res("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        var sliderFW = new Slider { Width = 140, Minimum = 0, Maximum = 1, Value = g.FacialWeight, SmallChange = 0.05, TickFrequency = 0.1 };
        var lblFW = new TextBlock { Text = g.FacialWeight.ToString("F1"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        sliderFW.ValueChanged += (_, ev) => { g.FacialWeight = (float)ev.NewValue; lblFW.Text = ev.NewValue.ToString("F1"); };
        fwRow.Children.Add(sliderFW);
        fwRow.Children.Add(lblFW);
        sp.Children.Add(fwRow);

        var btnPrevFacial = new Button { Content = "预览表情", Width = 80, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(0, 0, 0, 8) };
        btnPrevFacial.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(g.FacialPreset))
                _ = _pipe.SendCommand("preview_facial", new { facialX = g.FacialPreset, facialW = g.FacialWeight });
        };
        sp.Children.Add(btnPrevFacial);

        // Loop
        var loopRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        loopRow.Children.Add(new TextBlock { Text = "循环:", Foreground = Res("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var chkLoop = new CheckBox { IsChecked = g.Loop, VerticalAlignment = VerticalAlignment.Center };
        chkLoop.Checked += (_, _) => g.Loop = true;
        chkLoop.Unchecked += (_, _) => g.Loop = false;
        loopRow.Children.Add(chkLoop);
        sp.Children.Add(loopRow);

        // Body clips per part
        sp.Children.Add(new TextBlock { Text = "身体动画 (按部位):", Foreground = Res("TextSecondary"), Margin = new Thickness(0, 8, 0, 4) });

        foreach (var part in BodyPartNames.All)
        {
            var existing = g.BodyClips.FirstOrDefault(c => c.BodyPart == part);
            string clipName = existing?.ClipName ?? "";

            var partRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
            partRow.Children.Add(new TextBlock { Text = part + ":", Width = 70, Foreground = Res("TextSecondary"), VerticalAlignment = VerticalAlignment.Center });

            var txtFilter = new TextBox { Width = 100, Margin = new Thickness(0, 0, 4, 0), FontSize = 11 };
            var cbo = new ComboBox { Width = 150, MaxDropDownHeight = 300 };
            var allNames = new List<string> { "(无)" };
            allNames.AddRange((_initData?.AnimationList ?? new List<AnimationEntry>()).Select(a => a.Name).OrderBy(name => name));
            txtFilter.TextChanged += (_, _) =>
            {
                string txt = txtFilter.Text ?? "";
                if (string.IsNullOrEmpty(txt))
                    cbo.ItemsSource = allNames;
                else
                    cbo.ItemsSource = allNames.Where(n => n.Contains(txt, StringComparison.OrdinalIgnoreCase)).ToList();
                cbo.IsDropDownOpen = true;
            };
            cbo.ItemsSource = allNames;
            cbo.SelectedItem = string.IsNullOrEmpty(clipName) ? "(无)" : clipName;
            partRow.Children.Add(txtFilter);
            partRow.Children.Add(cbo);
            string capturedPart = part;
            cbo.SelectionChanged += (_, _) =>
            {
                string sel = cbo.SelectedItem?.ToString() ?? "";
                if (sel == "(无)") sel = "";
                var entry = g.BodyClips.FirstOrDefault(c => c.BodyPart == capturedPart);
                if (entry != null) entry.ClipName = sel;
                else g.BodyClips.Add(new PartClipEntryDto { BodyPart = capturedPart, ClipName = sel });
            };

            var btnPrev = new Button { Content = "▶", Width = 32, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(4, 0, 0, 0) };
            var capturedCbo = cbo;
            btnPrev.Click += (_, _) =>
            {
                string current = capturedCbo.SelectedItem?.ToString();
                if (!string.IsNullOrEmpty(current) && current != "(无)")
                    _ = _pipe.SendCommand("preview_animation", new { name = current, bodyPart = capturedPart });
            };
            partRow.Children.Add(btnPrev);

            sp.Children.Add(partRow);
        }

        // Global preview
        var previewRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        var btnGlobalPreview = new Button { Content = "全局预览", Width = 80, Style = (Style)FindResource("SmallButton") };
        btnGlobalPreview.Click += (_, _) =>
        {
            var clipParts = new List<string>();
            foreach (var c in g.BodyClips)
                if (!string.IsNullOrEmpty(c.ClipName))
                    clipParts.Add(c.BodyPart + "=" + c.ClipName);
            Debug.WriteLine("[WPF] preview_group_action: facialX=" + (g.FacialPreset ?? "") + " w=" + g.FacialWeight + " arm=" + (g.AllowRootMotion ? 1 : 0) + " et=" + (g.EnableEyeTracking ? 1 : 0) + " clips=" + clipParts.Count);
            _ = _pipe.SendCommand("preview_group_action", new
            {
                actionX = string.Join("|", clipParts),
                facialX = g.FacialPreset ?? "",
                facialW = g.FacialWeight,
                actionY = g.AllowRootMotion ? 1f : 0f,
                actionW = g.EnableEyeTracking ? 1f : 0f
            });
        };
        previewRow.Children.Add(btnGlobalPreview);
        var btnStop = new Button { Content = "停止", Width = 60, Margin = new Thickness(8, 0, 0, 0) };
        btnStop.Click += (_, _) => _ = _pipe.SendCommand("stop_preview");
        previewRow.Children.Add(btnStop);
        sp.Children.Add(previewRow);

        // Timing info
        sp.Children.Add(new TextBlock { Text = $"BlendIn: body={g.BlendInBody:F2}s facial={g.BlendInFacial:F2}s | BlendOut: body={g.BlendOutBody:F2}s facial={g.BlendOutFacial:F2}s",
            Foreground = Res("TextSecondary"), FontSize = 11, Margin = new Thickness(0, 12, 0, 0) });
        sp.Children.Add(new TextBlock { Text = $"HoldAfterTTS: {g.HoldAfterTTS:F1}s | HoldNoTTS: {g.HoldNoTTS:F1}s",
            Foreground = Res("TextSecondary"), FontSize = 11 });

        // Save button
        var saveRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var btnSave = new Button { Content = "保存", Width = 70, Style = (Style)FindResource("PrimaryButton") };
        btnSave.Click += (_, _) =>
        {
            var clipParts = new List<string>();
            foreach (var c in g.BodyClips)
                clipParts.Add(c.BodyPart + "=" + (c.ClipName ?? ""));
            _ = _pipe.SendCommand("update_action_group", new
            {
                name = g.GroupName,
                facialX = g.FacialPreset ?? "",
                facialW = g.FacialWeight,
                actionX = string.Join("|", clipParts),
                actionY = g.AllowRootMotion ? 1f : 0f,
                actionW = g.EnableEyeTracking ? 1f : 0f,
                loop = g.Loop
            });
            _ = _pipe.SendCommand("stop_preview");
            PanelPresetEdit.Children.Clear();
            PanelPresetEdit.Children.Add(new TextBlock { Text = "已保存并生效", Foreground = Res("Accent"), FontSize = 13 });
        };
        saveRow.Children.Add(btnSave);
        sp.Children.Add(saveRow);
    }

    #endregion

    #region Facial Preset Tab Events

    private void OnFacialPresetSelect(object sender, SelectionChangedEventArgs e) { }

    private void OnFacialPresetEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name && _initData != null)
        {
            _facialEditing = _initData.FacialPresets.FirstOrDefault(p => p.PresetName == name);
            if (_facialEditing != null) BuildFacialEditPanel();
        }
    }

    private void BuildFacialEditPanel()
    {
        PanelFacialEdit.Children.Clear();
        if (_facialEditing == null) return;
        var sp = PanelFacialEdit;
        var p = _facialEditing;

        sp.Children.Add(new TextBlock { Text = p.PresetName, FontSize = 16, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });

        // Preview + Save buttons
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var btnPreview = new Button { Content = "预览", Width = 60, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(0, 0, 8, 0) };
        btnPreview.Click += async (_, _) =>
        {
            var targetsJson = System.Text.Json.JsonSerializer.Serialize(p.Targets.Select(t => new { index = t.Index, weight = t.Weight }));
            await _pipe.SendCommand("preview_facial", new { facialX = p.PresetName, facialW = 1f, targetsJson });
        };
        btnRow.Children.Add(btnPreview);

        var btnSave = new Button { Content = "保存", Width = 60, Style = (Style)FindResource("SmallButton") };
        btnSave.Click += async (_, _) =>
        {
            var targetsJson = JsonSerializer.Serialize(p.Targets.Select(t => new { index = t.Index, weight = t.Weight }));
            await _pipe.SendCommand("update_facial_preset", new
            {
                name = p.PresetName,
                targetsJson,
                blushMode = p.BlushMode ?? ""
            });
            MessageBox.Show($"表情预设 '{p.PresetName}' 已保存。", "保存成功", MessageBoxButton.OK, MessageBoxImage.Information);
        };
        btnRow.Children.Add(btnSave);
        sp.Children.Add(btnRow);

        // Blend shape targets
        sp.Children.Add(new TextBlock { Text = "BlendShape 目标:", Foreground = Res("TextSecondary"), Margin = new Thickness(0, 4, 0, 4) });

        for (int i = 0; i < p.Targets.Count; i++)
        {
            var t = p.Targets[i];
            int idx = i;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

            var combo = new ComboBox { Width = 160, VerticalAlignment = VerticalAlignment.Center };
            if (_initData?.BlendShapeNames != null)
            {
                foreach (var name in _initData.BlendShapeNames)
                    combo.Items.Add(name);
            }
            if (t.Index >= 0 && t.Index < combo.Items.Count)
                combo.SelectedIndex = t.Index;
            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedIndex >= 0) p.Targets[idx].Index = combo.SelectedIndex;
            };
            row.Children.Add(combo);

            var slider = new Slider { Width = 120, Minimum = 0, Maximum = 100, Value = t.Weight, Margin = new Thickness(8, 0, 0, 0) };
            var lbl = new TextBlock { Text = t.Weight.ToString("F0"), Width = 36, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) };
            slider.ValueChanged += (_, ev) => { p.Targets[idx].Weight = (float)ev.NewValue; lbl.Text = ev.NewValue.ToString("F0"); };
            row.Children.Add(slider);
            row.Children.Add(lbl);

            var btnDel = new Button { Content = "✕", Width = 28, Height = 24, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(6, 0, 0, 0) };
            btnDel.Click += (_, _) => { p.Targets.RemoveAt(idx); BuildFacialEditPanel(); };
            row.Children.Add(btnDel);

            sp.Children.Add(row);
        }

        var btnAdd = new Button { Content = "＋ 添加 BlendShape", Margin = new Thickness(0, 6, 0, 0) };
        btnAdd.Click += (_, _) => { p.Targets.Add(new BlendShapeTargetEntry()); BuildFacialEditPanel(); };
        sp.Children.Add(btnAdd);

        // Effect objects
        if (p.ActivateObjects.Count > 0)
        {
            sp.Children.Add(new TextBlock { Text = "特效: " + string.Join(", ", p.ActivateObjects), Foreground = Res("TextSecondary"), Margin = new Thickness(0, 8, 0, 0), FontSize = 11 });
        }
        if (!string.IsNullOrEmpty(p.BlushMode))
        {
            sp.Children.Add(new TextBlock { Text = "腮红: " + p.BlushMode, Foreground = Res("TextSecondary"), FontSize = 11 });
        }

        // Stop preview
        var btnStop = new Button { Content = "停止预览", Width = 80, Margin = new Thickness(0, 12, 0, 0) };
        btnStop.Click += (_, _) => _ = _pipe.SendCommand("stop_preview");
        sp.Children.Add(btnStop);
    }

    #endregion

    #region History Tab Events
    private void OnHistoryClearClick(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("clear_history");
    #endregion

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is TabItem)
            _ = _pipe.SendCommand("stop_preview");
    }

    private System.Windows.Media.Brush Res(string name) => (System.Windows.Media.Brush)FindResource(name);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_COPYDATA)
        {
            var data = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
            if (data.dwData == (IntPtr)1)
            {
                var tabStr = Marshal.PtrToStringUni(data.lpData, data.cbData / 2);
                if (int.TryParse(tabStr, out int ti) && ti >= 0 && ti < MainTabs.Items.Count)
                {
                    MainTabs.SelectedIndex = ti;
                    Activate();
                }
            }
            handled = true;
        }
        return IntPtr.Zero;
    }

    #region Eye Tab Events
    private void PopulateEyeTab()
    {
        var names = _initData?.BlendShapeNames ?? new();
        var combos = new[] { CmbEyeBlink, CmbEyeLookL, CmbEyeLookR, CmbEyeLookU, CmbEyeLookD };
        foreach (var cmb in combos)
        {
            cmb.Items.Clear();
            cmb.Items.Add("-- 无 --");
            foreach (var n in names) cmb.Items.Add(n);
        }

        var ep = _initData?.EyeProfile;
        if (ep != null)
        {
            SldEyeStrength.Value = ep.LookStrength;
            LblEyeStrength.Text = ep.LookStrength.ToString("F0");
            SldEyeHeadRot.Value = ep.HeadRotationAmount;
            LblEyeHeadRot.Text = ep.HeadRotationAmount.ToString("F0");
        }
        SelectEyeIndex(CmbEyeBlink, ep?.BlinkIndex ?? -1);
        SelectEyeIndex(CmbEyeLookL, ep?.LookLeftIndex ?? -1);
        SelectEyeIndex(CmbEyeLookR, ep?.LookRightIndex ?? -1);
        SelectEyeIndex(CmbEyeLookU, ep?.LookUpIndex ?? -1);
        SelectEyeIndex(CmbEyeLookD, ep?.LookDownIndex ?? -1);
    }

    private static void SelectEyeIndex(ComboBox cmb, int idx)
    {
        cmb.SelectedIndex = idx >= 0 && idx + 1 < cmb.Items.Count ? idx + 1 : 0;
    }

    private static int GetEyeIndex(ComboBox cmb) => cmb.SelectedIndex > 0 ? cmb.SelectedIndex - 1 : -1;

    private void OnEyePreview(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string ?? "";
        int snd(int v, string dir) => tag == dir ? v : -1;
        _ = _pipe.SendCommand("preview_eye", new
        {
            eyeBlinkIdx = snd(GetEyeIndex(CmbEyeBlink), "blink"),
            eyeLookL = snd(GetEyeIndex(CmbEyeLookL), "left"),
            eyeLookR = snd(GetEyeIndex(CmbEyeLookR), "right"),
            eyeLookU = snd(GetEyeIndex(CmbEyeLookU), "up"),
            eyeLookD = snd(GetEyeIndex(CmbEyeLookD), "down"),
            eyeStrength = (float)SldEyeStrength.Value, eyeHeadRot = (float)SldEyeHeadRot.Value
        });
    }

    private void OnEyeProfileSave(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("update_eye_profile", new
        {
            eyeBlinkIdx = GetEyeIndex(CmbEyeBlink), eyeLookL = GetEyeIndex(CmbEyeLookL),
            eyeLookR = GetEyeIndex(CmbEyeLookR), eyeLookU = GetEyeIndex(CmbEyeLookU),
            eyeLookD = GetEyeIndex(CmbEyeLookD), eyeStrength = (float)SldEyeStrength.Value, eyeHeadRot = (float)SldEyeHeadRot.Value
        });
    }

    private void OnEyeAutoDetect(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("auto_detect_eyes");
    #endregion
}
