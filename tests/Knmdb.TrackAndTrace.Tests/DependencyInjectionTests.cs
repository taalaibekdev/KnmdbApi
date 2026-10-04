using System.Reflection;
using Knmdb.TrackAndTrace.Models.Responses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Knmdb.TrackAndTrace.Tests;

/// <summary>
/// Проверяет подключение SDK через внедрение зависимостей и пометку устаревшего функционала.
/// </summary>
public sealed class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Клиент_регистрируется_по_контуру()
    {
        using var provider = BuildProvider(services =>
            services.AddKnddbTrackAndTrace(KnddbEnvironment.Production));

        var client = provider.GetRequiredService<KnddbApiClient>();

        Assert.Equal(KnddbEnvironment.Production, client.Environment);
        Assert.Equal("ndbapi.med.kg", client.BaseAddress.Host);
        Assert.False(client.IsAuthenticated);
    }

    [Fact]
    public void Клиент_регистрируется_по_делегату_настройки()
    {
        using var provider = BuildProvider(services => services.AddKnddbTrackAndTrace(options =>
        {
            options.Environment = KnddbEnvironment.Test;
            options.UserAgent = "TestAgent/1.0";
            options.Timeout = TimeSpan.FromMinutes(3);
        }));

        var options = provider.GetRequiredService<IOptions<KnddbClientOptions>>().Value;

        Assert.Equal(KnddbEnvironment.Test, options.Environment);
        Assert.Equal("TestAgent/1.0", options.UserAgent);
        Assert.Equal(TimeSpan.FromMinutes(3), options.Timeout);
        Assert.Equal("testndbapi.med.kg", provider.GetRequiredService<KnddbApiClient>().BaseAddress.Host);
    }

    [Fact]
    public void Клиент_регистрируется_по_конфигурации()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Knddb:Environment"] = "Production",
                ["Knddb:UserAgent"] = "ConfigAgent/2.0",
                ["Knddb:Timeout"] = "00:01:30",
                ["Knddb:DefaultSessionKey"] = "pharmacy:main",
            })
            .Build();

        using var provider = BuildProvider(services => services.AddKnddbTrackAndTrace(configuration));

        var options = provider.GetRequiredService<IOptions<KnddbClientOptions>>().Value;

        Assert.Equal(KnddbEnvironment.Production, options.Environment);
        Assert.Equal("ConfigAgent/2.0", options.UserAgent);
        Assert.Equal(TimeSpan.FromSeconds(90), options.Timeout);
        Assert.Equal("pharmacy:main", options.DefaultSessionKey);
        Assert.Equal("ndbapi.med.kg", provider.GetRequiredService<KnddbApiClient>().BaseAddress.Host);
    }

    [Fact]
    public void Явный_адрес_в_конфигурации_имеет_приоритет_над_контуром()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Knddb:Environment"] = "Production",
                ["Knddb:BaseAddress"] = "http://localhost:5001/",
            })
            .Build();

        using var provider = BuildProvider(services => services.AddKnddbTrackAndTrace(configuration));

        var client = provider.GetRequiredService<KnddbApiClient>();

        Assert.Equal("localhost", client.BaseAddress.Host);
        Assert.Null(client.Environment);   // адрес не соответствует ни одному контуру
    }

    [Fact]
    public void Сессии_общие_для_всего_приложения()
    {
        using var provider = BuildProvider(services =>
            services.AddKnddbTrackAndTrace(KnddbEnvironment.Test));

        var first = provider.GetRequiredService<IKnddbSessionStore>();
        var second = provider.GetRequiredService<IKnddbSessionStore>();

        Assert.Same(first, second);

        first.Set(new KnddbSession { Key = "user:1", AccessToken = "token" });
        Assert.Equal("token", second.Get("user:1")!.AccessToken);
    }

    [Fact]
    public void Клиент_один_на_приложение_и_обслуживает_несколько_пользователей()
    {
        using var provider = BuildProvider(services =>
            services.AddKnddbTrackAndTrace(KnddbEnvironment.Test));

        var first = provider.GetRequiredService<KnddbApiClient>();
        var second = provider.GetRequiredService<KnddbApiClient>();

        Assert.Same(first, second);
    }

    [Fact]
    public void Повторная_регистрация_не_ломает_контейнер()
    {
        using var provider = BuildProvider(services =>
        {
            services.AddKnddbTrackAndTrace(KnddbEnvironment.Test);
            services.AddKnddbTrackAndTrace(KnddbEnvironment.Test);
        });

        Assert.NotNull(provider.GetRequiredService<KnddbApiClient>());
    }

    [Fact]
    public void Расширения_DI_доступны_без_отдельного_пакета()
    {
        var method = typeof(KnddbApiClient).Assembly
            .GetType("Microsoft.Extensions.DependencyInjection.KnddbServiceCollectionExtensions");

        Assert.NotNull(method);
        Assert.True(method!.IsPublic);
    }

    [Fact]
    public void Метод_GetSupportedQrTypesAsync_помечен_устаревшим()
    {
        var method = typeof(KnddbApiClient).GetMethod(nameof(KnddbApiClient.GetSupportedQrTypesAsync));

        Assert.NotNull(method);

        var obsolete = method!.GetCustomAttribute<ObsoleteAttribute>();

        Assert.NotNull(obsolete);
        Assert.False(obsolete!.IsError);
        Assert.Contains("департамент", obsolete.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Типы_типов_QR_кодов_помечены_устаревшими()
    {
        Assert.NotNull(typeof(QRTypeInfo).GetCustomAttribute<ObsoleteAttribute>());
        Assert.NotNull(typeof(GetSupportedQRTypesResponse).GetCustomAttribute<ObsoleteAttribute>());
    }

    [Fact]
    public void Актуальные_методы_не_помечены_устаревшими()
    {
        var obsoleteMethods = typeof(KnddbApiClient)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<ObsoleteAttribute>() is not null)
            .Select(m => m.Name)
            .ToArray();

        // Устаревшим должен быть ровно один метод — GetSupportedQrTypesAsync.
        Assert.Equal([nameof(KnddbApiClient.GetSupportedQrTypesAsync)], obsoleteMethods);
    }
}
