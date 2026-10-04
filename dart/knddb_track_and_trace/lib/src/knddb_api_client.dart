import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:http/http.dart' as http;

import 'environment.dart';
import 'exceptions.dart';
import 'json.dart';
import 'models/auth/knddb_credentials.dart';
import 'models/requests/declaration_requests.dart';
import 'models/requests/inquiry_and_filter_requests.dart';
import 'models/requests/sales_and_stock_requests.dart';
import 'models/responses/declaration_responses.dart';
import 'models/responses/dictionary_responses.dart';
import 'models/responses/product_inquiry_responses.dart';
import 'options.dart';
import 'result_codes.dart';
import 'session.dart';
import 'session_storage.dart';
import 'token_store.dart';

/// Функция разбора тела ответа в модель.
typedef KnddbJsonDecoder<T> = T Function(Map<String, dynamic> json);

/// Клиент API KNMDB (Track and Trace) — единая точка входа в SDK.
///
/// Один экземпляр обслуживает любое количество пользователей: состояние входа
/// хранится в отдельных сессиях [KnddbSession], а не в самом клиенте.
///
/// **Быстрый старт:**
///
/// ```dart
/// final client = KnddbApiClient(
///   options: KnddbClientOptions(environment: KnddbEnvironment.test),
/// );
///
/// await client.signIn('логин', 'пароль');
///
/// final stakeholders = await client.getAllStakeholders();
/// print('Организаций: ${stakeholders.numberOfStakeholders}');
///
/// client.close();
/// ```
///
/// **Смена пользователя «на ходу»:**
///
/// ```dart
/// await client.signIn('pharmacist', 'secret', sessionKey: 'user:42');
/// await client.signIn('warehouse', 'secret', sessionKey: 'user:77');
///
/// client.useSession('user:42');            // дальше запросы идут от аптекаря
/// final stock = await client.getStockInheldList();
///
/// client.beginUserScope('user:77');        // временно — от кладовщика
/// try {
///   await client.getStockInheldList();
/// } finally {
///   client.endUserScope();
/// }
/// ```
///
/// **Сохранение входа между запусками приложения:** передайте в конструктор
/// реализацию [KnddbSessionStorage] и вызовите [restoreSession] при старте.
class KnddbApiClient {
  /// Создаёт клиент.
  ///
  /// [options] — настройки; по умолчанию тестовый контур.
  /// [httpClient] — свой HTTP-клиент. Если не передан, SDK создаёт собственный
  /// и закрывает его в [close]; переданный клиент SDK не закрывает.
  /// [storage] — хранилище сессии для сохранения входа между запусками.
  KnddbApiClient({
    KnddbClientOptions? options,
    http.Client? httpClient,
    KnddbSessionStorage? storage,
  })  : options = options ?? KnddbClientOptions(),
        _httpClient = httpClient ?? http.Client(),
        _ownsHttpClient = httpClient == null {
    this.options.validate();

    _tokenStore = KnddbTokenStore(
      options: this.options,
      client: _httpClient,
      storage: storage,
    );
  }

  /// Префикс путей Track and Trace.
  static const String _path = 'api/TrackAndTrace/';

  /// Настройки SDK.
  final KnddbClientOptions options;

  final http.Client _httpClient;
  final bool _ownsHttpClient;
  late final KnddbTokenStore _tokenStore;

  /// Стек сессий, открытых через [beginUserScope].
  final List<KnddbSession?> _scopeStack = <KnddbSession?>[];

  /// Текущий пользователь, выбранный методами входа и переключения.
  KnddbSession? _current;

  // -------------------------------------------------------------------------
  // Состояние входа
  // -------------------------------------------------------------------------

  /// Текущий пользователь или `null`, если вход не выполнен.
  KnddbSession? get currentSession => _current;

  /// Логин текущего пользователя или `null`.
  String? get currentUserName => _current?.userName;

