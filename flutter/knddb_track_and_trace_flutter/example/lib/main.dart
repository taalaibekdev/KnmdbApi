import 'package:flutter/material.dart';
import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';
import 'package:knddb_track_and_trace_flutter/knddb_track_and_trace_flutter.dart';

/// Пример приложения Flutter: вход в KNMDB и проверка лекарства по QR-коду.
///
/// Запуск:
///
/// ```bash
/// cd flutter/knddb_track_and_trace_flutter/example
/// flutter run
/// ```
void main() => runApp(const KnddbExampleApp());

/// Демонстрационное приложение.
class KnddbExampleApp extends StatefulWidget {
  /// Создаёт приложение.
  ///
  /// [storage] позволяет подставить своё хранилище сессии; в тестах это
  /// избавляет от обращения к платформенному каналу защищённого хранилища.
  const KnddbExampleApp({super.key, this.storage});

  /// Хранилище сессии; по умолчанию — защищённое хранилище платформы.
  final KnddbSessionStorage? storage;

  @override
  State<KnddbExampleApp> createState() => _KnddbExampleAppState();
}

class _KnddbExampleAppState extends State<KnddbExampleApp> {
  late final KnddbAuthController _auth;

  @override
  void initState() {
    super.initState();

    // Тестовый контур. Для боевого укажите KnddbEnvironment.production.
    _auth = KnddbAuthController(
      options: KnddbClientOptions(
        environment: KnddbEnvironment.test,
        userAgent: 'knddb-flutter-example/1.0',
      ),
      storage: widget.storage,
    );

    // Пытаемся восстановить сохранённый вход.
    _auth.restore();
  }

  @override
  void dispose() {
    _auth.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => MaterialApp(
        title: 'KNMDB Track and Trace',
        theme: ThemeData(colorSchemeSeed: Colors.teal, useMaterial3: true),
        home: ListenableBuilder(
          listenable: _auth,
          builder: (context, _) => switch (_auth.state) {
            KnddbAuthState.idle ||
            KnddbAuthState.loading =>
              const Scaffold(body: Center(child: CircularProgressIndicator())),
            KnddbAuthState.authenticated => _HomeScreen(auth: _auth),
            _ => _LoginScreen(auth: _auth),
          },
        ),
      );
}

class _LoginScreen extends StatefulWidget {
  const _LoginScreen({required this.auth});

  final KnddbAuthController auth;

