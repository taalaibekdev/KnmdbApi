import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';
import 'package:knddb_track_and_trace_flutter/knddb_track_and_trace_flutter.dart';
import 'package:knddb_track_and_trace_flutter_example/main.dart';

void main() {
  testWidgets('приложение показывает экран входа, если сохранённого входа нет',
      (tester) async {
    await tester.pumpWidget(KnddbExampleApp(storage: _EmptyStorage()));
    await tester.pumpAndSettle();

    expect(find.text('Вход в KNMDB'), findsOneWidget);
    expect(find.byType(TextField), findsNWidgets(2));
    expect(find.text('Войти'), findsOneWidget);
  });

  testWidgets('контроллер входа доступен для тестов без платформенного канала',
      (tester) async {
    final controller = KnddbAuthController(
      options: KnddbClientOptions(),
      storage: _EmptyStorage(),
    );

    expect(controller.state, KnddbAuthState.idle);
    expect(controller.isAuthenticated, isFalse);

    controller.dispose();
  });
}

/// Хранилище без сохранённой сессии.
class _EmptyStorage implements KnddbSessionStorage {
  @override
  Future<void> save(KnddbSession session) async {}

  @override
  Future<KnddbSession?> load() async => null;

  @override
  Future<void> clear() async {}
}