  /// Ключ текущей сессии.
  String get currentSessionKey => _current?.key ?? options.defaultSessionKey;

  /// Признак того, что можно выполнить запрос: есть действующий токен либо
  /// сохранённые учётные данные.
  bool get isAuthenticated {
    final session = _current;
    if (session == null) {
      return false;
    }

    return session.isAccessTokenValid(margin: options.tokenExpirationMargin) ||
        (session.refreshToken ?? '').isNotEmpty ||
        session.credentials != null;
  }

  /// Контур, к которому подключён клиент.
  KnddbEnvironment? get environment => options.resolvedEnvironment;

  /// Адрес сервера, к которому обращается клиент.
  String get baseAddress => options.effectiveBaseUrl;

  /// Все сессии, известные SDK.
  Iterable<KnddbSession> get sessions => _tokenStore.sessions;

  /// Возвращает сессию по ключу или `null`.
  KnddbSession? findSession(String sessionKey) => _tokenStore.find(sessionKey);

  // -------------------------------------------------------------------------
  // Аутентификация и смена пользователя
  // -------------------------------------------------------------------------

  /// Восстанавливает сохранённую сессию и делает её текущей.
  ///
  /// Вызывайте при старте приложения, если передан [KnddbSessionStorage].
  /// Возвращает `true`, если сессия восстановлена и её токен ещё действителен
  /// либо может быть обновлён.
  Future<bool> restoreSession() async {
    final restored = await _tokenStore.restore();
    if (restored == null) {
      return false;
    }

    _current = restored;
    return true;
  }

  /// Выполняет вход и делает сессию текущей.
  ///
  /// [sessionKey] — ключ сессии; если не задан, используется ключ текущего
  /// пользователя. [storeCredentials] — сохранять ли логин и пароль в памяти
  /// для автоматического продления; укажите `false`, если приложение не должно
  /// держать пароль в памяти.
  ///
  /// Выбрасывает [KnddbAuthenticationException], если сервер отклонил логин
  /// или пароль.
  Future<KnddbSession> signIn(
    String userName,
    String password, {
    String? sessionKey,
    bool storeCredentials = true,
  }) async {
    final key = sessionKey ?? currentSessionKey;
    final session = await _tokenStore.signIn(
      key,
      KnddbCredentials(userName: userName, password: password),
      storeCredentials: storeCredentials,
    );

    _current = session;
    return session;
  }

  /// Переключает SDK на уже существующую сессию — без обращения к сети.
  ///
  /// Выбрасывает [KnddbConfigurationException], если сессия не найдена.
  KnddbSession useSession(String sessionKey) {
    final session = _tokenStore.find(sessionKey);
    if (session == null) {
      throw KnddbConfigurationException(
        'Сессия «$sessionKey» не найдена. Сначала выполните вход: '
        'signIn(...) или useSessionWithCredentials(...).',
      );
    }

    _current = session;
    return session;
  }

  /// Выполняет вход под указанным ключом и делает пользователя текущим.
  ///
  /// Если для ключа уже есть действующий токен и новые учётные данные
  /// не переданы, переключение выполняется без обращения к сети.
  Future<KnddbSession> useSessionWithCredentials(
    String sessionKey, {
    KnddbCredentials? credentials,
  }) async {
    final existing = _tokenStore.find(sessionKey);
    if (credentials == null &&
        existing != null &&
        existing.isAccessTokenValid(margin: options.tokenExpirationMargin)) {
      _current = existing;
      return existing;
    }

    final session = await _tokenStore.signIn(sessionKey, credentials);
    _current = session;
    return session;
  }

  /// Начинает область, внутри которой запросы выполняются от имени указанного
  /// пользователя.
  ///
  /// Прежний пользователь восстанавливается вызовом [endUserScope].
  /// Области можно вкладывать друг в друга.
  KnddbSession beginUserScope(String sessionKey) {
    // Прежний пользователь запоминается до переключения.
    _scopeStack.add(_current);

    try {
      return useSession(sessionKey);
    } on Object {
      _scopeStack.removeLast();
      rethrow;
    }
  }

