using Microsoft.Extensions.DependencyInjection;
using MmmSdk.Core.Components.Features.Json;
using MmmSdk.Core.Components.Settings;

namespace MmmSdk.Core.Components.Features;

/// <summary>機能のオン・オフの管理 (状態の保存・起動時の準備の実行・切り替えの通知)</summary>
/// <param name="features">オン・オフを切り替えられる機能</param>
/// <param name="confirmations">オフにする前の確認</param>
/// <param name="startups">起動時の準備</param>
/// <param name="settings">汎用設定ストア</param>
/// <param name="services">起動時の準備を作る DI のサービスプロバイダー</param>
/// <remarks>
/// 状態は設定ストアに、機能のキーごとに保存する。保存が無い機能は、登録の初期値 (既定はオン)。
/// 切り替えはすぐ反映する。画面 (サイドバー・メニュー・設定の画面など)は、<see cref="Changed"/> または <see cref="IsEnabled"/> で見る。
/// 機能の登録 (<see cref="FeatureServiceCollectionExtensions.AddFeature"/> 等)は各機能が行い、このクラスは特定の機能を知らない。
/// </remarks>
public sealed class FeatureService(
    IEnumerable<FeatureInfo> features,
    IEnumerable<IFeatureDisableConfirmation> confirmations,
    IEnumerable<StartupTaskRegistration> startups,
    ISettingsStore settings,
    IServiceProvider services) : IFeatureStatus
{
    /// <summary>登録された機能 (登録の並び順の値の順。同じ値は登録順)</summary>
    private readonly IReadOnlyList<FeatureInfo> _registered = [.. features.OrderBy(f => f.Order)];

    /// <summary>機能 (オフにできない機能も含む)。利用者が決めた並び順の順</summary>
    /// <remarks>保存した並び (<see cref="SetOrderAsync"/>)に載っている機能が先、載っていない機能 (あとから足したもの)は、登録の並び順の値の順で、その後ろ。</remarks>
    public IReadOnlyList<FeatureInfo> Features
    {
        get
        {
            var saved = settings.Get(OrderKey, [], FeatureJsonContext.Readable.StringArray);
            return [.. saved.Distinct().Select(key => _registered.FirstOrDefault(f => f.Key == key)).OfType<FeatureInfo>()
                .Concat(_registered.Where(f => !saved.Contains(f.Key)))];
        }
    }

    /// <summary>機能のオン・オフが切り替わったとき (引数は、切り替わった機能のキー)</summary>
    public event EventHandler<string>? Changed;

    /// <summary>機能の並び順が変わったとき (並べ替え・並び順を戻したとき)</summary>
    public event EventHandler? OrderChanged;

    /// <summary>機能がオンか</summary>
    /// <param name="featureKey">機能のキー。null (どの機能にも属さない)・オフにできない機能なら常にオン</param>
    /// <returns>オンなら true。保存が無いときは、登録の初期値 (<see cref="FeatureInfo.DefaultEnabled"/>)</returns>
    public bool IsEnabled(string? featureKey)
    {
        if (featureKey is null)
        {
            return true;
        }

        var info = _registered.FirstOrDefault(f => f.Key == featureKey);
        return info is { CanDisable: false } || settings.Get(SettingKey(featureKey), info?.DefaultEnabled ?? true);
    }

    /// <summary>画面の並びに使う値を取得する (サイドバー・設定ページの部品)</summary>
    /// <param name="featureKey">属する機能のキー。null・登録の無いキーのときは <paramref name="registeredOrder"/> のまま</param>
    /// <param name="registeredOrder">登録時の並び順の値</param>
    /// <returns>機能のキーがあれば、利用者が決めた並び (<see cref="Features"/>)の位置。無ければ登録時の値</returns>
    /// <remarks>機能に属さない設定の部品は負の値で先頭に固定されているので、0 以上の位置と混ざらない。</remarks>
    public int OrderOf(string? featureKey, int registeredOrder)
    {
        if (featureKey is null)
        {
            return registeredOrder;
        }

        var index = Features.ToList().FindIndex(f => f.Key == featureKey);
        return index < 0 ? registeredOrder : index;
    }

    /// <summary>機能の並び順を保存する</summary>
    /// <param name="keys">機能のキーを、新しい並び順で並べたもの</param>
    /// <returns>保存したら true。読み取り専用 (読み込みに失敗している)ため保存しなかったら false</returns>
    /// <remarks>保存してから <see cref="OrderChanged"/> を出す。</remarks>
    public async Task<bool> SetOrderAsync(IReadOnlyList<string> keys)
    {
        if (!await settings.SetAsync(OrderKey, [.. keys], FeatureJsonContext.Readable.StringArray))
        {
            return false;
        }

        OrderChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>機能の並び順を、登録時の並び (<see cref="FeatureInfo.Order"/>の順)に戻す</summary>
    /// <returns>戻したら true (保存した並びが無く、すでに登録の順のときも true)。読み取り専用のため保存しなかったら false</returns>
    public async Task<bool> ResetOrderAsync()
    {
        if (settings.Contains(OrderKey) && !await settings.RemoveAsync(OrderKey))
        {
            return false;
        }

        OrderChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>オンの機能の起動時の準備を、登録順に実行する</summary>
    /// <returns>準備の完了を表すタスク</returns>
    /// <remarks>
    /// 呼ぶスレッドで順に実行する。ホストは、設定ストアの先読みを最初の起動時の準備として登録しておく (
    /// オン・オフの判定を、読み込み済みの設定で行うため)。
    /// </remarks>
    public async Task StartAsync()
    {
        foreach (var startup in startups.Where(s => IsEnabled(s.FeatureKey)))
        {
            await RunAsync(startup);
        }
    }

    /// <summary>機能をオン・オフする</summary>
    /// <param name="featureKey">機能のキー</param>
    /// <param name="enabled">オンにするなら true</param>
    /// <returns>切り替えた結果</returns>
    /// <remarks>
    /// オフにするときは、先に確認 (<see cref="IFeatureDisableConfirmation"/>)を取る。
    /// オンにするときは、その機能の起動時の準備を実行してから <see cref="Changed"/> を出す。
    /// オフにするときは、保存してから <see cref="Changed"/> を出す (受け取った側がページを捨てる)。
    /// </remarks>
    public async Task<FeatureChangeResult> SetEnabledAsync(string featureKey, bool enabled)
    {
        if (IsEnabled(featureKey) == enabled)
        {
            return FeatureChangeResult.Changed;
        }

        if (!enabled
            && confirmations.FirstOrDefault(c => c.FeatureKey == featureKey) is { } confirmation
            && !await confirmation.ConfirmAsync())
        {
            return FeatureChangeResult.Cancelled;
        }

        if (!await settings.SetAsync(SettingKey(featureKey), enabled))
        {
            return FeatureChangeResult.NotSaved;
        }

        if (enabled)
        {
            foreach (var startup in startups.Where(s => s.FeatureKey == featureKey))
            {
                await RunAsync(startup);
            }
        }

        Changed?.Invoke(this, featureKey);
        return FeatureChangeResult.Changed;
    }

    /// <summary>起動時の準備を 1 つ実行する</summary>
    /// <param name="startup">起動時の準備の登録情報</param>
    /// <returns>準備の完了を表すタスク</returns>
    private Task RunAsync(StartupTaskRegistration startup)
        => ((IStartupTask)services.GetRequiredService(startup.TaskType)).StartAsync();

    /// <summary>機能の並び順 (機能のキーの配列)を保存する、設定ストアのキー</summary>
    private const string OrderKey = "Feature.Order";

    /// <summary>機能のオン・オフを保存する、設定ストアのキー</summary>
    /// <param name="featureKey">機能のキー</param>
    /// <returns>設定ストアのキー</returns>
    private static string SettingKey(string featureKey) => $"Feature.{featureKey}.Enabled";
}
