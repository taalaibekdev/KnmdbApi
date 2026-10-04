/// Пример использования Dart/Flutter SDK KNMDB Track and Trace.
///
/// Запуск:
///
/// ```bash
/// cd dart/knddb_track_and_trace
/// dart run example/knddb_track_and_trace_example.dart
/// ```
///
/// Пример работает на тестовом контуре. Замените логин и пароль на свои —
/// без них SDK вернёт ошибку аутентификации, и вы увидите, как она выглядит.
library;

// Пример — консольная программа, поэтому вывод через print здесь уместен.
// ignore_for_file: avoid_print

import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';

Future<void> main(List<String> args) async {
  // 1. Клиент. По умолчанию используется тестовый контур:
  //    https://testndbapi.med.kg/
  final client = KnddbApiClient(
    options: KnddbClientOptions(
      environment: KnddbEnvironment.test,
      userAgent: 'knddb-example/1.0',
    ),
  );

  print('Контур: ${client.environment?.displayName}');
  print('Адрес:  ${client.baseAddress}');
  print('');

  try {
    // 2. Проверка лекарства доступна без авторизации. Чтобы показать разбор
    //    ошибки, используем заведомо несуществующий код.
    await demoMedicineVerification(client);

    // 3. Для остальных методов нужен вход. Укажите свои логин и пароль.
    final login = args.isNotEmpty ? args[0] : 'ваш_логин';
    final password = args.length > 1 ? args[1] : 'ваш_пароль';

    print('Вход под учётной записью «$login»...');
    await client.signIn(login, password);
    print('Вход выполнен: ${client.currentUserName}');
    print('');

    // 4. Справочник организаций.
    await demoStakeholders(client);

    // 5. Остатки на складе.
    await demoStock(client);

    // 6. Смена пользователя «на ходу». Раскомментируйте, когда появится
    //    вторая учётная запись:
    //
    // await client.signIn('другой_логин', 'другой_пароль', sessionKey: 'user:2');
    // client.useSession('default');
    // await client.getStockInheldList();
    // client.useSession('user:2');
    // await client.getStockInheldList();

    // 7. Выход.
    await client.signOut();
    print('');
    print('Выход выполнен. Авторизован: ${client.isAuthenticated}');
  } on KnddbAuthenticationException catch (error) {
    print('Ошибка аутентификации: ${error.message}');
    if (error.oauthError != null) {
      print('Код ошибки OAuth: ${error.oauthError}');
    }
  } on KnddbConfigurationException catch (error) {
    print('Ошибка конфигурации: ${error.message}');
  } finally {
    // 8. SDK создал HTTP-клиент сам — закрываем его.
    client.close();
  }
}

/// Показывает работу проверки лекарства и обработку ошибок.
Future<void> demoMedicineVerification(KnddbApiClient client) async {
  print('--- Проверка лекарства по QR-коду (без авторизации) ---');

  try {
    final medicine = await client
        .productInquiryByQrCode('010460000000001721SN0000000000000001');

    // Сервер может вернуть resultCode 0 и actionResult: null — упаковка
    // с таким кодом не зарегистрирована. SDK возвращает null, а не пустой
    // объект, поэтому проверка обязательна.
    if (medicine == null) {
      print(
          'Упаковка с таким QR-кодом не найдена в системе KNMDB (пустой ответ).');
      print('');
      return;
    }

    print('Препарат:      ${medicine.productName}');
    print('Производитель: ${medicine.manufacturerName}');
    print('Партия:        ${medicine.batchNumber}');
    print(
        'Годен до:      ${medicine.expirationDate.toIso8601String().substring(0, 10)}');
    print('Держатель:     ${medicine.stakeHolderName}');
    print('');
    print('Вердикт:       ${medicine.verificationMessage}');
    print(
        'Состояние:     ${medicine.productState.label} (${medicine.productState.value})');
    print('Статус:        ${medicine.productStatus.label}');

    if (medicine.isExpired) {
      print('ВНИМАНИЕ: срок годности истёк');
    }
    if (medicine.isSuspendedOrRecalled) {
      print(
          'ВНИМАНИЕ: упаковка отозвана — ${medicine.suspendRecallInfo?.reason}');
    }

    print('');
    print('История движения:');
    for (final ProductInquiryHistory step
        in medicine.productInquiryHistory ?? const <ProductInquiryHistory>[]) {
      print('  ${step.stateDate.toIso8601String().substring(0, 10)}  '
          '${step.state.label.padRight(26)}  ${step.stakeHolder}');
    }
  } on KnddbApiException catch (error) {
    if (error.isNotFound) {
      print('Упаковка не найдена (404) — это ожидаемо для примера.');
    } else {
      print('Ошибка: ${error.message}');
    }
  }

  print('');
}

/// Выводит список организаций из справочника.
Future<void> demoStakeholders(KnddbApiClient client) async {
  print('--- Справочник организаций ---');

  final response = await client.getAllStakeholders();

  // Сервер может вернуть resultCode 0 и actionResult: null — SDK в этом случае
  // возвращает null, поэтому проверка обязательна.
  if (response == null) {
    print('Сервер не вернул данные справочника (пустой ответ).');
    print('');
    return;
  }

  print('Всего организаций: ${response.numberOfStakeholders}');
  for (final StakeholderInfo organization
      in (response.stakeholders ?? const <StakeholderInfo>[]).take(10)) {
    print('  ${organization.code.toString().padLeft(6)}  '
        '${organization.type.label.padRight(20)}  ${organization.name}');
  }

  print('');
}

/// Выводит сводку и детализацию остатков.
Future<void> demoStock(KnddbApiClient client) async {
  print('--- Остатки на складе ---');

  final summary = await client.getStockInheldList();

  if (summary == null) {
    print('Сервер не вернул остатки (пустой ответ).');
    print('');
    return;
  }

  final List<StockInheldSummaryInfo> positions =
      summary.stockInheldSummaryList ?? const <StockInheldSummaryInfo>[];

  if (positions.isEmpty) {
    print('Остатков нет.');
    print('');
    return;
  }

  for (final position in positions.take(5)) {
    print(
        '  ${position.gtin}  ${position.fullBrandName}  —  ${position.amount}');

    // Детализация по препарату: серийные номера и сроки годности.
    final details = await client.getStockInheldListByGtin(position.gtin!);
    for (final StockInheldInfo box
        in (details?.stockInheldList ?? const <StockInheldInfo>[]).take(3)) {
      print('      SN ${box.serialNumber}  партия ${box.batchNumber}  '
          'годен до ${box.expirationDate.toIso8601String().substring(0, 10)}  '
          '${box.currentState.label}');
    }
  }

  print('');
}
