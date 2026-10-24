using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace PulseCapsule.UI;

public sealed record HoneConnectionEdit(ProviderConfig Original, ProviderConfig Config, string NewSecret);

public partial class HoneConnectionEditor : UserControl
{
    public ProviderConfig Original { get; }
    public string ServiceName { get; }
    public event Action<HoneConnectionEditor>? ClearRequested;
    public event Action<HoneConnectionEditor>? RemoveRequested;

    public HoneConnectionEditor(ProviderConfig config, string name, string hint)
    {
        InitializeComponent();
        Original = config;
        ServiceName = name;
        ServiceTitle.Text = config.Type == ProviderType.HoneWallet ? name + " · 账户访问令牌" : name;
        AutomationProperties.SetName(this, name);
        AutomationProperties.SetAutomationId(this, "HoneConnection_" + config.Id);
        AutomationProperties.SetName(SecretInput, config.Type == ProviderType.HoneWallet ? name + " · 账户访问令牌" : name);
        SecretHint.Text = hint;
        ConnectionName.Text = config.Name;
        SecretEnvironment.Text = config.SecretEnvironment;
        RefreshMinutes.Text = config.RefreshMinutes.ToString(CultureInfo.InvariantCulture);
        LowThreshold.Text = config.LowThreshold.ToString(CultureInfo.InvariantCulture);
        UsageMultiplier.Text = config.UsageMultiplier.ToString(CultureInfo.InvariantCulture);
        BillingOptions.Visibility = config.Type == ProviderType.Hone ? Visibility.Visible : Visibility.Collapsed;
    }

    public HoneConnectionEdit Read(string baseUrl)
    {
        foreach (var field in new Control[] { ConnectionName, RefreshMinutes, LowThreshold, UsageMultiplier, SecretInput }) FieldFeedback.SetError(field, "");
        var name = ConnectionName.Text.Trim();
        if (name.Length == 0) throw Invalid(ConnectionName, "请填写连接名称。");
        if (!int.TryParse(RefreshMinutes.Text, out var interval) || interval is < 1 or > 1440)
            throw Invalid(RefreshMinutes, "刷新间隔须在 1–1440 分钟之间。");
        var threshold = Number(LowThreshold, "提醒金额");
        if (threshold < 0) throw Invalid(LowThreshold, "提醒金额不能小于 0。");
        var multiplier = Original.Type == ProviderType.Hone ? Number(UsageMultiplier, "换算倍数") : Original.UsageMultiplier;
        if (multiplier <= 0) throw Invalid(UsageMultiplier, "换算倍数必须大于 0。");
        return new(Original, Original with { Name = name, BaseUrl = baseUrl, RefreshMinutes = interval,
            LowThreshold = threshold, UsageMultiplier = multiplier, SecretEnvironment = SecretEnvironment.Text.Trim() }, SecretInput.Password.Trim());
    }

    public ProviderException RequireNewSecret() => Invalid(SecretInput, "接口地址已改变，请重新输入该地址对应的密钥。");

    public void CredentialCleared()
    {
        SecretInput.Clear();
        SecretHint.Text = "本机保存的密钥已清除；配置的环境变量仍可能提供密钥。";
    }

    private decimal Number(TextBox input, string label)
    {
        if (decimal.TryParse(input.Text.Trim(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result)) return result;
        throw Invalid(input, label + "应填写数字，小数点使用 .。");
    }

    private ProviderException Invalid(Control input, string message)
    {
        Advanced.IsExpanded = true;
        return FieldFeedback.Invalid(input, ServiceName + "：" + message);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ClearRequested?.Invoke(this);
    private void Remove_Click(object sender, RoutedEventArgs e) => RemoveRequested?.Invoke(this);
}
