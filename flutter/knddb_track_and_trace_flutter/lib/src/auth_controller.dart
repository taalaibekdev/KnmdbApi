import 'package:flutter/foundation.dart';
import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';

import 'secure_session_storage.dart';

/// Состояние входа в приложении Flutter.
enum KnddbAuthState {
  /// Состояние ещё не определено: идёт проверка сохранённого входа.
  idle,

  /// Выполняется вход или восстановление.
  loading,

  /// Пользователь вошёл; можно вызывать методы API.
  authenticated,

  /// Пользователь не вошёл.
  unauthenticated,

  /// Последняя попытка входа завершилась ошибкой.
  error,
}

/// Контроллер состояния входа в KNMDB для Flutter-интерфейса.
///
/// Хранит [KnddbApiClient] с защищённым хранилищем сессии, отслеживает
/// состояние входа и уведомляет слушателей. Удобен для построения экранов
/// входа и переключения пользователя.
///
/// ```dart
/// final auth = KnddbAuthController(
///   options: KnddbClientOptions(environment: KnddbEnvironment.production),
/// );
///
/// await auth.restore();                 // при старте приложения
/// await auth.signIn(login, password);   // при входе
/// await auth.signOut();                 // при выходе
/// ```
class KnddbAuthController extends ChangeNotifier {
  /// Создаёт контроллер.
  ///
  /// [options] — настройки SDK; по умолчанию тестовый контур.
  /// [client] — готовый клиент (например, с собственным HTTP-клиентом для
  /// тестов). Если не передан, создаётся клиент с [FlutterSecureSessionStorage].
  /// [storage] — своё хранилище сессии вместо защищённого по умолчанию.
  KnddbAuthController({
    KnddbClientOptions? options,
    KnddbApiClient? client,
    KnddbSessionStorage? storage,
  }) : client = client ??
            KnddbApiClient(
              options: options ?? KnddbClientOptions(),
              storage: storage ?? FlutterSecureSessionStorage(),
            );

  /// Клиент SDK, с которым работает приложение.
  final KnddbApiClient client;

  KnddbAuthState _state = KnddbAuthState.idle;
  KnddbApiException? _error;

  /// Текущее состояние входа.
  KnddbAuthState get state => _state;

  /// Последняя ошибка входа или `null`.
  KnddbApiException? get error => _error;

  /// Логин текущего пользователя или `null`.
  String? get userName => client.currentUserName;

  /// Ключ текущей сессии.
  String get sessionKey => client.currentSessionKey;

  /// Признак того, что пользователь вошёл.
  bool get isAuthenticated => client.isAuthenticated;

  /// Пытается восстановить сохранённый вход.
  ///
  /// Вызывайте при старте приложения. Возвращает `true`, если вход
  /// восстановлен.
  Future<bool> restore() async {
    _setState(KnddbAuthState.loading);

    try {
      final restored = await client.restoreSession();
      _setState(restored
          ? KnddbAuthState.authenticated
          : KnddbAuthState.unauthenticated);
      return restored;
    } on KnddbApiException catch (error) {
      _fail(error);
      return false;
    }
  }

  /// Выполняет вход по логину и паролю.
  ///
  /// [sessionKey] — ключ сессии; задавайте его, если приложение обслуживает
  /// несколько учётных записей KNMDB. [storeCredentials] — сохранять ли пароль
  /// в памяти для автопродления.
  Future<bool> signIn(
    String userName,
    String password, {
    String? sessionKey,
    bool storeCredentials = true,
  }) async {
    _setState(KnddbAuthState.loading);

    try {
      await client.signIn(
        userName,
        password,
        sessionKey: sessionKey,
        storeCredentials: storeCredentials,
      );
      _setState(KnddbAuthState.authenticated);
      return true;
    } on KnddbApiException catch (error) {
      _fail(error);
      return false;
    }
  }

  /// Переключает приложение на другую сохранённую сессию.
  ///
  /// Если для ключа есть действующий токен, обращение к сети не выполняется.
  Future<bool> switchUser(String sessionKey,
      {KnddbCredentials? credentials}) async {
    _setState(KnddbAuthState.loading);

    try {
      await client.useSessionWithCredentials(sessionKey,
          credentials: credentials);
      _setState(KnddbAuthState.authenticated);
      return true;
    } on KnddbApiException catch (error) {
      _fail(error);
      return false;
    }
  }

  /// Завершает текущую сессию и очищает сохранённый вход.
  Future<void> signOut() async {
    _setState(KnddbAuthState.loading);

    await client.signOut();
    _error = null;
    _setState(KnddbAuthState.unauthenticated);
  }

  /// Повторяет последнюю неудачную попытку входа.
  ///
  /// Требуется, чтобы приложение само сохранило логин и пароль: SDK их
  /// намеренно не хранит после неудачного входа.
  Future<bool> retry(String userName, String password, {String? sessionKey}) =>
      signIn(userName, password,
          sessionKey: sessionKey, storeCredentials: false);

  /// Освобождает HTTP-клиент и слушателей.
  @override
  void dispose() {
    client.close();
    super.dispose();
  }

  void _setState(KnddbAuthState next) {
    if (next != KnddbAuthState.error) {
      _error = null;
    }

    _state = next;
    notifyListeners();
  }

  void _fail(KnddbApiException error) {
    _error = error;
    _state = KnddbAuthState.error;
    notifyListeners();
  }
}
