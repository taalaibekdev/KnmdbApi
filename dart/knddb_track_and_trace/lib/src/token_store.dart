import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import 'exceptions.dart';
import 'json.dart';
import 'models/api/api_problem_details.dart';
import 'models/auth/knddb_credentials.dart';
import 'options.dart';
import 'result_codes.dart';
import 'session.dart';
import 'session_storage.dart';

/// Получает и обновляет токены OAuth 2.0 для сессий KNMDB.
///
/// Работает с конечной точкой `POST /connect/token`. Схема:
///
/// 1. вход по логину и паролю (`grant_type=password`);
/// 2. обновление по токену обновления (`grant_type=refresh_token`);
/// 3. повторный вход по сохранённым учётным данным, если токен обновления отклонён.
class KnddbTokenStore {
  /// Создаёт хранилище токенов.
  KnddbTokenStore({
    required this.options,
    required http.Client client,
    KnddbSessionStorage? storage,
  })  : _client = client,
        _storage = storage;

  /// Путь конечной точки выдачи токенов.
  static const String tokenPath = 'connect/token';

  /// Настройки SDK.
  final KnddbClientOptions options;

  final http.Client _client;
  final KnddbSessionStorage? _storage;
  final Map<String, KnddbSession> _sessions = <String, KnddbSession>{};

  /// Сессии, известные SDK на текущий момент.
  Iterable<KnddbSession> get sessions => _sessions.values;

  /// Возвращает сессию по ключу или `null`.
  KnddbSession? find(String key) => _sessions[key];

  /// Возвращает существующую сессию или создаёт новую.
  KnddbSession getOrCreate(String key, {String? userName}) {
    final existing = _sessions[key];
    if (existing != null) {
      if (userName != null) {
        existing.userName = userName;
      }
      return existing;
    }

    final created = KnddbSession(key: key, userName: userName);
    _sessions[key] = created;
    return created;
  }

  /// Регистрирует восстановленную сессию.
  void register(KnddbSession session) {
    _sessions[session.key] = session;
  }

  /// Выполняет вход и возвращает сессию с действующими токенами.
  Future<KnddbSession> signIn(
    String sessionKey,
    KnddbCredentials? credentials, {
    bool storeCredentials = true,
  }) async {
    final session = getOrCreate(sessionKey);
    final effective = _resolveCredentials(sessionKey, session, credentials);

    return session.mutex.run(() async {
      final response =
          await _requestToken(_passwordGrant(effective), sessionKey);

      session.userName = effective.userName;
      session.credentials = storeCredentials ? effective : null;
      session.applyTokenResponse(response);

      await _persist(session);
      return session;
    });
  }

  /// Формирует поля запроса токена для потока «логин и пароль».
  ///
  /// Параметр `scope` добавляется, только если он задан явно: пустое значение
  /// [KnddbClientOptions.scope] означает «использовать области сервера
  /// по умолчанию».
  Map<String, String> _passwordGrant(KnddbCredentials credentials) {
    final form = <String, String>{
      'grant_type': 'password',
      'username': credentials.userName,
      'password': credentials.password,
    };

    final scope = options.scope ?? KnddbClientOptions.defaultScope;
    if (scope.trim().isNotEmpty) {
      form['scope'] = scope;
    }

    return form;
  }

  /// Возвращает сессию с действующим токеном доступа, получая или обновляя его
  /// при необходимости.
  ///
  /// [requireAuthentication] — `false` для методов, доступных без авторизации:
  /// тогда при отсутствии учётных данных запрос уйдёт без токена.
  /// [forceRefresh] — принудительно обновить токен (после ответа 401).
  Future<KnddbSession> getSession(
    String sessionKey, {
    bool requireAuthentication = true,
    bool forceRefresh = false,
  }) async {
    final session = getOrCreate(sessionKey);

    if (!forceRefresh &&
        session.isAccessTokenValid(margin: options.tokenExpirationMargin)) {
      return session;
    }

    final credentials = session.credentials ?? options.defaultCredentials;

    if (credentials == null && (session.refreshToken ?? '').isEmpty) {
      if (requireAuthentication) {
        throw KnddbAuthenticationException(
          'Для сессии «$sessionKey» нет действующего токена и не заданы учётные данные. '
          'Выполните вход: signIn(...) или заполните KnddbClientOptions.defaultCredentials.',
          sessionKey: sessionKey,
        );
      }

      return session;
    }

    return session.mutex.run(() async {
      // Пока ждали мутекс, токен мог обновиться в параллельном запросе.
      if (!forceRefresh &&
          session.isAccessTokenValid(margin: options.tokenExpirationMargin)) {
        return session;
      }

      return _renew(session, credentials);
    });
  }

