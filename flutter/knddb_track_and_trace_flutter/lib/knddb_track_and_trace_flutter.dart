/// Интеграция Dart/Flutter SDK KNMDB Track and Trace с Flutter.
///
/// Пакет добавляет к базовому SDK две вещи:
///
/// 1. **Защищённое хранение сессии** ([FlutterSecureSessionStorage]) — вход
///    переживает перезапуск приложения. Токены хранятся в Keychain на iOS
///    и macOS, в EncryptedSharedPreferences на Android, в libsecret на Linux
///    и в DPAPI на Windows.
/// 2. **Контроллер состояния входа** ([KnddbAuthController]) — удобен для
///    Flutter-интерфейса: хранит состояние (`idle`, `loading`, `authenticated`,
///    `error`) и уведомляет слушателей через [ChangeNotifier].
///
/// ## Быстрый старт
///
/// ```dart
/// import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';
/// import 'package:knddb_track_and_trace_flutter/knddb_track_and_trace_flutter.dart';
///
/// final auth = KnddbAuthController(
///   options: KnddbClientOptions(environment: KnddbEnvironment.production),
/// );
///
/// // При запуске приложения — попытка восстановить вход.
/// await auth.restore();
///
/// // Вход по логину и паролю.
/// await auth.signIn(login, password);
///
/// // В интерфейсе — слушаем состояние.
/// ListenableBuilder(
///   listenable: auth,
///   builder: (context, _) => switch (auth.state) {
///     KnddbAuthState.loading => const CircularProgressIndicator(),
///     KnddbAuthState.authenticated => const HomeScreen(),
///     _ => LoginScreen(onSubmit: auth.signIn),
///   },
/// );
///
/// // Клиент для вызовов API.
/// final stock = await auth.client.getStockInheldList();
/// ```
library;

export 'src/auth_controller.dart' show KnddbAuthController, KnddbAuthState;
export 'src/secure_session_storage.dart' show FlutterSecureSessionStorage;
