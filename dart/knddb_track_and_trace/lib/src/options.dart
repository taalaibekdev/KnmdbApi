import 'environment.dart';
import 'models/auth/knddb_credentials.dart';

/// Настройки SDK KNMDB.
///
/// Обязательно задаётся только контур: [KnddbEnvironment.test] (по умолчанию)
/// или [KnddbEnvironment.production]. Остальные параметры необязательны.
///
/// ```dart
/// final options = KnddbClientOptions(
///   environment: KnddbEnvironment.production,
///   userAgent: 'MyPharmacyApp/1.0',
/// );
/// ```
class KnddbClientOptions {
  /// Создаёт настройки.
  ///
  /// [environment] — контур API; по умолчанию тестовый.
  /// [baseUrl] — явный базовый адрес; если задан, имеет приоритет над контуром
  /// (используется для локального стенда или прокси).
  /// [timeout] — таймаут одного запроса; по умолчанию 100 секунд. Увеличьте
  /// его для массовых операций с большим числом QR-кодов.
  /// [defaultCredentials] — логин и пароль по умолчанию; заполняйте только для
  /// однопользовательских приложений.
  /// [tokenExpirationMargin] — запас времени до истечения токена, при котором
  /// он обновляется заранее; по умолчанию 30 секунд.
  /// [defaultSessionKey] — ключ сессии по умолчанию.
  KnddbClientOptions({
    this.environment = KnddbEnvironment.test,
    this.baseUrl,
    this.timeout = const Duration(seconds: 100),
    this.defaultCredentials,
    this.tokenExpirationMargin = const Duration(seconds: 30),
    this.defaultSessionKey = defaultSessionKeyValue,
    this.userAgent,
    this.scope,
  });

  /// Значение [defaultSessionKey] по умолчанию.
  static const String defaultSessionKeyValue = 'default';

  /// Пользовательский агент по умолчанию.
  ///
  /// Сервер KNMDB записывает заголовок `User-Agent` в базу данных при входе.
  /// Если заголовок не передан, запись завершается ошибкой: `resultCode 2`
  /// («An error occurred while saving the entity changes»). Поэтому SDK
  /// отправляет этот заголовок всегда — даже если приложение его не задало.
  ///
  /// Своё значение задавайте через [userAgent]: полезно указывать название
  /// и версию приложения, чтобы администратор KNMDB мог отличить интеграции
  /// в журналах.
  static const String defaultUserAgent = 'Knddb.TrackAndTrace.SDK/1.0';

  /// Область доступа (scope) OAuth 2.0 по умолчанию: `api`.
  ///
  /// Сервер KNMDB регистрирует только область `api`
  /// (`options.RegisterScopes("api")` в конфигурации OpenIddict).
  ///
  /// **Не добавляйте `offline_access`.** В OpenIddict эта область разрешена
  /// только при включённом потоке обновления токена
  /// (`AllowRefreshTokenFlow()`). Сервер KNMDB его не включает, поэтому запрос
  /// с `offline_access` отклоняется целиком: `invalid_request` — «The
  /// 'offline_access' scope is not allowed», и вход становится невозможным.
  ///
  /// Своё значение задавайте через [scope], если конфигурация вашего сервера
  /// отличается.
  static const String defaultScope = 'api';

  /// Контур API KNMDB.
  final KnddbEnvironment environment;

  /// Явный базовый адрес. Если не задан, берётся из [environment].
  final String? baseUrl;

  /// Таймаут одного HTTP-запроса.
  final Duration timeout;

  /// Логин и пароль по умолчанию.
  final KnddbCredentials? defaultCredentials;

  /// Запас времени до истечения токена, при котором он обновляется заранее.
  final Duration tokenExpirationMargin;

  /// Ключ сессии по умолчанию.
  final String defaultSessionKey;

  /// Пользовательский агент в заголовке `User-Agent`.
  ///
  /// Если не задан, отправляется [defaultUserAgent]. Заголовок отправляется
  /// всегда: без него сервер KNMDB не может сохранить запись о входе.
  final String? userAgent;

  /// Область доступа (scope) OAuth 2.0, запрашиваемая при входе.
  ///
  /// По умолчанию — [defaultScope] (`api`). Меняйте только если конфигурация
  /// вашего сервера KNMDB отличается: значение `offline_access` приведёт
  /// к отказу входа, если на сервере не включён поток обновления токена.
  ///
  /// Пустая строка означает «не передавать параметр `scope`» — тогда сервер
  /// использует области по умолчанию.
  final String? scope;

  /// Контур, которому соответствует текущий адрес подключения.
  ///
  /// `null`, если адрес не совпадает ни с тестовым, ни с боевым контуром.
  KnddbEnvironment? get resolvedEnvironment {
    final explicit = baseUrl;
    if (explicit == null || explicit.isEmpty) {
      return environment;
    }

    return KnddbEnvironment.fromBaseUrl(explicit);
  }

  /// Базовый адрес, по которому будет работать SDK.
  ///
  /// Всегда заканчивается на `/`.
  String get effectiveBaseUrl {
    final address =
        (baseUrl == null || baseUrl!.isEmpty) ? environment.baseUrl : baseUrl!;

    return address.endsWith('/') ? address : '$address/';
  }

  /// Признак работы на боевом контуре.
  ///
  /// Удобно для предупреждений в приложении перед необратимыми операциями.
  bool get isProduction => resolvedEnvironment == KnddbEnvironment.production;

  /// Проверяет настройки и возвращает нормализованный базовый адрес.
  ///
  /// Выбрасывает [ArgumentError], если адрес некорректен или таймаут не
  /// положителен.
  String validate() {
    final address = effectiveBaseUrl;
    final parsed = Uri.tryParse(address);

    if (parsed == null || !parsed.isAbsolute) {
      throw ArgumentError.value(
        address,
        'baseUrl',
        'Базовый адрес API KNMDB должен быть абсолютным URI',
      );
    }

    if (parsed.scheme != 'http' && parsed.scheme != 'https') {
      throw ArgumentError.value(
        parsed.scheme,
        'baseUrl',
        'Схема базового адреса должна быть http или https',
      );
    }

    if (timeout <= Duration.zero) {
      throw ArgumentError.value(
          timeout, 'timeout', 'Таймаут должен быть больше нуля');
    }

    if (defaultSessionKey.trim().isEmpty) {
      throw ArgumentError.value(defaultSessionKey, 'defaultSessionKey',
          'Ключ сессии не может быть пустым');
    }

    return address;
  }

  /// Создаёт копию настроек с заменой указанных значений.
  KnddbClientOptions copyWith({
    KnddbEnvironment? environment,
    String? baseUrl,
    Duration? timeout,
    KnddbCredentials? defaultCredentials,
    Duration? tokenExpirationMargin,
    String? defaultSessionKey,
    String? userAgent,
    String? scope,
  }) {
    return KnddbClientOptions(
      environment: environment ?? this.environment,
      baseUrl: baseUrl ?? this.baseUrl,
      timeout: timeout ?? this.timeout,
      defaultCredentials: defaultCredentials ?? this.defaultCredentials,
      tokenExpirationMargin:
          tokenExpirationMargin ?? this.tokenExpirationMargin,
      defaultSessionKey: defaultSessionKey ?? this.defaultSessionKey,
      userAgent: userAgent ?? this.userAgent,
      scope: scope ?? this.scope,
    );
  }

  @override
  String toString() => 'KnddbClientOptions(environment: ${environment.name}, '
      'baseUrl: $effectiveBaseUrl, timeout: ${timeout.inSeconds}s)';
}