  /// Завершает сессию: удаляет токены и сохранённые учётные данные.
  Future<void> signOut(String sessionKey) async {
    final session = _sessions.remove(sessionKey);
    session?.clearTokens();
    session?.credentials = null;
    session?.userName = null;

    final storage = _storage;
    if (storage != null) {
      await storage.clear();
    }
  }

  /// Пытается восстановить сессию из хранилища.
  ///
  /// Возвращает восстановленную сессию или `null`.
  Future<KnddbSession?> restore() async {
    final storage = _storage;
    if (storage == null) {
      return null;
    }

    final restored = await storage.load();
    if (restored == null) {
      return null;
    }

    _sessions[restored.key] = restored;
    return restored;
  }

  Future<KnddbSession> _renew(
      KnddbSession session, KnddbCredentials? credentials) async {
    final refreshToken = session.refreshToken;
    if (refreshToken != null && refreshToken.isNotEmpty) {
      try {
        final refreshed = await _requestToken(<String, String>{
          'grant_type': 'refresh_token',
          'refresh_token': refreshToken,
        }, session.key);

        session.applyTokenResponse(refreshed);
        await _persist(session);
        return session;
      } on KnddbAuthenticationException {
        // Токен обновления отклонён — переходим к повторному входу.
        session.refreshToken = null;
      }
    }

    if (credentials == null) {
      throw KnddbAuthenticationException(
        'Сессия «${session.key}»: не удалось обновить токен и нет учётных данных '
        'для повторного входа. Выполните вход заново.',
        sessionKey: session.key,
      );
    }

    final response =
        await _requestToken(_passwordGrant(credentials), session.key);

    session.credentials ??= credentials;
    session.userName = credentials.userName;
    session.applyTokenResponse(response);

    await _persist(session);
    return session;
  }

  KnddbCredentials _resolveCredentials(
    String sessionKey,
    KnddbSession session,
    KnddbCredentials? credentials,
  ) {
    final resolved =
        credentials ?? session.credentials ?? options.defaultCredentials;

    if (resolved == null) {
      throw KnddbConfigurationException(
        'Не заданы учётные данные для сессии «$sessionKey». '
        'Передайте логин и пароль в signIn(...) или заполните '
        'KnddbClientOptions.defaultCredentials.',
      );
    }

    if (resolved.userName.trim().isEmpty) {
      throw const KnddbConfigurationException(
          'Логин пользователя KNMDB не может быть пустым.');
    }

    if (resolved.password.isEmpty) {
      throw const KnddbConfigurationException(
          'Пароль пользователя KNMDB не может быть пустым.');
    }

    return resolved;
  }

  Future<void> _persist(KnddbSession session) async {
    final storage = _storage;
    if (storage != null) {
      await storage.save(session);
    }
  }

  Future<Map<String, dynamic>> _requestToken(
    Map<String, String> form,
    String sessionKey,
  ) async {
    final baseUrl = options.validate();
    final uri = Uri.parse(baseUrl).resolve(tokenPath);

    http.Response response;
    try {
      response = await _client
          .post(
            uri,
            headers: <String, String>{
              'content-type': 'application/x-www-form-urlencoded',
              // Сервер KNMDB записывает user-agent в базу при входе: без этого
              // заголовка сохранение падает с кодом результата 2.
              'user-agent':
                  options.userAgent ?? KnddbClientOptions.defaultUserAgent,
            },
            body: form,
          )
          .timeout(options.timeout);
    } on TimeoutException catch (error) {
      throw KnddbAuthenticationException(
        'Превышен таймаут (${options.timeout.inSeconds} с) при обращении '
        'к серверу авторизации KNMDB.',
        method: 'POST',
        requestUri: '/$tokenPath',
        sessionKey: sessionKey,
        cause: error,
      );
    } on SocketException catch (error) {
      throw KnddbAuthenticationException(
        'Не удалось обратиться к серверу авторизации KNMDB по адресу '
        '«$uri». Проверьте сетевое подключение и адрес сервера.',
        method: 'POST',
        requestUri: '/$tokenPath',
        sessionKey: sessionKey,
        cause: error,
      );
    } on http.ClientException catch (error) {
      throw KnddbAuthenticationException(
        'Сетевой сбой при обращении к серверу авторизации KNMDB: ${error.message}',
        method: 'POST',
        requestUri: '/$tokenPath',
        sessionKey: sessionKey,
        cause: error,
      );
    }

    final body = response.body;

    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw _tokenError(response.statusCode, body, sessionKey);
    }