  /// Начинает область от имени другого пользователя, выполняя вход
  /// при необходимости.
  Future<KnddbSession> beginUserScopeWithCredentials(
    String sessionKey, {
    KnddbCredentials? credentials,
  }) async {
    // Прежний пользователь запоминается до переключения.
    _scopeStack.add(_current);

    try {
      return await useSessionWithCredentials(sessionKey,
          credentials: credentials);
    } on Object {
      _scopeStack.removeLast();
      rethrow;
    }
  }

  /// Закрывает область, открытую [beginUserScope] или
  /// [beginUserScopeWithCredentials], и восстанавливает прежнего пользователя.
  void endUserScope() {
    if (_scopeStack.isEmpty) {
      return;
    }

    _current = _scopeStack.removeLast();
  }

  /// Завершает текущую сессию.
  ///
  /// Токены отзываются на сервере (best effort) и очищаются в памяти SDK.
  /// Ошибка серверного вызова не прерывает выход.
  Future<void> signOut() => signOutSession(currentSessionKey);

  /// Завершает указанную сессию.
  Future<void> signOutSession(String sessionKey) async {
    final session = _tokenStore.find(sessionKey);

    if (session != null && session.isAuthenticated) {
      try {
        await _send<void>(
          method: 'POST',
          path: 'connect/logout',
          sessionKey: sessionKey,
          decode: (_) {},
        );
      } on KnddbApiException {
        // Выход на сервере — операция «best effort»: локальные токены всё равно
        // удаляются.
      }
    }

    if (_current?.key == sessionKey) {
      _current = null;
    }

    await _tokenStore.signOut(sessionKey);
  }

  /// Принудительно обновляет токен доступа текущей сессии.
  Future<KnddbSession> refreshToken() async {
    final key = currentSessionKey;
    final session = await _tokenStore.getSession(key, forceRefresh: true);
    _current = session;
    return session;
  }

  // -------------------------------------------------------------------------
  // Справочники
  // -------------------------------------------------------------------------

  /// Список всех организаций (стейкхолдеров) системы.
  ///
  /// HTTP: `GET /api/TrackAndTrace/GetAllStakeholders`.
  ///
  /// Коды организаций из ответа используются в полях `stakeholderCode`,
  /// `fromStakeholder`, `toStakeholder` других методов.
  Future<GetAllStakeholdersResponse?> getAllStakeholders() => _send(
        method: 'GET',
        path: '${_path}GetAllStakeholders',
        decode: GetAllStakeholdersResponse.fromJson,
      );

  /// Список поддерживаемых типов QR-кодов (форматов Data Matrix).
  ///
  /// HTTP: `GET /api/TrackAndTrace/GetSupportedQRTypes`.
  ///
  /// **Устаревший метод.** Исключён из API департамента лекарственных средств,
  /// поэтому метод и связанные типы будут удалены из SDK, когда окончательно
  /// исчезнут из API. Новую интеграцию на него не закладывайте: значение
  /// `qrTypeId` согласуйте с департаментом и задайте константой в конфигурации.
  @Deprecated(
    'Метод GetSupportedQRTypes исключён из API департамента лекарственных средств '
    'и будет удалён из SDK. Значение qrTypeId согласуйте с департаментом.',
  )
  Future<GetSupportedQRTypesResponse?> getSupportedQrTypes() => _send(
        method: 'GET',
        path: '${_path}GetSupportedQRTypes',
        // Единственное место использования устаревшего типа — сам устаревший метод.
        // ignore: deprecated_member_use_from_same_package
        decode: GetSupportedQRTypesResponse.fromJson,
      );

