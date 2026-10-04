/// Пара учётных данных пользователя KNMDB.
///
/// Используется только на клиенте: логин и пароль уходят в теле запроса
/// `POST /connect/token` в формате `application/x-www-form-urlencoded`
/// и никогда не сохраняются на диск средствами SDK.
class KnddbCredentials {
  /// Создаёт пару учётных данных.
  const KnddbCredentials({required this.userName, required this.password});

  /// Логин пользователя KNMDB.
  final String userName;

  /// Пароль пользователя KNMDB.
  final String password;

  @override
  String toString() => 'KnddbCredentials(userName: $userName, password: ***)';
}