    final decoded = jsonDecode(body);
    if (decoded is! Map) {
      throw KnddbAuthenticationException(
        'Сервер авторизации KNMDB вернул ответ неизвестного формата.',
        sessionKey: sessionKey,
        responseBody: body,
      );
    }

    final map = asMap(decoded)!;

    // Сервер может ответить кодом 200 и конвертом с ошибкой: обработчик
    // исключений KNMDB возвращает статус 200 даже при внутреннем сбое.
    // Без этой проверки пользователь увидел бы невнятное «сервер не вернул
    // поле access_token» вместо настоящей причины.
    final resultCode = asInt(map['resultCode']);
    if (resultCode != null && resultCode != KnddbResultCodes.success) {
      throw _envelopeError(
          resultCode, map['resultMessage'] as String?, body, sessionKey);
    }

    return map;
  }

  /// Создаёт исключение по конверту с ошибкой, пришедшему с HTTP-статусом 200.
  KnddbAuthenticationException _envelopeError(
    int resultCode,
    String? resultMessage,
    String body,
    String sessionKey,
  ) {
    final description = KnddbResultCodes.describe(resultCode);

    var message = 'Сервер KNMDB не выполнил вход (resultCode $resultCode).';

    if (description != null) {
      message = '$message $description';
    }

    if (resultMessage != null && resultMessage.isNotEmpty) {
      message = '$message Ответ сервера: $resultMessage';
    }

    if (resultCode == KnddbResultCodes.unexpectedError) {
      message += ' Вероятная причина — несовместимость запроса с сервером: '
          'проверьте заголовок user-agent и формат дат.';
    }

    return KnddbAuthenticationException(
      message,
      method: 'POST',
      requestUri: '/$tokenPath',
      statusCode: 200,
      responseBody: body,
      sessionKey: sessionKey,
      resultCode: resultCode,
      resultMessage: resultMessage,
    );
  }

  KnddbAuthenticationException _tokenError(
    int statusCode,
    String body,
    String sessionKey,
  ) {
    String? oauthError;
    String? oauthDescription;

    if (body.trimLeft().startsWith('{')) {
      try {
        final decoded = asMap(jsonDecode(body));
        oauthError = decoded?['error'] as String?;
        oauthDescription = decoded?['error_description'] as String?;

        if (oauthError == null && oauthDescription == null) {
          // Иногда сервер отвечает в формате ProblemDetails.
          oauthError = decoded?['title'] as String?;
          oauthDescription = decoded?['detail'] as String?;
        }
      } on FormatException {
        // Тело не является JSON — оставляем сообщение по HTTP-статусу.
      }
    }

    final message = switch (statusCode) {
      400 ||
      401 =>
        'Сервер KNMDB отклонил запрос токена: неверный логин или пароль, '
            'либо токен обновления больше не действителен.',
      403 =>
        'Учётной записи KNMDB запрещён вход (учётная запись заблокирована или отключена).',
      503 => 'Сервер авторизации KNMDB временно недоступен.',
      _ => 'Сервер авторизации KNMDB вернул ошибку $statusCode.',
    };

    final details = <String>[];
    if (oauthError != null && oauthError.isNotEmpty) {
      details.add('Код ошибки OAuth: $oauthError');
    }
    if (oauthDescription != null && oauthDescription.isNotEmpty) {
      details.add(oauthDescription);
    }

    return KnddbAuthenticationException(
      details.isEmpty ? message : '$message ${details.join('. ')}.',
      method: 'POST',
      requestUri: '/$tokenPath',
      statusCode: statusCode,
      responseBody: body,
      sessionKey: sessionKey,
      oauthError: oauthError,
    );
  }
}