  /// Справочник лекарственных средств, изменённых после указанной даты.
  ///
  /// HTTP: `POST /api/TrackAndTrace/GetMedicineList`.
  ///
  /// Для инкрементальной синхронизации передавайте максимальное значение
  /// `lastUpdate` из предыдущего ответа.
  Future<GetMedicineListResponse?> getMedicineList(
          [GetMedicineListRequest? request]) =>
      _send(
        method: 'POST',
        path: '${_path}GetMedicineList',
        body: (request ?? const GetMedicineListRequest()).toJson(),
        decode: GetMedicineListResponse.fromJson,
      );

  // -------------------------------------------------------------------------
  // Проверка лекарственного средства (без авторизации)
  // -------------------------------------------------------------------------

  /// Проверяет лекарственное средство по QR-коду (Data Matrix).
  ///
  /// HTTP: `POST /api/TrackAndTrace/ProductInquiryQRCode`.
  /// **Авторизация не требуется.**
  ///
  /// Если упаковка не найдена, выбрасывается [KnddbApiException]
  /// со [KnddbApiException.isNotFound] == `true`.
  Future<ProductInquiryResult?> productInquiryByQrCode(String qrCode) => _send(
        method: 'POST',
        path: '${_path}ProductInquiryQRCode',
        body: {'qrCode': qrCode},
        requireAuthentication: false,
        decode: ProductInquiryResult.fromJson,
      );

  /// Проверяет лекарственное средство по паре «GTIN + серийный номер».
  ///
  /// HTTP: `POST /api/TrackAndTrace/ProductInquiryGtinSn`.
  /// **Авторизация не требуется.**
  Future<ProductInquiryResult?> productInquiryByGtinSn(
    String gtin,
    String serialNumber,
  ) =>
      _send(
        method: 'POST',
        path: '${_path}ProductInquiryGtinSn',
        body: {'gtin': gtin, 'serialNumber': serialNumber},
        requireAuthentication: false,
        decode: ProductInquiryResult.fromJson,
      );

  // -------------------------------------------------------------------------
  // Ввод в оборот
  // -------------------------------------------------------------------------

  /// Регистрирует декларацию импорта партии лекарственного средства.
  ///
  /// HTTP: `POST /api/TrackAndTrace/ImportDeclaration`.
  ///
  /// После успешного вызова упаковки получают состояние `ProductState.import`.
  Future<ImportDeclarationResponse?> importDeclaration(
          ImportDeclarationRequest request) =>
      _send(
        method: 'POST',
        path: '${_path}ImportDeclaration',
        body: request.toJson(),
        decode: ImportDeclarationResponse.fromJson,
      );

  /// Регистрирует декларацию производства партии лекарственного средства.
  ///
  /// HTTP: `POST /api/TrackAndTrace/ProductionDeclaration`.
  Future<ProductionDeclarationResponse?> productionDeclaration(
    ProductionDeclarationRequest request,
  ) =>
      _send(
        method: 'POST',
        path: '${_path}ProductionDeclaration',
        body: request.toJson(),
        decode: ProductionDeclarationResponse.fromJson,
      );

  // -------------------------------------------------------------------------
  // Перемещение между организациями
  // -------------------------------------------------------------------------

  /// Создаёт декларацию перемещения упаковок другому стейкхолдеру.
  ///
  /// HTTP: `POST /api/TrackAndTrace/TransferDeclaration`.
  ///
  /// Получатель подтверждает приём методом [transferAccept].
  Future<TransferDeclarationResponse?> transferDeclaration(
          TransferDeclarationRequest request) =>
      _send(
        method: 'POST',
        path: '${_path}TransferDeclaration',
        body: request.toJson(),
        decode: TransferDeclarationResponse.fromJson,
      );

