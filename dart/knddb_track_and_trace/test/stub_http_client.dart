import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';

/// Снимок отправленного запроса.
class RecordedRequest {
  /// Создаёт снимок.
  RecordedRequest({
    required this.method,
    required this.path,
    this.query,
    this.authorization,
    this.body,
    this.userAgent,
  });

  /// HTTP-метод.
  final String method;

  /// Путь запроса.
  final String path;

  /// Строка запроса.
  final String? query;

  /// Заголовок `Authorization`.
  final String? authorization;

  /// Тело запроса.
  final String? body;

  /// Заголовок `User-Agent`.
  final String? userAgent;

  /// Разбирает тело запроса как JSON.
  Map<String, dynamic> json() => body == null || body!.isEmpty
      ? <String, dynamic>{}
      : jsonDecode(body!) as Map<String, dynamic>;
}

/// HTTP-клиент-заглушка: отдаёт заранее подготовленные ответы и записывает
/// отправленные запросы.
///
/// Позволяет тестировать SDK целиком — от формирования запроса до разбора
/// ответа — без реального сервера KNMDB.
class StubHttpClient extends http.BaseClient {
  /// Создаёт заглушку.
  ///
  /// [responder] получает запрос и порядковый номер вызова (начиная с нуля)
  /// и возвращает ответ.
  StubHttpClient(this.responder);

  /// Функция ответа.
  final http.Response Function(http.Request request, int index) responder;

  /// Все запросы в порядке отправки.
  final List<RecordedRequest> requests = <RecordedRequest>[];

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final body = request is http.Request ? request.body : '';
    final requestUri = request.url;

    requests.add(
      RecordedRequest(
        method: request.method,
        path: requestUri.path,
        query: requestUri.hasQuery ? '?${requestUri.query}' : null,
        authorization: request.headers['authorization'],
        body: body.isEmpty ? null : body,
        userAgent: request.headers['user-agent'],
      ),
    );

    final response = responder(
      http.Request(request.method, requestUri)..body = body,
      requests.length - 1,
    );

    return http.StreamedResponse(
      Stream<List<int>>.value(response.bodyBytes),
      response.statusCode,
      headers: response.headers,
      request: request,
    );
  }
}

/// Готовит ответ с JSON-телом.
http.Response jsonResponse(Object? payload, {int statusCode = 200}) =>
    http.Response(
      payload is String ? payload : jsonEncode(payload),
      statusCode,
      headers: const {'content-type': 'application/json'},
    );

/// Готовит успешный ответ в конверте KNMDB:
/// `{ resultCode: 0, resultMessage: …, actionResult: … }`.
///
/// Именно так отвечает настоящий сервер: фильтр `ActionResultFilterAttribute`
/// оборачивает результат каждого метода Track and Trace в этот конверт.
///
/// [payload] — готовый JSON-фрагмент полезной нагрузки, например
/// `'{ "numberOfStakeholders": 0 }'`.
http.Response envelope(String payload) => jsonResponse(
      '{"resultCode":0,"resultMessage":"Action completed successfully.",'
      '"actionResult":$payload}',
    );

/// Готовит ошибку бизнес-логики в конверте с HTTP-статусом 200.
///
/// Настоящий сервер так отвечает на бизнес-исключение: статус остаётся 200,
/// а причина передаётся кодом `resultCode`.
http.Response envelopeError(int resultCode, String resultMessage) =>
    jsonResponse(<String, Object?>{
      'resultCode': resultCode,
      'resultMessage': resultMessage,
      'actionResult': null,
    });

/// Готовит ответ с телом в формате ProblemDetails.
http.Response problemResponse({
  required int statusCode,
  String? detail,
  String? title,
  Map<String, Object?>? extensions,
  Map<String, String>? headers,
}) {
  final payload = <String, Object?>{
    'type': 'about:blank',
    'title': title ?? 'Error',
    'status': statusCode,
    if (detail != null) 'detail': detail,
    ...?extensions,
  };

  return http.Response(
    jsonEncode(payload),
    statusCode,
    headers: {'content-type': 'application/json', ...?headers},
  );
}

/// Готовит ответ с токенами.
http.Response tokenResponse({
  String accessToken = 'access-token',
  String refreshToken = 'refresh-token',
  int expiresIn = 3600,
}) =>
    jsonResponse(<String, Object?>{
      'access_token': accessToken,
      'token_type': 'Bearer',
      'expires_in': expiresIn,
      'scope': 'api offline_access',
      'refresh_token': refreshToken,
    });

/// Создаёт клиент SDK поверх заглушки.
({KnddbApiClient client, StubHttpClient http}) createClient({
  required http.Response Function(http.Request request, int index) responder,
  KnddbEnvironment environment = KnddbEnvironment.test,
  Duration? tokenExpirationMargin,
}) {
  final stub = StubHttpClient(responder);
  final client = KnddbApiClient(
    options: KnddbClientOptions(
      environment: environment,
      tokenExpirationMargin:
          tokenExpirationMargin ?? const Duration(seconds: 30),
    ),
    httpClient: stub,
  );

  return (client: client, http: stub);
}
