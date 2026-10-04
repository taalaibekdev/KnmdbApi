import 'models/api/api_problem_details.dart';
import 'result_codes.dart';

/// Исключение, которое SDK выбрасывает, когда API KNMDB вернул неуспешный
/// HTTP-статус.
///
/// Содержит всё необходимое для диагностики: HTTP-статус, «сырое» тело ответа,
/// разобранный [problemDetails] и идентификатор запроса [traceId].
///
/// ```dart
/// try {
///   await client.productInquiryByQrCode(qrCode);
/// } on KnddbApiException catch (error) {
///   if (error.isNotFound) {
///     print('Упаковка не найдена');
///   }
///   print('${error.statusCode}: ${error.message}');
///   print('traceId: ${error.traceId}');
/// }
/// ```
class KnddbApiException implements Exception {
  /// Создаёт исключение.
  const KnddbApiException(
    this.message, {
    this.method,
    this.requestUri,
    this.statusCode,
    this.responseBody,
    this.problemDetails,
    this.traceId,
    this.errors,
    this.cause,
    this.resultCode,
    this.resultMessage,
  });

  /// Текст ошибки, готовый к показу в журнале или пользователю.
  final String message;

  /// HTTP-метод запроса (`GET`, `POST`).
  final String? method;

  /// Относительный путь запроса.
  final String? requestUri;

  /// HTTP-статус ответа.
  final int? statusCode;

  /// «Сырое» тело ответа сервера.
  final String? responseBody;

  /// Разобранное описание проблемы в формате RFC 7807.
  final ApiProblemDetails? problemDetails;

  /// Идентификатор запроса для обращения в поддержку.
  final String? traceId;

  /// Ошибки валидации: имя поля → текст ошибки.
  final Map<String, String>? errors;

  /// Исходное исключение (сетевой сбой, ошибка разбора JSON).
  final Object? cause;

  /// Код результата (`resultCode`) из конверта ответа KNMDB.
  ///
  /// **Ключевое поле для ошибок бизнес-логики.** Сервер отвечает конвертом
  /// `{ resultCode, resultMessage, actionResult }` и при ошибке бизнес-логики
  /// оставляет HTTP-статус `200`. По [statusCode] такие ошибки не отличить.
  ///
  /// `null` означает, что конверта в ответе не было: ошибку вернул уровень
  /// ASP.NET Core (проверка модели, отсутствие прав, необработанное исключение).
  ///
  /// Расшифровка кодов — в [KnddbResultCodes].
  final int? resultCode;

  /// Сообщение сервера (`resultMessage`) из конверта ответа.
  ///
  /// Как правило на английском языке, например
  /// `Product with QRCode 0104… not found`. Готовое русское пояснение
  /// возвращает [resultDescription].
  final String? resultMessage;

  /// Признак ошибки бизнес-логики: сервер вернул конверт с ненулевым
  /// [resultCode]. Такие ошибки приходят с HTTP-статусом 200.
  bool get isBusinessError =>
      resultCode != null && resultCode != KnddbResultCodes.success;

  /// Пояснение причины на русском языке.
  ///
  /// Возвращает расшифровку [resultCode] из [KnddbResultCodes.describe], либо
  /// [resultMessage] от сервера, либо `null`.
  String? get resultDescription =>
      KnddbResultCodes.describe(resultCode) ?? resultMessage;

  /// Признак того, что упаковка не найдена — по HTTP-статусу (404) либо
  /// по коду результата 6022 («QR-код не найден») или 6039 («пара GTIN +
  /// серийный номер не найдена»).
  ///
  /// Удобно для проверки лекарства: сервер сообщает «не найдено» и через
  /// HTTP 404, и через код в конверте с HTTP 200.
  bool get isProductNotFound =>
      isNotFound ||
      resultCode == KnddbResultCodes.productWithQrCodeNotFound ||
      resultCode == KnddbResultCodes.productWithSerialNumberNotFound;

  /// Признак того, что упаковка не подходит для продажи (код 6023).
  ///
  /// Самая частая причина — повторная продажа уже проданной упаковки.
  bool get isProductNotSuitableForSale =>
      resultCode == KnddbResultCodes.productWithQrCodeNotSuitableForSale;

  /// Признак того, что упаковка или сущность не найдена (HTTP 404).
  bool get isNotFound => statusCode == 404;

  /// Признак отсутствия прав на операцию (HTTP 403).
  ///
  /// Права настраиваются администратором департамента лекарственных средств,
  /// поэтому такую ошибку показывайте как задачу администратору, а не как сбой
  /// приложения.
  bool get isForbidden => statusCode == 403;

  /// Признак ошибки входных данных (HTTP 400).
  bool get isBadRequest => statusCode == 400;

  /// Признак проблемы с авторизацией: HTTP 401 или 403.
  bool get isAuthenticationFailure => statusCode == 401 || statusCode == 403;

  /// Признак временной ошибки сервера (HTTP 5xx или 408) — запрос имеет смысл
  /// повторить.
  bool get isTransientFailure =>
      statusCode == null || statusCode == 408 || statusCode! >= 500;

  /// Признак конфликта состояния на сервере (HTTP 409).
  bool get isConflict => statusCode == 409;

  @override
  String toString() {
    final buffer = StringBuffer('KnddbApiException: $message');

    if (resultCode != null) {
      buffer.write('\n  resultCode: $resultCode');
      final description = resultDescription;
      if (description != null && description.isNotEmpty) {
        buffer.write(' — $description');
      }
    }
    if (statusCode != null) {
      buffer.write('\n  HTTP: $statusCode');
    }
    if (method != null || requestUri != null) {
      buffer.write('\n  Запрос: ${method ?? '?'} ${requestUri ?? '?'}');
    }
    if (traceId != null) {
      buffer.write('\n  traceId: $traceId');
    }
    if (errors != null && errors!.isNotEmpty) {
      buffer.write('\n  Ошибки валидации:');
      for (final entry in errors!.entries) {
        buffer.write('\n    - ${entry.key}: ${entry.value}');
      }
    }

    return buffer.toString();
  }
}

/// Исключение, возникающее при неудачной аутентификации: неверный логин или
/// пароль, истёкший и не подлежащий обновлению токен, отсутствие активной сессии.
///
/// Наследуется от [KnddbApiException], поэтому существующие обработчики
/// продолжат работать.
class KnddbAuthenticationException extends KnddbApiException {
  /// Создаёт исключение.
  const KnddbAuthenticationException(
    super.message, {
    super.method,
    super.requestUri,
    super.statusCode,
    super.responseBody,
    super.problemDetails,
    super.traceId,
    super.errors,
    super.cause,
    super.resultCode,
    super.resultMessage,
    this.oauthError,
    this.sessionKey,
  });

  /// Описание ошибки OAuth 2.0 от сервера (`invalid_grant`, `invalid_client`).
  final String? oauthError;

  /// Ключ сессии, для которой не удалось выполнить аутентификацию.
  final String? sessionKey;
}

/// Исключение конфигурации SDK: не задан адрес сервера, отсутствуют учётные
/// данные, для защищённого метода нет активной сессии.
///
/// Возникает до обращения к сети, поэтому повторять запрос не нужно — требуется
/// исправить настройки или сначала выполнить вход.
class KnddbConfigurationException extends KnddbApiException {
  /// Создаёт исключение.
  const KnddbConfigurationException(super.message, {super.cause});
}