  /// Подтверждает приём перемещения получателем.
  ///
  /// HTTP: `POST /api/TrackAndTrace/TransferAccept`.
  Future<TransferDeclarationResponse?> transferAccept(int declarationId) =>
      _send(
        method: 'POST',
        path: '${_path}TransferAccept',
        body: {'declarationId': declarationId},
        decode: TransferDeclarationResponse.fromJson,
      );

  /// Отменяет ранее созданное перемещение.
  ///
  /// HTTP: `POST /api/TrackAndTrace/TransferCancel`.
  Future<TransferDeclarationCancelResponse?> transferCancel(
          int declarationId) =>
      _send(
        method: 'POST',
        path: '${_path}TransferCancel',
        body: {'declarationId': declarationId},
        decode: TransferDeclarationCancelResponse.fromJson,
      );

  /// Создаёт возвратное перемещение упаковок предыдущему держателю.
  ///
  /// HTTP: `POST /api/TrackAndTrace/TransferReturn`.
  Future<TransferDeclarationResponse?> transferReturn(
          TransferReturnRequest request) =>
      _send(
        method: 'POST',
        path: '${_path}TransferReturn',
        body: request.toJson(),
        decode: TransferDeclarationResponse.fromJson,
      );

  /// Отменяет возвратное перемещение.
  ///
  /// HTTP: `POST /api/TrackAndTrace/TransferReturnCancel`.
  Future<TransferDeclarationCancelResponse?> transferReturnCancel(
          int declarationId) =>
      _send(
        method: 'POST',
        path: '${_path}TransferReturnCancel',
        body: {'declarationId': declarationId},
        decode: TransferDeclarationCancelResponse.fromJson,
      );

  /// Полное содержимое одной декларации перемещения.
  ///
  /// HTTP: `GET /api/TrackAndTrace/GetTransferDeclaration`.
  Future<GetTransferDeclarationResponse?> getTransferDeclaration(
          int declarationId) =>
      _send(
        method: 'GET',
        path: '${_path}GetTransferDeclaration?declarationId=$declarationId',
        decode: GetTransferDeclarationResponse.fromJson,
      );

  /// Ищет декларации перемещения по необязательным фильтрам.
  ///
  /// HTTP: `POST /api/TrackAndTrace/GetTransferListByFilter`.
  Future<GetTransferListByFilterResponse?> getTransferListByFilter([
    GetTransferListByFilterRequest? request,
  ]) =>
      _send(
        method: 'POST',
        path: '${_path}GetTransferListByFilter',
        body: (request ?? const GetTransferListByFilterRequest()).toJson(),
        decode: GetTransferListByFilterResponse.fromJson,
      );

  // -------------------------------------------------------------------------
  // Складской учёт
  // -------------------------------------------------------------------------

  /// Ставит упаковки на складской учёт.
  ///
  /// HTTP: `POST /api/TrackAndTrace/StockDeclaration`.
  Future<StockDeclarationResponse?> stockDeclaration(
          StockDeclarationRequest request) =>
      _send(
        method: 'POST',
        path: '${_path}StockDeclaration',
        body: request.toJson(),
        decode: StockDeclarationResponse.fromJson,
      );

  /// Отменяет складскую декларацию.
  ///
  /// HTTP: `POST /api/TrackAndTrace/StockDeclarationCancel`.
  ///
  /// Проверяйте [StockDeclarationCancelResponse.isSuccess]: сервер может вернуть
  /// HTTP 200 и `isSuccess == false` с пояснением в `message`.
  Future<StockDeclarationCancelResponse?> stockDeclarationCancel(
          int declarationId) =>
      _send(
        method: 'POST',
        path: '${_path}StockDeclarationCancel',
        body: {'declarationId': declarationId},
        decode: StockDeclarationCancelResponse.fromJson,
      );

  /// Сводные остатки по всем доступным препаратам.
  ///
  /// HTTP: `POST /api/TrackAndTrace/GetStockInheldList`.
  Future<GetStockInheldListResponse?> getStockInheldList() => _send(
        method: 'POST',
        path: '${_path}GetStockInheldList',
        decode: GetStockInheldListResponse.fromJson,
      );