/// Собирает исключение по неуспешному HTTP-ответу API.
KnddbApiException buildApiException({
  required int statusCode,
  required String body,
  required String method,
  required String path,
  String? sessionKey,
  Map<String, String>? headers,
}) {
  ApiProblemDetails? problem;
  if (body.trimLeft().startsWith('{')) {
    try {
      final decoded = asMap(jsonDecode(body));
      if (decoded != null) {
        problem = ApiProblemDetails.fromJson(decoded);
      }
    } on FormatException {
      // Тело не является JSON — диагностика по HTTP-статусу.
    }
  }

  final traceId = headers?['x-request-id'] ?? headers?['traceparent'];

  var message = switch (statusCode) {
    400 => 'Сервер KNMDB отклонил запрос: некорректные данные.',
    401 =>
      'Сервер KNMDB не принял токен доступа (401). Требуется повторный вход.',
    403 => 'Недостаточно прав для вызова метода (403). '
        'Обратитесь к администратору департамента лекарственных средств.',
    404 => 'Запрашиваемые данные не найдены (404).',
    409 => 'Конфликт состояния на сервере KNMDB (409): '
        'операция несовместима с текущим состоянием объекта.',
    408 => 'Сервер KNMDB не успел обработать запрос (408).',
    _ when statusCode >= 500 =>
      'Внутренняя ошибка сервера KNMDB ($statusCode).',
    _ => 'Сервер KNMDB вернул ошибку $statusCode.',
  };

  final details = problem?.message;
  if (details != null && details.isNotEmpty && !message.contains(details)) {
    message = '$message $details';
  }

  if (traceId != null && traceId.isNotEmpty) {
    message = '$message (traceId: $traceId)';
  }

  final errors = _extractErrors(problem, body);

  if (statusCode == 401) {
    return KnddbAuthenticationException(
      message,
      method: method,
      requestUri: path,
      statusCode: statusCode,
      responseBody: body,
      problemDetails: problem,
      traceId: traceId,
      errors: errors,
      sessionKey: sessionKey,
    );
  }

  return KnddbApiException(
    message,
    method: method,
    requestUri: path,
    statusCode: statusCode,
    responseBody: body,
    problemDetails: problem,
    traceId: traceId,
    errors: errors,
  );
}

/// Собирает исключение по ошибке бизнес-логики из конверта KNMDB.
///
/// Сервер отвечает конвертом `{ resultCode, resultMessage, actionResult }`
/// и при ошибке бизнес-логики оставляет HTTP-статус `200`, поэтому вызывать эту
/// функцию нужно по значению `resultCode`, а не по статусу.
KnddbApiException buildBusinessException({
  required int resultCode,
  required String? resultMessage,
  required String method,
  required String path,
  String? sessionKey,
  String? body,
}) {
  final description = KnddbResultCodes.describe(resultCode);

  var message =
      description ?? resultMessage ?? 'Сервер KNMDB сообщил об ошибке.';

  if (description != null &&
      resultMessage != null &&
      resultMessage.isNotEmpty) {
    message = '$message Ответ сервера: $resultMessage';
  }

  // Ошибка бизнес-логики приходит с HTTP 200 — по статусу её не отличить.
  return KnddbApiException(
    message,
    method: method,
    requestUri: path,
    statusCode: 200,
    responseBody: body,
    resultCode: resultCode,
    resultMessage: resultMessage,
  );
}

Map<String, String>? _extractErrors(ApiProblemDetails? problem, String body) {
  final result = <String, String>{};

  final extensionErrors = problem?.extensions?['errors'];
  if (extensionErrors is Map) {
    for (final entry in extensionErrors.entries) {
      final value = entry.value;
      result[entry.key.toString()] =
          value is List ? value.join('; ') : value.toString();
    }
  }

  if (result.isEmpty) {
    final detail = problem?.detail;
    if (detail != null && detail.isNotEmpty) {
      result['detail'] = detail;
    }
  }

  if (result.isEmpty &&
      problem == null &&
      body.isNotEmpty &&
      !body.trimLeft().startsWith('{')) {
    result['response'] = body.length > 512 ? body.substring(0, 512) : body;
  }

  return result.isEmpty ? const {} : result;
}
