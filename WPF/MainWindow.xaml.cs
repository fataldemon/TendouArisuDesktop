using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
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
    private string _animCatFilter = "All";
    private ExpressionMappingEntry? _exprEditing;
    private ActionGroupFullEntry? _groupEditing;
    private FacialPresetEntry? _facialEditing;

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
        SetTtsModeButtons(d.TtsMode);
        TxtTtsUrl.Text = d.TtsMode == 0 ? d.GradioUrl : d.SimpleVitsUrl;
        TxtTranslationUrl.Text = d.TranslationUrl;
        TxtTranslationAppId.Text = d.TranslationAppId;
        TxtTranslationKey.Password = d.TranslationKey;
        TxtTranslationSalt.Text = d.TranslationSalt;
        TxtMsgWidth.Text = d.MsgMaxWidth.ToString();
        TxtMsgHeight.Text = d.MsgHeight.ToString();
        TxtDialogHold.Text = d.DialogMinHoldTime.ToString("F0");
        UpdateConnectionStatus(d.Connected);
        PopulateModelHistory(d.ModelHistory);
        PopulateAnimationList(d.AnimationList);
        PopulateExpressionList(d.ExpressionMappings);
        PopulateActionGroupList(d.ActionGroups);
        PopulateFacialPresetList(d.FacialPresets);
        TxtHistory.Text = d.DialogueHistory;
    }

    private void PopulateModelHistory(List<string> history)
    {
        LstModelHistory.ItemsSource = history.Select(h =>
        {
            var parts = h.Split('|');
            return parts.Length > 1 ? parts[1] : h;
        }).ToList();
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
            ActionSummary = !string.IsNullOrEmpty(m.ActionGroupName) ? m.ActionGroupName : (m.ActionGroup?.AnimationName ?? "-"),
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

    private void OnTtsModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int mode))
        {
            _ttsMode = mode;
            SetTtsModeButtons(mode);
            _ = _pipe.SendCommand("update_config", new { ttsMode = mode, ttsUrl = TxtTtsUrl.Text });
        }
    }

    private void SetTtsModeButtons(int mode)
    {
        _ttsMode = mode;
        BtnTtsGradio.IsEnabled = mode != 0;
        BtnTtsSimpleVits.IsEnabled = mode != 1;
        BtnTtsNone.IsEnabled = mode != 2;
        LblTtsUrl.Content = mode switch { 0 => "Gradio API 地址", 1 => "Simple-Vits API 地址", _ => "API 地址" };
    }

    private void OnTtsTestClick(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("test_tts", new { text = TxtTtsTestLine.Text });
    private void OnTtsSendClick(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("test_tts", new { text = TxtTtsTestLine.Text });

    #endregion

    #region Dialog Settings Tab Events
    private void OnDialogSettingsSave(object sender, RoutedEventArgs e)
    {
        float hold = 10f;
        float.TryParse(TxtDialogHold.Text, out hold);
        _ = _pipe.SendCommand("update_dialog", new { msgWidth = GetInt(TxtMsgWidth), msgHeight = GetInt(TxtMsgHeight), dialogHold = hold });
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
        if (!string.IsNullOrEmpty(TxtVrmPath.Text))
            _ = _pipe.SendCommand("load_model", new { path = TxtVrmPath.Text });
    }
    private void OnModelRestoreClick(object sender, RoutedEventArgs e) => _ = _pipe.SendCommand("restore_default_model");
    private void OnModelHistorySelect(object sender, SelectionChangedEventArgs e)
    {
        if (LstModelHistory.SelectedIndex >= 0)
            _ = _pipe.SendCommand("load_model_from_history", new { index = LstModelHistory.SelectedIndex });
    }
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

        // Emotion name
        var emoPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        emoPanel.Children.Add(new TextBlock { Text = "情绪:", Foreground = Res("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var txtEmotion = new TextBox { Width = 120, Text = entry.Emotion };
        txtEmotion.TextChanged += (_, _) => entry.Emotion = txtEmotion.Text;
        emoPanel.Children.Add(txtEmotion);
        var btnPreviewAll = new Button { Content = "预览", Width = 60, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(8, 0, 0, 0) };
        btnPreviewAll.Click += (_, _) =>
        {
            var group = _initData?.ActionGroups.FirstOrDefault(g => g.GroupName == entry.ActionGroupName);
            var clipParts = new List<string>();
            if (group != null)
                foreach (var c in group.BodyClips)
                    if (!string.IsNullOrEmpty(c.ClipName))
                        clipParts.Add(c.BodyPart + "=" + c.ClipName);
            string facial = !string.IsNullOrEmpty(entry.FacialOverride) ? entry.FacialOverride : (group?.FacialPreset ?? "");
            float facialW = entry.FacialWeightOverride > 0 ? entry.FacialWeightOverride : (group?.FacialWeight ?? 1f);
            _ = _pipe.SendCommand("preview_group_action", new
            {
                actionX = string.Join("|", clipParts),
                facialX = facial,
                facialW = facialW,
                actionY = group?.AllowRootMotion ?? false ? 1f : 0f,
                actionW = group?.EnableEyeTracking ?? false ? 1f : 0f
            });
        };
        emoPanel.Children.Add(btnPreviewAll);
        sp.Children.Add(emoPanel);

        // Action Group selector
        sp.Children.Add(new TextBlock { Text = "动作组:", Foreground = Res("TextSecondary"), Margin = new Thickness(0, 8, 0, 4) });
        var cboGroup = new ComboBox { Width = 180 };
        if (_initData?.ActionGroups != null)
            foreach (var g in _initData.ActionGroups.OrderBy(g => g.GroupName))
                cboGroup.Items.Add(g.GroupName);
        cboGroup.SelectedItem = entry.ActionGroupName;
        cboGroup.SelectionChanged += (_, _) => { if (cboGroup.SelectedItem != null) entry.ActionGroupName = cboGroup.SelectedItem.ToString()!; };
        sp.Children.Add(cboGroup);

        // Facial Override selector
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

        // Random event
        var rndRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
        var chkRnd = new CheckBox { Content = "随机事件", VerticalAlignment = VerticalAlignment.Center, IsChecked = entry.IsRandomEvent };
        chkRnd.Checked += (_, _) => entry.IsRandomEvent = true;
        chkRnd.Unchecked += (_, _) => entry.IsRandomEvent = false;
        rndRow.Children.Add(chkRnd);
        sp.Children.Add(rndRow);

        var btnPreviewFacial = new Button { Content = "预览表情", Width = 80, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(0, 4, 0, 0) };
        btnPreviewFacial.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(entry.FacialOverride))
                _ = _pipe.SendCommand("preview_facial", new { facialX = entry.FacialOverride, facialW = entry.FacialWeightOverride > 0 ? entry.FacialWeightOverride : 1f });
        };
        sp.Children.Add(btnPreviewFacial);

        // Save / Cancel
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var btnSave = new Button { Content = "保存", Width = 70, Style = (Style)FindResource("PrimaryButton") };
        btnSave.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(entry.Emotion)) { MessageBox.Show("请输入情绪名称"); return; }
            _ = _pipe.SendCommand("update_expression_mapping", new
            {
                emotion = entry.Emotion,
                actionX = entry.ActionGroupName,
                facialX = entry.FacialOverride ?? "",
                facialW = entry.FacialWeightOverride > 0 ? entry.FacialWeightOverride : 1f,
                isRandom = entry.IsRandomEvent
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
                actionW = g.EnableEyeTracking ? 1f : 0f
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
        btnPreview.Click += (_, _) => _ = _pipe.SendCommand("preview_facial", new { facialX = p.PresetName, facialW = 1f });
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
}
