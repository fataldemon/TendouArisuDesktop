using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace AliceBotSettings;

public partial class MainWindow : Window
{
    private readonly PipeClient _pipe = new();
    private InitData? _initData;
    private int _ttsMode;
    private string _animCatFilter = "All";
    private ExpressionMappingEntry? _exprEditing;
    private ActionPresetEntry? _presetEditing;

    public MainWindow()
    {
        InitializeComponent();
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
        Dispatcher.Invoke(() => PopulateAll(data));
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
        TxtIdentity.Text = d.Identity;
        TxtPreset.Text = d.Preset;
        UpdateConnectionStatus(d.Connected);
        PopulateModelHistory(d.ModelHistory);
        PopulateAnimationList(d.AnimationList);
        PopulateExpressionList(d.ExpressionMappings);
        PopulatePresetList(d.ActionPresets);
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
                Content = cat,
                Width = 70,
                Style = (Style)FindResource("SmallButton"),
                Margin = new Thickness(0, 0, 4, 0)
            };
            btn.Tag = cat;
            if (cat == _animCatFilter) btn.IsEnabled = false;
            btn.Click += (_, _) =>
            {
                _animCatFilter = cat;
                PopulateAnimationList(list);
            };
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
            EmotionDisplay = m.Emotion == "待机" ? "★ 待机" : m.Emotion,
            FacialSummary = m.FacialGroup?.Preset ?? "-",
            ActionSummary = m.ActionGroup?.AnimationName ?? "-",
            IsIdle = m.Emotion == "待机"
        }).ToList();
        LstExprMappings.ItemsSource = displayList;
    }

    #region Connection Tab Events

    private void OnConnectClick(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("connect");
    }

    private void OnDisconnectClick(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("disconnect");
    }

    private void OnTtsModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int mode))
        {
            _ttsMode = mode;
            SetTtsModeButtons(mode);
            LblTtsUrl.Content = mode switch
            {
                0 => "Gradio API 地址",
                1 => "Simple-Vits API 地址",
                _ => "API 地址"
            };
            _ = _pipe.SendCommand("update_config", new
            {
                ttsMode = mode,
                ttsUrl = TxtTtsUrl.Text
            });
        }
    }

    private void SetTtsModeButtons(int mode)
    {
        _ttsMode = mode;
        BtnTtsGradio.IsEnabled = mode != 0;
        BtnTtsSimpleVits.IsEnabled = mode != 1;
        BtnTtsNone.IsEnabled = mode != 2;
        LblTtsUrl.Content = mode switch
        {
            0 => "Gradio API 地址",
            1 => "Simple-Vits API 地址",
            _ => "API 地址"
        };
    }

    private void OnTtsTestClick(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("test_tts", new { text = TxtTtsTestLine.Text });
    }

    private void OnTtsSendClick(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("test_tts", new { text = TxtTtsTestLine.Text });
    }

    #endregion

    #region Dialog Settings Tab Events

    private void OnDialogSettingsSave(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("update_dialog", new
        {
            identity = TxtIdentity.Text,
            preset = TxtPreset.Text
        });
    }

    private void OnDialogSettingsCancel(object sender, RoutedEventArgs e)
    {
        if (_initData != null)
        {
            TxtIdentity.Text = _initData.Identity;
            TxtPreset.Text = _initData.Preset;
        }
    }

    #endregion

    #region Model Tab Events

    private void OnModelBrowseClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "VRM Files|*.vrm",
            Title = "选择 VRM 模型文件"
        };
        if (dlg.ShowDialog() == true)
        {
            TxtVrmPath.Text = dlg.FileName;
        }
    }

    private void OnModelLoadClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(TxtVrmPath.Text))
            _ = _pipe.SendCommand("load_model", new { path = TxtVrmPath.Text });
    }

    private void OnModelRestoreClick(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("restore_default_model");
    }

    private void OnModelHistorySelect(object sender, SelectionChangedEventArgs e)
    {
        if (LstModelHistory.SelectedIndex >= 0)
            _ = _pipe.SendCommand("load_model_from_history", new { index = LstModelHistory.SelectedIndex });
    }

    #endregion

    #region Animation Tab Events

    private void OnAnimRefreshClick(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("scan_animations");
    }

    private void OnAnimImportClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "FBX Files|*.fbx",
            Title = "导入动画"
        };
        if (dlg.ShowDialog() == true)
            _ = _pipe.SendCommand("import_animation", new { path = dlg.FileName });
    }

    private void OnRootMotionChanged(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("set_root_motion", new { enable = ChkRootMotion.IsChecked == true });
    }

    private void OnAnimPreviewClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
            _ = _pipe.SendCommand("preview_animation", new { name });
    }

    #endregion

    #region Expression Tab Events

    private void OnExprRestoreDefaults(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("restore_default_mappings");
    }

    private void OnExprAdd(object sender, RoutedEventArgs e)
    {
        _exprEditing = new ExpressionMappingEntry
        {
            Emotion = "",
            FacialGroup = new FacialGroupEntry { Preset = "happy", Weight = 1f },
            ActionGroup = new ActionGroupEntry { AnimationName = "0", BodyPart = "fullBody", Weight = 1f }
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
            if (emotion == "待机")
            {
                MessageBox.Show("待机映射不可删除", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _ = _pipe.SendCommand("delete_expression_mapping", new { emotion });
        }
    }

    private void OnExprMappingSelect(object sender, SelectionChangedEventArgs e)
    {
        // Allow re-selection for edit
    }

    private static readonly string[] FacialPresets = { "angry", "serious", "happy", "fun", "panic", "curious", "thinking", "disappointed", "sweating", "confident", "cry", "plain", "shy", "touching", "wink" };
    private static readonly string[] BodyParts = { "fullBody", "upperBody", "lowerBody", "head", "leftArm", "rightArm", "leftLeg", "rightLeg", "hands" };

    private void BuildExprEditPanel()
    {
        PanelExprEdit.Children.Clear();
        if (_exprEditing == null) return;

        var sp = PanelExprEdit;

        // Emotion name
        var emoPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        emoPanel.Children.Add(new TextBlock { Text = "情绪:", Foreground = FindResource("TextSecondary") as System.Windows.Media.Brush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var txtEmotion = new TextBox { Width = 120, Text = _exprEditing.Emotion };
        txtEmotion.TextChanged += (_, _) => _exprEditing.Emotion = txtEmotion.Text;
        emoPanel.Children.Add(txtEmotion);
        var btnPreviewAll = new Button { Content = "预览", Width = 60, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(8, 0, 0, 0) };
        btnPreviewAll.Click += (_, _) =>
        {
            _ = _pipe.SendCommand("preview_expression", new { emotion = _exprEditing.Emotion });
            if (_exprEditing.ActionGroup != null)
                _ = _pipe.SendCommand("preview_action", new { name = _exprEditing.ActionGroup.AnimationName });
        };
        emoPanel.Children.Add(btnPreviewAll);
        sp.Children.Add(emoPanel);

        // Facial section
        var fg = _exprEditing.FacialGroup ?? new FacialGroupEntry();
        _exprEditing.FacialGroup = fg;

        var fgHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 4) };
        fgHeader.Children.Add(new TextBlock { Text = "面部表情:", Foreground = FindResource("TextSecondary") as System.Windows.Media.Brush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var btnFgPreview = new Button { Content = "预览", Width = 50, Style = (Style)FindResource("SmallButton") };
        btnFgPreview.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(fg.Preset))
                _ = _pipe.SendCommand("preview_facial", new { facialX = fg.Preset, facialW = fg.Weight });
        };
        fgHeader.Children.Add(btnFgPreview);
        sp.Children.Add(fgHeader);
        sp.Children.Add(BuildFacialPresetSelector(fg));
        sp.Children.Add(BuildWeightSlider(fg.Weight, w => { fg.Weight = w; }));

        // Action section
        var ag = _exprEditing.ActionGroup ?? new ActionGroupEntry();
        _exprEditing.ActionGroup = ag;
        var agHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 4) };
        agHeader.Children.Add(new TextBlock { Text = "动作:", Foreground = FindResource("TextSecondary") as System.Windows.Media.Brush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var btnAgPreview = new Button { Content = "预览", Width = 50, Style = (Style)FindResource("SmallButton") };
        btnAgPreview.Click += (_, _) => _ = _pipe.SendCommand("preview_action", new { name = ag.AnimationName });
        agHeader.Children.Add(btnAgPreview);
        sp.Children.Add(agHeader);
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        var cboAction = new ComboBox { Width = 140 };
        if (_initData?.ActionPresets != null)
            foreach (var p in _initData.ActionPresets.OrderBy(p => p.Name)) cboAction.Items.Add(p.Name);
        foreach (var item in cboAction.Items) { if (item.ToString() == ag.AnimationName) cboAction.SelectedItem = item; }
        cboAction.SelectionChanged += (_, _) => { if (cboAction.SelectedItem != null) ag.AnimationName = cboAction.SelectedItem.ToString()!; };
        nameRow.Children.Add(cboAction);
        sp.Children.Add(nameRow);
        sp.Children.Add(BuildBodyPartSelector(ag));
        sp.Children.Add(BuildWeightSlider(ag.Weight, w => { ag.Weight = w; }));

        // Save / Cancel
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var btnSave = new Button { Content = "保存", Width = 70, Style = (Style)FindResource("PrimaryButton") };
        btnSave.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(_exprEditing.Emotion))
            {
                MessageBox.Show("请输入情绪名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _ = _pipe.SendCommand("update_expression_mapping", new
            {
                emotion = _exprEditing.Emotion,
                facialX = fg.Preset,
                facialW = fg.Weight,
                actionX = ag.AnimationName,
                actionP = ag.BodyPart,
                actionY = ag.Weight
            });
            _ = _pipe.SendCommand("restore_expression");
            _exprEditing = null;
            PanelExprEdit.Children.Clear();
            PanelExprEdit.Children.Add(new TextBlock { Text = "已保存", Foreground = (System.Windows.Media.Brush)FindResource("Accent"), FontSize = 13 });
        };
        actionRow.Children.Add(btnSave);
        var btnCancel = new Button { Content = "取消", Width = 70, Margin = new Thickness(8, 0, 0, 0) };
        btnCancel.Click += (_, _) =>
        {
            _ = _pipe.SendCommand("restore_expression");
            _exprEditing = null;
            PanelExprEdit.Children.Clear();
            PanelExprEdit.Children.Add(new TextBlock { Text = "选择或添加一个映射进行编辑", Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary"), FontSize = 13 });
        };
        actionRow.Children.Add(btnCancel);
        sp.Children.Add(actionRow);
    }

    private FrameworkElement BuildFacialPresetSelector(FacialGroupEntry fg)
    {
        var sp = new WrapPanel { Margin = new Thickness(0, 2, 0, 4) };
        sp.Children.Add(new TextBlock { Text = "预设:", Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        int sel = Math.Max(0, Array.IndexOf(FacialPresets, fg.Preset));
        for (int k = 0; k < FacialPresets.Length; k++)
        {
            var btn = new Button { Content = FacialPresets[k], Width = 70, Style = (Style)FindResource("SmallButton"), FontSize = 10, Tag = k, Margin = new Thickness(0, 0, 2, 2) };
            btn.IsEnabled = k != sel;
            int captured = k;
            btn.Click += (_, _) => { fg.Preset = FacialPresets[captured]; BuildExprEditPanel(); };
            sp.Children.Add(btn);
        }
        return sp;
    }

    private FrameworkElement BuildBodyPartSelector(ActionGroupEntry ag)
    {
        var sp = new WrapPanel { Margin = new Thickness(0, 2, 0, 4) };
        sp.Children.Add(new TextBlock { Text = "部位:", Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
        int sel = Math.Max(0, Array.IndexOf(BodyParts, ag.BodyPart));
        for (int k = 0; k < BodyParts.Length; k++)
        {
            var label = BodyParts[k].Length > 9 ? BodyParts[k][..9] : BodyParts[k];
            var btn = new Button { Content = label, Width = 62, Style = (Style)FindResource("SmallButton"), FontSize = 10, Tag = k, Margin = new Thickness(0, 0, 2, 2) };
            btn.IsEnabled = k != sel;
            int captured = k;
            btn.Click += (_, _) => { ag.BodyPart = BodyParts[captured]; BuildExprEditPanel(); };
            sp.Children.Add(btn);
        }
        return sp;
    }

    private FrameworkElement BuildWeightSlider(float initial, Action<float> onChange)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        var txt = new TextBlock { Text = "权重:", Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) };
        var slider = new Slider { Width = 140, Minimum = 0, Maximum = 1, Value = initial, SmallChange = 0.05, TickFrequency = 0.1 };
        var lbl = new TextBlock { Text = initial.ToString("F1"), Foreground = (System.Windows.Media.Brush)FindResource("TextPrimary"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        slider.ValueChanged += (_, e) => { lbl.Text = e.NewValue.ToString("F1"); onChange((float)e.NewValue); };
        sp.Children.Add(txt);
        sp.Children.Add(slider);
        sp.Children.Add(lbl);
        return sp;
    }

    #endregion

    #region Action Preset Tab Events

    private void PopulatePresetList(List<ActionPresetEntry> presets)
    {
        var displayList = presets.OrderBy(p => p.ActionParam).Select(p => new
        {
            p.Name, p.ActionParam, p.IsDefault,
            DisplayName = (p.IsDefault ? "★ " : "") + p.Name,
            Summary = "param=" + p.ActionParam
        }).ToList();
        LstPresets.ItemsSource = displayList;
    }

    private void OnPresetRestoreDefaults(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("restore_default_presets");
    }

    private void OnPresetAdd(object sender, RoutedEventArgs e)
    {
        _presetEditing = new ActionPresetEntry { Name = "", ActionParam = 0 };
        BuildPresetEditPanel();
    }

    private void OnPresetSelect(object sender, SelectionChangedEventArgs e) { }

    private void OnPresetEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name && _initData != null)
        {
            _presetEditing = _initData.ActionPresets.FirstOrDefault(p => p.Name == name);
            if (_presetEditing != null) BuildPresetEditPanel();
        }
    }

    private void OnPresetDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string name)
            _ = _pipe.SendCommand("delete_action_preset", new { name });
    }

    private void OnPresetPreviewClick(object sender, RoutedEventArgs e)
    {
        if (_presetEditing != null)
            _ = _pipe.SendCommand("preview_action", new { name = _presetEditing.Name });
    }

    private void BuildPresetEditPanel()
    {
        PanelPresetEdit.Children.Clear();
        if (_presetEditing == null) return;
        var p = _presetEditing;
        var sp = PanelPresetEdit;

        // Name
        var nameRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        nameRow.Children.Add(new TextBlock { Text = "名称:", Foreground = FindResource("TextSecondary") as System.Windows.Media.Brush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var txtName = new TextBox { Width = 160, Text = p.Name, IsReadOnly = p.IsDefault };
        txtName.TextChanged += (_, _) => p.Name = txtName.Text;
        nameRow.Children.Add(txtName);
        var btnPreview = new Button { Content = "预览", Width = 50, Style = (Style)FindResource("SmallButton"), Margin = new Thickness(8, 0, 0, 0) };
        btnPreview.Click += OnPresetPreviewClick;
        nameRow.Children.Add(btnPreview);
        sp.Children.Add(nameRow);

        // ActionParam
        var apRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        apRow.Children.Add(new TextBlock { Text = "ActionParam:", Foreground = FindResource("TextSecondary") as System.Windows.Media.Brush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var txtAp = new TextBox { Width = 60, Text = p.ActionParam.ToString(), IsReadOnly = p.IsDefault };
        txtAp.TextChanged += (_, _) => { if (int.TryParse(txtAp.Text, out int v)) p.ActionParam = v; };
        apRow.Children.Add(txtAp);
        sp.Children.Add(apRow);

        // Save / Cancel
        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var btnSave = new Button { Content = "保存", Width = 70, Style = (Style)FindResource("PrimaryButton") };
        btnSave.Click += (_, _) =>
        {
            if (string.IsNullOrEmpty(p.Name))
            {
                MessageBox.Show("请输入预设名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            _ = _pipe.SendCommand("save_action_preset", new { name = p.Name, actionParam = p.ActionParam });
            _presetEditing = null;
            PanelPresetEdit.Children.Clear();
            PanelPresetEdit.Children.Add(new TextBlock { Text = "已保存", Foreground = (System.Windows.Media.Brush)FindResource("Accent"), FontSize = 13 });
        };
        actionRow.Children.Add(btnSave);
        var btnCancel = new Button { Content = "取消", Width = 70, Margin = new Thickness(8, 0, 0, 0) };
        btnCancel.Click += (_, _) =>
        {
            _presetEditing = null;
            PanelPresetEdit.Children.Clear();
            PanelPresetEdit.Children.Add(new TextBlock { Text = "选择一个预设进行编辑", Foreground = (System.Windows.Media.Brush)FindResource("TextSecondary"), FontSize = 13 });
        };
        actionRow.Children.Add(btnCancel);
        sp.Children.Add(actionRow);
    }

    #endregion

    #region History Tab Events

    private void OnHistoryClearClick(object sender, RoutedEventArgs e)
    {
        _ = _pipe.SendCommand("clear_history");
    }

    #endregion

    private void OnTabChanged(object sender, SelectionChangedEventArgs e)
    {
        // Restore state when leaving tabs
        if (e.RemovedItems.Count > 0 && e.RemovedItems[0] is TabItem removed)
        {
            var header = removed.Header as string;
            if (header == "动画库")
                _ = _pipe.SendCommand("stop_preview");
            else if (header == "表情映射")
                _ = _pipe.SendCommand("restore_expression");
        }
    }
}
