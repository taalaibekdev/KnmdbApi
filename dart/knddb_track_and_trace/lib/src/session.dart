import 'dart:async';

import 'package:meta/meta.dart';

import 'models/auth/knddb_credentials.dart';

/// Состояние входа одного пользователя KNMDB.
///
/// Каждая сессия полностью независима, поэтому один экземпляр SDK безопасно
/// обслуживает нескольких пользователей сразу: у каждой сессии свои токены
/// и свой мутекс обновления.
///
/// Экземпляры создаются методом `KnddbApiClient.signIn`; вручную их создавать
/// не требуется.
class KnddbSession {
  /// Создаёт сессию.
  KnddbSession({
    required this.key,
    this.userName,
    this.credentials,
    this.accessToken,
    this.refreshToken,
    this.tokenType = 'Bearer',
    this.accessTokenExpiresAt,
    this.lastRefreshedAt,
    this.scope,
  });

  /// Ключ сессии по умолчанию.
  static const String defaultKey = 'default';

  /// Восстанавливает сессию из сохранённого JSON.
  ///
  /// Учётные данные (пароль) в сохраняемом JSON отсутствуют, поэтому после
  /// восстановления для автоматического продления сессии потребуется либо
  /// [refreshToken], либо повторный вход.
  factory KnddbSession.fromJson(Map<String, dynamic> json) {
    return KnddbSession(
      key: json['key'] as String,
      userName: json['userName'] as String?,
      accessToken: json['accessToken'] as String?,
      refreshToken: json['refreshToken'] as String?,
      tokenType: (json['tokenType'] as String?) ?? 'Bearer',
      accessTokenExpiresAt: _parseDate(json['accessTokenExpiresAt']),
      lastRefreshedAt: _parseDate(json['lastRefreshedAt']),
      scope: json['scope'] as String?,
    );
  }

  /// Ключ сессии — идентификатор пользователя внутри SDK.
  ///
  /// Для нескольких пользователей используйте ключи вида `user:{login}`.
  final String key;

  /// Логин пользователя.
  String? userName;

  /// Учётные данные для автоматического получения нового токена.
  ///
  /// Хранятся только в памяти процесса. Если приложение не должно держать
  /// пароль в памяти, вызовите вход с `storeCredentials: false`.
  KnddbCredentials? credentials;

  /// Токен доступа (JWT) для заголовка `Authorization: Bearer ...`.
  String? accessToken;

  /// Токен обновления: позволяет получить новый токен доступа без пароля.
  String? refreshToken;

  /// Тип токена. Для KNMDB — `Bearer`.
  String tokenType;

  /// Момент истечения токена доступа (UTC).
  DateTime? accessTokenExpiresAt;

  /// Момент последнего успешного получения или обновления токена (UTC).
  DateTime? lastRefreshedAt;

  /// Области доступа, выданные сервером (значение поля `scope`).
  String? scope;

  /// Мутекс обновления токена.
  ///
  /// Гарантирует, что при нескольких параллельных запросах токен обновится
  /// один раз, а остальные запросы дождутся результата.
  @internal
  final TokenMutex mutex = TokenMutex();

  /// Признак того, что пользователь вошёл в систему.
  bool get isAuthenticated => (accessToken ?? '').isNotEmpty;

  /// Проверяет, можно ли использовать токен доступа.
  ///
  /// [margin] — запас времени до истечения, при котором токен уже считается
  /// истекающим. [now] — текущее время (для тестируемости).
  bool isAccessTokenValid({Duration? margin, DateTime? now}) {
    if (!isAuthenticated || accessTokenExpiresAt == null) {
      return false;
    }

    final current = now ?? DateTime.now().toUtc();
    return accessTokenExpiresAt!.isAfter(current.add(margin ?? Duration.zero));
  }

  /// Остаток времени жизни токена доступа.
  Duration remainingLifetime({DateTime? now}) {
    final expiresAt = accessTokenExpiresAt;
    if (expiresAt == null) {
      return Duration.zero;
    }

    return expiresAt.difference(now ?? DateTime.now().toUtc());
  }

  /// Применяет к сессии ответ конечной точки `/connect/token`.
  void applyTokenResponse(Map<String, dynamic> response, {DateTime? now}) {
    final token = response['access_token'] as String?;
    if (token == null || token.isEmpty) {
      throw StateError('Сервер авторизации KNMDB не вернул поле access_token');
    }

    final current = now ?? DateTime.now().toUtc();
    final expiresIn = (response['expires_in'] as num?)?.toInt() ?? 0;

    accessToken = token;

    final tokenTypeValue = response['token_type'] as String?;
    tokenType = (tokenTypeValue != null && tokenTypeValue.isNotEmpty)
        ? tokenTypeValue
        : 'Bearer';

    // expires_in приходит в секундах. Если сервер не прислал значение, считаем
    // токен короткоживущим, чтобы не работать с «вечным» токеном.
    accessTokenExpiresAt =
        current.add(Duration(seconds: expiresIn > 0 ? expiresIn : 300));

    // Токен обновления приходит не всегда: тогда сохраняем ранее полученный.
    final refresh = response['refresh_token'] as String?;
    if (refresh != null && refresh.isNotEmpty) {
      refreshToken = refresh;
    }

    final scopeValue = response['scope'] as String?;
    if (scopeValue != null && scopeValue.isNotEmpty) {
      scope = scopeValue;
    }

    lastRefreshedAt = current;
  }

  /// Сбрасывает токены, сохраняя ключ, логин и учётные данные.
  void clearTokens() {
    accessToken = null;
    refreshToken = null;
    accessTokenExpiresAt = null;
    scope = null;
  }

  /// Сохраняемая часть сессии.
  ///
  /// Учётные данные (пароль) в неё намеренно не попадают: сохранять пароли
  /// на устройстве SDK не должен.
  Map<String, dynamic> toJson() => {
        'key': key,
        'userName': userName,
        'accessToken': accessToken,
        'refreshToken': refreshToken,
        'tokenType': tokenType,
        'accessTokenExpiresAt': accessTokenExpiresAt?.toIso8601String(),
        'lastRefreshedAt': lastRefreshedAt?.toIso8601String(),
        'scope': scope,
      };

  static DateTime? _parseDate(Object? value) {
    if (value is String && value.isNotEmpty) {
      return DateTime.tryParse(value)?.toUtc();
    }

    return null;
  }

  @override
  String toString() => 'KnddbSession(key: $key, userName: $userName, '
      'isAuthenticated: $isAuthenticated, expiresAt: $accessTokenExpiresAt)';
}

/// Мутекс на основе цепочки [Future].
///
/// Нужен, чтобы два параллельных запроса не обновляли токен одновременно:
/// один обновляет, остальные ждут результата.
class TokenMutex {
  Future<void> _tail = Future<void>.value();

  /// Выполняет [action], не допуская одновременного выполнения с другой задачей.
  Future<T> run<T>(Future<T> Function() action) {
    final completer = Completer<void>();
    final previous = _tail;
    _tail = completer.future;

    return previous.then((_) => action()).whenComplete(completer.complete);
  }
}
