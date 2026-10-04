using System.Diagnostics.CodeAnalysis;
using Knmdb.TrackAndTrace;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Подключение SDK KNMDB (Track and Trace) к приложению.
/// </summary>
/// <remarks>
/// <para>
/// Это основной способ использования SDK: один вызов регистрирует <see cref="KnddbApiClient"/>
/// с настроенным <see cref="HttpClient"/> и управляемой продолжительностью жизни соединений.
/// </para>
/// <para>
/// Работает в ASP.NET Core, .NET MAUI, WPF, WinForms, Avalonia, консольных приложениях
/// и фоновых службах — дополнительные пакеты подключать не нужно.
/// </para>
/// </remarks>
public static class KnddbServiceCollectionExtensions
{
    /// <summary>
    /// Подключает <see cref="KnddbApiClient"/> и читает настройки из конфигурации.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configuration">Корень конфигурации приложения.</param>
    /// <param name="sectionName">Имя раздела конфигурации. По умолчанию — <c>Knddb</c>.</param>
    /// <returns>Та же коллекция сервисов.</returns>
    /// <exception cref="ArgumentNullException">Не заданы <paramref name="services"/> или <paramref name="configuration"/>.</exception>
    /// <remarks>
    /// <para>Пример <c>appsettings.json</c>:</para>
    /// <code language="json">
    /// {
    ///   "Knddb": {
    ///     "Environment": "Production",
    ///     "Timeout": "00:02:00",
    ///     "UserAgent": "MyApp/1.0"
    ///   }
    /// }
    /// </code>
    /// <para>
    /// Допустимые значения <c>Environment</c>: <c>Test</c> (тестовый контур,
    /// <c>https://testndbapi.med.kg/</c>) и <c>Production</c> (боевой контур,
    /// <c>https://ndbapi.med.kg/</c>).
    /// </para>
    /// <para>
    /// Роли API настраиваются администратором департамента лекарственных средств через
    /// административную панель KNMDB: интеграции достаточно логина и пароля, проверять
    /// права не нужно.
    /// </para>
    /// <para>
    /// Перегрузка помечена как несовместимая с обрезкой кода и Native AOT: привязка
    /// произвольных типов к конфигурации использует рефлексию. Если приложение
    /// собирается с <c>PublishTrimmed</c> или <c>PublishAot</c>, настройте параметры кодом —
    /// перегрузкой с делегатом <see cref="Action{T}"/>.
    /// </para>
    /// </remarks>
    [RequiresUnreferencedCode("Привязка KnddbClientOptions к конфигурации использует рефлексию. Для обрезки кода и Native AOT используйте перегрузку с делегатом настройки.")]
    [RequiresDynamicCode("Привязка KnddbClientOptions к конфигурации может требовать генерации кода во время выполнения. Для Native AOT используйте перегрузку с делегатом настройки.")]
    public static IServiceCollection AddKnddbTrackAndTrace(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = KnddbClientOptions.DefaultSectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(sectionName);

        // Значения читаются с явным указанием типов: привязка к перечислению и к Uri
        // не всегда получается при стандартном Bind, поэтому выполняется вручную.
        var environment = section.GetValue<KnddbEnvironment?>(nameof(KnddbClientOptions.Environment));
        var baseAddress = section.GetValue<string>(nameof(KnddbClientOptions.BaseAddress));
        var timeout = section.GetValue<TimeSpan?>(nameof(KnddbClientOptions.Timeout));
        var tokenExpirationMargin = section.GetValue<TimeSpan?>(nameof(KnddbClientOptions.TokenExpirationMargin));
        var userAgent = section.GetValue<string>(nameof(KnddbClientOptions.UserAgent));
        var defaultSessionKey = section.GetValue<string>(nameof(KnddbClientOptions.DefaultSessionKey));
        var httpClientName = section.GetValue<string>(nameof(KnddbClientOptions.HttpClientName));

        return services.AddKnddbTrackAndTrace(options =>
        {
            if (environment is not null)
            {
                options.Environment = environment.Value;
            }

            if (!string.IsNullOrWhiteSpace(baseAddress))
            {
                options.BaseAddress = new Uri(baseAddress, UriKind.Absolute);
            }

            if (timeout is not null)
            {
                options.Timeout = timeout.Value;
            }

            if (tokenExpirationMargin is not null)
            {
                options.TokenExpirationMargin = tokenExpirationMargin.Value;
            }

            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                options.UserAgent = userAgent;
            }

            if (!string.IsNullOrWhiteSpace(defaultSessionKey))
            {
                options.DefaultSessionKey = defaultSessionKey;
            }

            if (!string.IsNullOrWhiteSpace(httpClientName))
            {
                options.HttpClientName = httpClientName;
            }
        });
    }

    /// <summary>
    /// Подключает <see cref="KnddbApiClient"/> с настройкой кодом.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configure">Делегат настройки параметров.</param>
    /// <returns>Та же коллекция сервисов.</returns>
    /// <exception cref="ArgumentNullException">Не заданы <paramref name="services"/> или <paramref name="configure"/>.</exception>
    /// <remarks>
    /// <code>
    /// builder.Services.AddKnddbTrackAndTrace(o =&gt; o.Environment = KnddbEnvironment.Production);
    /// </code>
    /// </remarks>
    public static IServiceCollection AddKnddbTrackAndTrace(
        this IServiceCollection services,
        Action<KnddbClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<KnddbClientOptions>().Configure(configure);

        return services.AddKnddbTrackAndTraceCore();
    }

    /// <summary>
    /// Подключает <see cref="KnddbApiClient"/> для указанного контура.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="environment">Контур: тестовый или боевой.</param>
    /// <returns>Та же коллекция сервисов.</returns>
    /// <remarks>
    /// <code>
    /// builder.Services.AddKnddbTrackAndTrace(KnddbEnvironment.Production);
    /// </code>
    /// </remarks>
    public static IServiceCollection AddKnddbTrackAndTrace(
        this IServiceCollection services,
        KnddbEnvironment environment)
        => services.AddKnddbTrackAndTrace(options => options.Environment = environment);

    /// <summary>
    /// Регистрирует компоненты SDK: хранилища сессий, HTTP-клиент и <see cref="KnddbApiClient"/>.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Та же коллекция сервисов.</returns>
    /// <remarks>
    /// Вызывайте напрямую, только если настройки <see cref="KnddbClientOptions"/>
    /// регистрируются приложением самостоятельно.
    /// </remarks>
    public static IServiceCollection AddKnddbTrackAndTraceCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Сессии и учётные данные общие для всего приложения:
        // пользователи переключаются ключом сессии, а не отдельным экземпляром клиента.
        services.TryAddSingleton<IKnddbSessionStore, InMemoryKnddbSessionStore>();
        services.TryAddSingleton<IKnddbCredentialStore, InMemoryKnddbCredentialStore>();

        services.AddHttpClient(KnddbClientOptions.DefaultHttpClientName, static (provider, client) =>
                provider.GetRequiredService<IOptions<KnddbClientOptions>>().Value.ApplyDefaultHeaders(client))
            .ConfigurePrimaryHttpMessageHandler(KnddbClientOptions.CreateHandler);

        services.TryAddSingleton(static provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var options = provider.GetRequiredService<IOptions<KnddbClientOptions>>().Value;

            return new KnddbApiClient(
                factory.CreateClient(options.HttpClientName),
                options,
                provider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>());
        });

        return services;
    }
}