  @override
  State<_LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<_LoginScreen> {
  final _login = TextEditingController();
  final _password = TextEditingController();

  @override
  void dispose() {
    _login.dispose();
    _password.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final error = widget.auth.error;

    return Scaffold(
      appBar: AppBar(title: const Text('Вход в KNMDB')),
      body: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            TextField(
              controller: _login,
              decoration: const InputDecoration(labelText: 'Логин'),
            ),
            const SizedBox(height: 12),
            TextField(
              controller: _password,
              obscureText: true,
              decoration: const InputDecoration(labelText: 'Пароль'),
            ),
            const SizedBox(height: 24),
            FilledButton(
              onPressed: () => widget.auth.signIn(_login.text, _password.text),
              child: const Text('Войти'),
            ),
            if (error != null) ...[
              const SizedBox(height: 24),
              Text(
                error.isForbidden
                    ? '${error.message}\n\nОбратитесь к администратору '
                        'департамента лекарственных средств.'
                    : error.message,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
                textAlign: TextAlign.center,
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _HomeScreen extends StatelessWidget {
  const _HomeScreen({required this.auth});

  final KnddbAuthController auth;

  @override
  Widget build(BuildContext context) => Scaffold(
        appBar: AppBar(
          title: Text('KNMDB — ${auth.userName ?? ''}'),
          actions: [
            IconButton(
              tooltip: 'Выйти',
              onPressed: auth.signOut,
              icon: const Icon(Icons.logout),
            ),
          ],
        ),
        body: ListView(
          padding: const EdgeInsets.all(16),
          children: [
            const _SectionTitle('Проверка лекарства (без авторизации)'),
            _QrCodeField(auth: auth),
          ],
        ),
      );
}

class _SectionTitle extends StatelessWidget {
  const _SectionTitle(this.text);

  final String text;

  @override
  Widget build(BuildContext context) => Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Text(text, style: Theme.of(context).textTheme.titleMedium),
      );
}

class _QrCodeField extends StatefulWidget {
  const _QrCodeField({required this.auth});

  final KnddbAuthController auth;

  @override
  State<_QrCodeField> createState() => _QrCodeFieldState();
}

class _QrCodeFieldState extends State<_QrCodeField> {
  final _qrCode = TextEditingController();
  ProductInquiryResult? _result;
  String? _error;
  bool _loading = false;

  @override
  void dispose() {
    _qrCode.dispose();
    super.dispose();
  }

  Future<void> _check() async {
    setState(() {
      _loading = true;
      _error = null;
      _result = null;
    });

    try {
      final result =
          await widget.auth.client.productInquiryByQrCode(_qrCode.text.trim());
      setState(() => _result = result);
    } on KnddbApiException catch (error) {
      setState(() =>
          _error = error.isNotFound ? 'Упаковка не найдена' : error.message);
    } finally {
      setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final result = _result;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        TextField(
          controller: _qrCode,
          decoration:
              const InputDecoration(labelText: 'Содержимое Data Matrix'),
        ),
        const SizedBox(height: 12),
        FilledButton(
          onPressed: _loading ? null : _check,
          child: _loading
              ? const SizedBox(
                  height: 18,
                  width: 18,
                  child: CircularProgressIndicator(strokeWidth: 2),
                )
              : const Text('Проверить'),
        ),
        if (_error != null) ...[
          const SizedBox(height: 16),
          Text(_error!,
              style: TextStyle(color: Theme.of(context).colorScheme.error)),
        ],
        if (result != null) ...[
          const SizedBox(height: 16),
          _VerdictCard(result: result),
        ],
      ],
    );
  }
}

class _VerdictCard extends StatelessWidget {
  const _VerdictCard({required this.result});

  final ProductInquiryResult result;

  @override
  Widget build(BuildContext context) {
    final (icon, color) = switch (result.verificationVerdict) {
      'recalled' => (Icons.dangerous, Colors.red),
      'expired' => (Icons.warning_amber, Colors.orange),
      'notForSale' => (Icons.info_outline, Colors.blueGrey),
      _ => (Icons.verified, Colors.green),
    };

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(icon, color: color),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    result.verificationMessage,
                    style: Theme.of(context)
                        .textTheme
                        .titleSmall
                        ?.copyWith(color: color),
                  ),
                ),
              ],
            ),
            const Divider(height: 24),
            _Row('Препарат', result.productName),
            _Row('Производитель', result.manufacturerName),
            _Row('GTIN', result.gtin),
            _Row('Серийный номер', result.serialNumber),
            _Row('Партия', result.batchNumber),
            _Row('Годен до',
                result.expirationDate.toIso8601String().substring(0, 10)),
            _Row('Держатель', result.stakeHolderName),
            _Row('Состояние',
                '${result.productState.label} (${result.productState.value})'),
            _Row('Статус', result.productStatus.label),
            if (result.partialSaleRemainingAmount != null)
              _Row(
                  'Остаток в упаковке', '${result.partialSaleRemainingAmount}'),
          ],
        ),
      ),
    );
  }
}

class _Row extends StatelessWidget {
  const _Row(this.label, this.value);

  final String label;
  final String? value;

  @override
  Widget build(BuildContext context) {
    if (value == null || value!.isEmpty) {
      return const SizedBox.shrink();
    }

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 150,
            child: Text(label, style: const TextStyle(color: Colors.black54)),
          ),
          Expanded(child: Text(value!)),
        ],
      ),
    );
  }
}
