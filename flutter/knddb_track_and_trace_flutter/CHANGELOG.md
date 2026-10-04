# Журнал изменений

## 1.0.1

Обновление базового пакета `knddb_track_and_trace` до 1.0.1:

- исправлен вход: SDK запрашивал лишнюю область доступа `offline_access`,
  из-за чего сервер отклонял запрос целиком;
- исправлен разбор статуса маркировки: значение 0 («статус не задан»)
  встречается у 94 % препаратов и раньше обрабатывалось неверно.

Подробности — в журнале изменений базового пакета.

## 1.0.0

Первая публичная версия.

### Добавлено

- **`FlutterSecureSessionStorage`** — сохранение сессии KNMDB в защищённом
  хранилище платформы: Keychain (iOS, macOS), EncryptedSharedPreferences
  (Android), libsecret (Linux), DPAPI (Windows). Пароль не сохраняется.
- **`KnddbAuthController`** — контроллер состояния входа для Flutter-интерфейса
  с состояниями `idle`, `loading`, `authenticated`, `unauthenticated`, `error`
  и уведомлением слушателей через `ChangeNotifier`.
- Методы `restore`, `signIn`, `switchUser`, `signOut`, `retry`.
- Поддержка нескольких учётных записей KNMDB через ключи сессий.