  /// Детальные остатки по конкретному GTIN (упаковка за упаковкой).
  ///
  /// HTTP: `POST /api/TrackAndTrace/GetStockInheldListByGtin`.
  Future<GetStockInheldListByGtinResponse?> getStockInheldListByGtin(
          String gtin) =>
      _send(
        method: 'POST',
        path: '${_path}GetStockInheldListByGtin',
        body: {'gtin': gtin},
        decode: GetStockInheldListByGtinResponse.fromJson,
      );

  /// Сведения о частично проданной упаковке.
  ///
  /// HTTP: `POST /api/TrackAndTrace/GetPartialSaleInfo`.
  Future<GetPartialSaleInfoResponse?> getPartialSaleInfo(String qrCode) =>
      _send(
        method: 'POST',
        path: '${_path}GetPartialSaleInfo',
        body: {'qrCode': qrCode},
        decode: GetPartialSaleInfoResponse.fromJson,
      );

  // -------------------------------------------------------------------------
  // Реализация
  // -------------------------------------------------------------------------

  /// Создаёт декларацию реализации (продажи или расхода) упаковок.
  ///
  /// HTTP: `POST /api/TrackAndTrace/SalesDeclaration`.
  Future<SalesDeclarationResponse?> salesDeclaration(
          SalesDeclarationRequest request) =>
      _send(
        method: 'POST',
        path: '${_path}SalesDeclaration',
        body: request.toJson(),
        decode: SalesDeclarationResponse.fromJson,
      );

  /// Отменяет декларацию реализации целиком.
  ///
  /// HTTP: `POST /api/TrackAndTrace/SalesDeclarationCancel`.
  Future<SalesDeclarationResponse?> salesDeclarationCancel(int declarationId) =>
      _send(
        method: 'POST',
        path: '${_path}SalesDeclarationCancel',
        body: {'declarationId': declarationId},
        decode: SalesDeclarationResponse.fromJson,
      );

  /// Отменяет реализацию одной упаковки по её QR-коду.
  ///
  /// HTTP: `POST /api/TrackAndTrace/SalesDeclarationBoxCancel`.
  Future<SalesDeclarationResponse?> salesDeclarationBoxCancel(String qrCode) =>
      _send(
        method: 'POST',
        path: '${_path}SalesDeclarationBoxCancel',
        body: {'qrCode': qrCode},
        decode: SalesDeclarationResponse.fromJson,
      );

  // -------------------------------------------------------------------------
  // Выбытие (деактивация)
  // -------------------------------------------------------------------------

  /// Деактивирует (списывает) упаковки с указанием типа расходной операции.
  ///
  /// HTTP: `POST /api/TrackAndTrace/DeactivateDeclaration`.
  Future<DeactivateDeclarationResponse?> deactivateDeclaration(
    DeactivateDeclarationRequest request,
  ) =>
      _send(
        method: 'POST',
        path: '${_path}DeactivateDeclaration',
        body: request.toJson(),
        decode: DeactivateDeclarationResponse.fromJson,
      );

  // -------------------------------------------------------------------------
  // Освобождение ресурсов
  // -------------------------------------------------------------------------

  /// Закрывает HTTP-клиент, если его создал SDK.
  void close() {
    if (_ownsHttpClient) {
      _httpClient.close();
    }
  }

  // -------------------------------------------------------------------------
  // Транспорт
  // -------------------------------------------------------------------------

