/// Контур (окружение) API KNMDB.
///
/// У системы два публичных адреса. Выбирайте контур осознанно: на боевом контуре
/// операции необратимы — созданные декларации влияют на реальный оборот
/// лекарственных средств.
///
/// | Контур | Базовый адрес |
/// |---|---|
/// | [KnddbEnvironment.test] | `https://testndbapi.med.kg/` |
/// | [KnddbEnvironment.production] | `https://ndbapi.med.kg/` |
///
/// Проще всего задать контур через [KnddbEnvironment.baseUrl] или
/// связать его с настройками клиента: `KnddbClientOptions(environment: ...)`.
enum KnddbEnvironment {
  /// Тестовый контур: `https://testndbapi.med.kg/`.
  ///
  /// Безопасен для экспериментов: данные тестового контура не влияют на реальный
  /// оборот. Используйте его при разработке, отладке и в тестах.
  test('https://testndbapi.med.kg/', 'тестовый контур (testndbapi.med.kg)'),

  /// Боевой (промышленный) контур: `https://ndbapi.med.kg/`.
  ///
  /// Все операции реальны и необратимы. Убедитесь, что приложение передаёт
  /// корректные данные и учётной записи назначены необходимые права.
  production('https://ndbapi.med.kg/', 'боевой контур (ndbapi.med.kg)');

  const KnddbEnvironment(this.baseUrl, this.displayName);

  /// Базовый адрес контура, включая завершающий `/`.
  final String baseUrl;

  /// Человекочитаемое название контура — для журналов и сообщений об ошибках.
  final String displayName;

  /// Определяет контур по базовому адресу.
  ///
  /// Возвращает `null`, если адрес не совпадает ни с тестовым, ни с боевым
  /// контуром (например, локальный стенд разработчика).
  static KnddbEnvironment? fromBaseUrl(String? baseUrl) {
    if (baseUrl == null || baseUrl.isEmpty) {
      return null;
    }

    final host = Uri.tryParse(baseUrl)?.host.toLowerCase();
    if (host == null || host.isEmpty) {
      return null;
    }

    for (final environment in KnddbEnvironment.values) {
      if (host == Uri.parse(environment.baseUrl).host.toLowerCase()) {
        return environment;
      }
    }

    return null;
  }
}