  /// Выполняет запрос, разбирает ответ и преобразует ошибки в исключения.
  Future<T?> _send<T>({
    required String method,
    required String path,
    required KnddbJsonDecoder<T> decode,
    Map<String, dynamic>? body,
    bool requireAuthentication = true,
    String? sessionKey,
  }) async {
    final baseUrl = options.validate();
    final uri = Uri.parse(baseUrl).resolve(path);
    final key = sessionKey ?? currentSessionKey;

    var session = await _tokenStore.getSession(
      key,
      requireAuthentication: requireAuthentication,
    );

    var authenticationRetried = false;

    while (true) {
      final headers = <String, String>{
        'accept': 'application/json',
        if (body != null) 'content-type': 'application/json; charset=utf-8',
        if ((session.accessToken ?? '').isNotEmpty)
          'authorization': '${session.tokenType} ${session.accessToken}',
        // Заголовок user-agent отправляется всегда: без него сервер KNMDB
        // не может сохранить запись о входе и отвечает кодом результата 2.
        'user-agent': options.userAgent ?? KnddbClientOptions.defaultUserAgent,
      };

      http.Response response;
      try {
        final request = http.Request(method, uri)..headers.addAll(headers);
        if (body != null) {
          request.body = jsonEncode(toJsonValue(body));
        }

        final streamed =
            await _httpClient.send(request).timeout(options.timeout);
        response =
            await http.Response.fromStream(streamed).timeout(options.timeout);
      } on TimeoutException catch (error) {
        throw KnddbApiException(
          'Превышен таймаут (${options.timeout.inSeconds} с) при выполнении '
          'запроса $method $path. Увеличьте KnddbClientOptions.timeout, если '
          'операция обрабатывает большой объём данных.',
          method: method,
          requestUri: path,
          cause: error,
        );
      } on SocketException catch (error) {
        throw KnddbApiException(
          'Не удалось выполнить запрос $method $path к серверу KNMDB по адресу '
          '«$uri». Проверьте сетевое подключение и адрес сервера.',
          method: method,
          requestUri: path,
          cause: error,
        );
      } on http.ClientException catch (error) {
        throw KnddbApiException(
          'Сетевой сбой при выполнении запроса $method $path: ${error.message}',
          method: method,
          requestUri: path,
          cause: error,
        );
      }

      // Токен мог истечь раньше заявленного срока: обновляем и повторяем один раз.
      if (response.statusCode == 401 &&
          requireAuthentication &&
          !authenticationRetried) {
        authenticationRetried = true;
        session = await _tokenStore.getSession(
          key,
          requireAuthentication: requireAuthentication,
          forceRefresh: true,
        );
        continue;
      }

      if (response.statusCode < 200 || response.statusCode >= 300) {
        throw buildApiException(
          statusCode: response.statusCode,
          body: response.body,
          method: method,
          path: path,
          sessionKey: key,
          headers: response.headers,
        );
      }

      if (response.body.isEmpty) {
        // Метод без полезного тела ответа (например, выход из системы).
        return decode(const <String, dynamic>{});
      }

      final decoded = jsonDecode(utf8.decode(response.bodyBytes));
      final root = asMap(decoded) ?? const <String, dynamic>{};

      // Сервер KNMDB оборачивает все ответы Track and Trace в конверт
      // { resultCode, resultMessage, actionResult }. Ошибки бизнес-логики
      // приходят в том же конверте и с HTTP-статусом 200, поэтому проверять
      // нужно resultCode, а не статус.
      final resultCode = asInt(root['resultCode']);

      if (resultCode == null) {
        // Конверта нет — сервер вернул данные напрямую.
        return decode(root);
      }

      if (resultCode != KnddbResultCodes.success) {
        throw buildBusinessException(
          resultCode: resultCode,
          resultMessage: root['resultMessage'] as String?,
          method: method,
          path: path,
          sessionKey: key,
          body: response.body,
        );
      }

      final payload = asMap(root['actionResult']);

      // Сервер вернул resultCode 0 и actionResult: null — данных нет.
      // Возвращаем null, а не пустой объект, чтобы вызывающая сторона
      // не принимала отсутствие данных за валидный результат.
      if (payload == null) {
        return null;
      }

      return decode(payload);
    }
  }
}
