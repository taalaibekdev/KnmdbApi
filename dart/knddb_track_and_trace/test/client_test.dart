import 'dart:convert';

import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';
import 'package:test/test.dart';

import 'stub_http_client.dart';

void main() {
  group('Вход', () {
    test('отправляет форму grant_type=password', () async {
      final (:client, :http) = createClient(
        responder: (request, index) => tokenResponse(),
      );

      await client.signIn('pharmacist', 'secret');

      final recorded = http.requests.single;

      expect(recorded.method, 'POST');
      expect(recorded.path, '/connect/token');

      final form = Uri.splitQueryString(recorded.body!);
      expect(form['grant_type'], 'password');
      expect(form['username'], 'pharmacist');
      expect(form['password'], 'secret');
      expect(form['scope'], 'api');

      client.close();
    });

    test('неверный логин вызывает KnddbAuthenticationException', () async {
      final (:client, :http) = createClient(
        responder: (request, index) => jsonResponse(
          <String, Object?>{
            'error': 'invalid_grant',
            'error_description': 'The username/password couple is invalid.',
          },
          statusCode: 400,
        ),
      );

      await expectLater(
        client.signIn('user', 'wrong'),
        throwsA(
          isA<KnddbAuthenticationException>()
              .having(
                  (error) => error.oauthError, 'oauthError', 'invalid_grant')
              .having((error) => error.statusCode, 'statusCode', 400),
        ),
      );

      expect(client.isAuthenticated, isFalse);
      client.close();
    });

    test('без входа защищённый метод сообщает об отсутствии учётных данных',
        () async {
      final (:client, :http) =
          createClient(responder: (request, index) => jsonResponse({}));

      await expectLater(
        client.getAllStakeholders(),
        throwsA(
          isA<KnddbAuthenticationException>().having(
              (error) => error.message, 'message', contains('учётные данные')),
        ),
      );

      expect(http.requests, isEmpty);
      client.close();
    });
  });

  group('Запросы', () {
    test('защищённый метод получает заголовок Authorization', () async {
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse(accessToken: 'token-a');
          }
          return envelope(
            '{ "numberOfStakeholders": 1, '
            '"stakeholders": [ { "code": 1, "name": "A", "type": 5 } ] }',
          );
        },
      );

      await client.signIn('user', 'pwd');
      final response = await client.getAllStakeholders();

      expect(response, isNotNull);
      expect(response!.numberOfStakeholders, 1);
      expect(response.stakeholders![0].type, StakeholderType.pharmacy);

      final apiRequest = http.requests[1];
      expect(apiRequest.method, 'GET');
      expect(apiRequest.path, '/api/TrackAndTrace/GetAllStakeholders');
      expect(apiRequest.authorization, 'Bearer token-a');

      client.close();
    });

    test('токен запрашивается один раз на несколько запросов', () async {
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          return envelope('{ "numberOfStakeholders": 0 }');
        },
      );

      await client.signIn('user', 'pwd');
      await client.getAllStakeholders();
      await client.getMedicineList();

      expect(http.requests, hasLength(3));
      expect(
          http.requests.where((r) => r.path == '/connect/token'), hasLength(1));

      client.close();
    });

    test('анонимный метод выполняется без токена', () async {
      final (:client, :http) = createClient(
        responder: (request, index) => envelope(
          '{ "productBoxId": 1, "productPackageItemId": "x", '
          '"productionDate": "2025-01-01T00:00:00Z", '
          '"expirationDate": "2027-01-01T00:00:00Z", '
          '"isExpired": false, "isAvailableForSale": true, '
          '"isSuspendedOrRecalled": false, "productStatus": 1, "productState": 3 }',
        ),
      );

      final result = await client.productInquiryByQrCode('01...');

      expect(result, isNotNull);
      expect(result!.isAvailableForSale, isTrue);
      expect(result.productState, ProductState.sales);
      expect(http.requests, hasLength(1));
      expect(http.requests.single.authorization, isNull);
      expect(
          http.requests.single.path, '/api/TrackAndTrace/ProductInquiryQRCode');

      client.close();
    });

    test('метод с параметром формирует строку запроса', () async {
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          return envelope('{ "declarationId": 99 }');
        },
      );

      await client.signIn('user', 'pwd');
      await client.getTransferDeclaration(99);

      final recorded = http.requests.last;

      expect(recorded.path, '/api/TrackAndTrace/GetTransferDeclaration');
      expect(recorded.query, '?declarationId=99');

      client.close();
    });

    test('тело запроса уходит с корректным Content-Type и датами', () async {
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          return envelope('{ "declarationId": 5 }');
        },
      );

      await client.signIn('user', 'pwd');
      await client.importDeclaration(
        ImportDeclarationRequest(
          gtin: '04600000000017',
          batchNo: 'B-2025-01',
          expirationDate: DateTime.utc(2027, 1, 1),
          price: 120.5,
          productionDate: DateTime.utc(2025, 1, 1),
          documentDate: DateTime.utc(2025, 2, 1),
          documentNo: 'ГТД-1',
          qrTypeId: 1,
          qrCodes: const ['01...'],
        ),
      );

      final recorded = http.requests.last;
      final body = recorded.json();

      expect(recorded.path, '/api/TrackAndTrace/ImportDeclaration');
      expect(body['expirationDate'], '2027-01-01');
      expect(body['gtin'], '04600000000017');

      client.close();
    });
  });

  group('Смена пользователя', () {
    test('два пользователя работают со своими токенами', () async {
      var tokenCounter = 0;

      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            tokenCounter++;
            return tokenResponse(
                accessToken: 'token-$tokenCounter',
                refreshToken: 'r$tokenCounter');
          }
          return envelope('{ "numberOfStakeholders": 0 }');
        },
      );

      final first =
          await client.signIn('user-a', 'pwd-a', sessionKey: 'user:a');
      final second =
          await client.signIn('user-b', 'pwd-b', sessionKey: 'user:b');

      expect(first.accessToken, 'token-1');
      expect(second.accessToken, 'token-2');

      client.useSession('user:a');
      await client.getAllStakeholders();
      expect(http.requests.last.authorization, 'Bearer token-1');

      client.useSession('user:b');
      await client.getAllStakeholders();
      expect(http.requests.last.authorization, 'Bearer token-2');

      // Смена пользователя не приводит к повторному запросу токена.
      expect(
          http.requests.where((r) => r.path == '/connect/token'), hasLength(2));

      client.close();
    });

    test('область пользователя временно переключает токен', () async {
      var tokenCounter = 0;

      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            tokenCounter++;
            return tokenResponse(
                accessToken: 'token-$tokenCounter',
                refreshToken: 'r$tokenCounter');
          }
          return envelope('{ "numberOfStakeholders": 0 }');
        },
      );

      final defaultSession = await client.signIn('user-default', 'pwd');
      final otherSession =
          await client.signIn('user-other', 'pwd', sessionKey: 'user:other');

      client.useSession('default');

      client.beginUserScope('user:other');
      await client.getAllStakeholders();
      expect(http.requests.last.authorization,
          'Bearer ${otherSession.accessToken}');

      client.endUserScope();
      await client.getAllStakeholders();
      expect(http.requests.last.authorization,
          'Bearer ${defaultSession.accessToken}');
      expect(client.currentSessionKey, 'default');

      client.close();
    });

    test('useSession для несуществующей сессии сообщает об ошибке', () {
      final (:client, :http) =
          createClient(responder: (request, index) => jsonResponse({}));

      expect(
        () => client.useSession('unknown'),
        throwsA(
          isA<KnddbConfigurationException>().having(
              (error) => error.message, 'message', contains('не найдена')),
        ),
      );

      client.close();
    });

    test('useSessionWithCredentials переиспользует действующий токен',
        () async {
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse(accessToken: 'token-a');
          }
          return envelope('{ "numberOfStakeholders": 0 }');
        },
      );

      await client.signIn('user', 'pwd', sessionKey: 'user:a');
      await client.useSessionWithCredentials('user:a');
      await client.getAllStakeholders();

      expect(
          http.requests.where((r) => r.path == '/connect/token'), hasLength(1));

      client.close();
    });

    test('выход очищает сессию, даже если сервер вернул ошибку', () async {
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          if (request.url.path == '/connect/logout') {
            return jsonResponse(<String, Object?>{'title': 'Error'},
                statusCode: 500);
          }
          return envelope('{ "numberOfStakeholders": 0 }');
        },
      );

      await client.signIn('user', 'pwd');
      expect(client.isAuthenticated, isTrue);

      await client.signOut();

      expect(client.isAuthenticated, isFalse);
      expect(client.currentSession, isNull);
      expect(client.findSession('default'), isNull);

      client.close();
    });
  });

  group('Обновление токена', () {
    test('ответ 401 приводит к обновлению токена и повтору запроса', () async {
      var apiAttempts = 0;

      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            final form = Uri.splitQueryString(request.body);
            return tokenResponse(
              accessToken: form['grant_type'] == 'refresh_token'
                  ? 'refreshed-token'
                  : 'initial-token',
              refreshToken: 'refresh-2',
            );
          }

          apiAttempts++;
          if (apiAttempts == 1) {
            return jsonResponse(<String, Object?>{}, statusCode: 401);
          }
          return envelope('{ "numberOfStakeholders": 7 }');
        },
      );

      await client.signIn('user', 'pwd');
      final response = await client.getAllStakeholders();

      expect(response, isNotNull);
      expect(response!.numberOfStakeholders, 7);
      expect(apiAttempts, 2);

      final apiRequests = http.requests
          .where((r) => r.path.startsWith('/api/'))
          .toList(growable: false);

      expect(apiRequests, hasLength(2));
      expect(apiRequests[1].authorization, 'Bearer refreshed-token');

      client.close();
    });

    test('токен обновляется заранее при истечении', () async {
      var tokenCounter = 0;

      final (:client, :http) = createClient(
        tokenExpirationMargin: const Duration(hours: 2),
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            tokenCounter++;
            return tokenResponse(
                accessToken: 'token-$tokenCounter',
                refreshToken: 'r$tokenCounter');
          }
          return envelope('{ "numberOfStakeholders": 0 }');
        },
      );

      // Срок действия 3600 секунд, запас 2 часа — токен сразу считается
      // истекающим, поэтому второй запрос вызовет обновление по refresh_token.
      await client.signIn('user', 'pwd');
      await client.getAllStakeholders();

      expect(tokenCounter, 2);

      final refreshRequest =
          http.requests.lastWhere((r) => r.path == '/connect/token');

      expect(
        Uri.splitQueryString(refreshRequest.body!)['grant_type'],
        'refresh_token',
      );

      client.close();
    });
  });

  group('Ошибки', () {
    test('403 содержит подсказку обратиться к администратору департамента',
        () async {
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          return problemResponse(
            statusCode: 403,
            detail: 'Роль не назначена.',
          );
        },
      );

      await client.signIn('user', 'pwd');

      await expectLater(
        client.getAllStakeholders(),
        throwsA(
          isA<KnddbApiException>()
              .having((error) => error.statusCode, 'statusCode', 403)
              .having((error) => error.isForbidden, 'isForbidden', isTrue)
              .having((error) => error.message, 'message',
                  contains('департамента лекарственных средств'))
              .having((error) => error.message, 'message',
                  contains('Роль не назначена.')),
        ),
      );

      client.close();
    });

    test('404 распознаётся как isNotFound', () async {
      final (:client, :http) = createClient(
        responder: (request, index) =>
            problemResponse(statusCode: 404, title: 'Not Found'),
      );

      await expectLater(
        client.productInquiryByQrCode('unknown'),
        throwsA(
          isA<KnddbApiException>()
              .having((error) => error.isNotFound, 'isNotFound', isTrue)
              .having((error) => error.statusCode, 'statusCode', 404),
        ),
      );

      client.close();
    });

    test('ошибки валидации извлекаются в errors', () async {
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          return problemResponse(
            statusCode: 400,
            title: 'One or more validation errors occurred.',
            extensions: {
              'errors': {
                'qrCodes': ['Поле обязательно'],
                'gtin': ['Неверная длина'],
              },
            },
          );
        },
      );

      await client.signIn('user', 'pwd');

      await expectLater(
        client.getMedicineList(),
        throwsA(
          isA<KnddbApiException>()
              .having((error) => error.isBadRequest, 'isBadRequest', isTrue)
              .having((error) => error.errors, 'errors', isNotNull)
              .having((error) => error.errors!['qrCodes'], 'qrCodes',
                  'Поле обязательно')
              .having(
                  (error) => error.errors!['gtin'], 'gtin', 'Неверная длина'),
        ),
      );

      client.close();
    });

    test('traceId попадает в исключение и сообщение', () async {
      final (:client, :http) = createClient(
        responder: (request, index) => problemResponse(
          statusCode: 500,
          detail: 'Внутренняя ошибка',
          headers: {'x-request-id': 'req-42'},
        ),
      );

      await expectLater(
        client.productInquiryByQrCode('01...'),
        throwsA(
          isA<KnddbApiException>()
              .having((error) => error.traceId, 'traceId', 'req-42')
              .having((error) => error.isTransientFailure, 'isTransientFailure',
                  isTrue)
              .having((error) => error.message, 'message',
                  contains('traceId: req-42')),
        ),
      );

      client.close();
    });

    test('тело ответа и ProblemDetails сохраняются в исключении', () async {
      final (:client, :http) = createClient(
        responder: (request, index) => problemResponse(
          statusCode: 409,
          detail: 'Упаковка уже передана',
          title: 'Conflict',
        ),
      );

      await expectLater(
        client.productInquiryByQrCode('01...'),
        throwsA(
          isA<KnddbApiException>()
              .having((error) => error.isConflict, 'isConflict', isTrue)
              .having(
                  (error) => error.problemDetails, 'problemDetails', isNotNull)
              .having((error) => error.problemDetails!.detail, 'detail',
                  'Упаковка уже передана')
              .having((error) => error.responseBody, 'responseBody',
                  contains('Conflict')),
        ),
      );

      client.close();
    });
  });

  group('Конверт ответа KNMDB', () {
    test('успешный ответ разбирается из actionResult', () async {
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          return envelope(
            '{ "numberOfStakeholders": 2, "stakeholders": ['
            '{ "code": 100, "name": "Тест_склад", "type": 7 },'
            '{ "code": 205, "name": "тест аптека", "type": 5 } ] }',
          );
        },
      );

      await client.signIn('user', 'pwd');
      final response = await client.getAllStakeholders();

      expect(response, isNotNull);
      expect(response!.numberOfStakeholders, 2);
      expect(response.stakeholders![0].type, StakeholderType.warehouse);
      expect(response.stakeholders![1].type, StakeholderType.pharmacy);

      client.close();
    });

    test('resultCode 0 с actionResult: null даёт null, а не пустой объект',
        () async {
      // Проверено на живом тестовом контуре: на несуществующий QR-код сервер
      // отвечает HTTP 200, resultCode 0 и actionResult: null. SDK обязан
      // вернуть null, иначе приложение примет отсутствие данных за валидный
      // результат и покажет пустую карточку лекарства.
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          return envelope('null');
        },
      );

      await client.signIn('user', 'pwd');

      final result = await client.productInquiryByQrCode('01...');
      expect(result, isNull);

      final stakeholders = await client.getAllStakeholders();
      expect(stakeholders, isNull);

      client.close();
    });

    test('ответ без конверта по-прежнему поддерживается', () async {
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          return jsonResponse(<String, Object?>{
            'numberOfStakeholders': 5,
            'stakeholders': <Object?>[],
          });
        },
      );

      await client.signIn('user', 'pwd');
      final response = await client.getAllStakeholders();

      expect(response, isNotNull);
      expect(response!.numberOfStakeholders, 5);

      client.close();
    });
  });

  group('Ошибки бизнес-логики с HTTP 200', () {
    test('код 6022 «упаковка не найдена» распознаётся', () async {
      const serverMessage = 'Product with QRCode 0104600000000017 not found';

      final (:client, :http) = createClient(
        responder: (request, index) => envelopeError(6022, serverMessage),
      );

      await expectLater(
        client.productInquiryByQrCode('0104600000000017'),
        throwsA(
          isA<KnddbApiException>()
              // Ключевое: ошибка пришла с HTTP 200, по статусу её не отличить.
              .having((error) => error.statusCode, 'statusCode', 200)
              .having((error) => error.resultCode, 'resultCode', 6022)
              .having((error) => error.resultMessage, 'resultMessage',
                  serverMessage)
              .having(
                  (error) => error.isBusinessError, 'isBusinessError', isTrue)
              .having((error) => error.isProductNotFound, 'isProductNotFound',
                  isTrue)
              .having((error) => error.isProductNotSuitableForSale,
                  'isProductNotSuitableForSale', isFalse)
              .having(
                  (error) => error.message, 'message', contains('не найдена'))
              .having(
                  (error) => error.message, 'message', contains(serverMessage)),
        ),
      );

      client.close();
    });

    test('код 6023 «повторная продажа» распознаётся', () async {
      const serverMessage =
          'Product with QRCode 0104600000000017 is not suitable for sale';

      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          return envelopeError(6023, serverMessage);
        },
      );

      await client.signIn('user', 'pwd');

      await expectLater(
        client.salesDeclaration(
          SalesDeclarationRequest(
            isPharmacyConsumption: true,
            details: const [
              SalesDeclarationDetail(qrCode: '0104600000000017', price: 165),
            ],
          ),
        ),
        throwsA(
          isA<KnddbApiException>()
              .having((error) => error.resultCode, 'resultCode', 6023)
              .having((error) => error.isProductNotSuitableForSale,
                  'isProductNotSuitableForSale', isTrue)
              .having((error) => error.isProductNotFound, 'isProductNotFound',
                  isFalse)
              .having((error) => error.message, 'message',
                  contains('не подходит для продажи')),
        ),
      );

      client.close();
    });

    test('неизвестный код результата передаётся как есть', () async {
      final (:client, :http) = createClient(
        responder: (request, index) =>
            envelopeError(9999, 'Some unknown business error'),
      );

      await expectLater(
        client.productInquiryByQrCode('01...'),
        throwsA(
          isA<KnddbApiException>()
              .having((error) => error.resultCode, 'resultCode', 9999)
              .having(
                  (error) => error.isBusinessError, 'isBusinessError', isTrue)
              .having((error) => error.resultDescription, 'resultDescription',
                  'Some unknown business error'),
        ),
      );

      client.close();
    });

    test('код 2 на /connect/token объясняет причину', () async {
      final (:client, :http) = createClient(
        responder: (request, index) => envelopeError(
          2,
          'Unexpected error(s) occurred: An error occurred while saving the entity changes',
        ),
      );

      await expectLater(
        client.signIn('user', 'pwd'),
        throwsA(
          isA<KnddbAuthenticationException>()
              .having((error) => error.resultCode, 'resultCode', 2)
              .having((error) => error.statusCode, 'statusCode', 200)
              .having((error) => error.message, 'message',
                  contains('Внутренняя ошибка сервера'))
              .having(
                  (error) => error.message, 'message', contains('user-agent')),
        ),
      );

      expect(client.isAuthenticated, isFalse);
      client.close();
    });

    test('toString содержит код результата и описание', () async {
      final (:client, :http) = createClient(
        responder: (request, index) =>
            envelopeError(6022, 'Product with QRCode X not found'),
      );

      try {
        await client.productInquiryByQrCode('X');
        fail('ожидалось исключение');
      } on KnddbApiException catch (error) {
        expect(error.toString(), contains('resultCode: 6022'));
        expect(error.toString(), contains('не найдена'));
      }

      client.close();
    });

    test('расшифровка кодов покрывает ключевые случаи', () {
      expect(KnddbResultCodes.describe(0), 'Операция выполнена успешно.');
      expect(KnddbResultCodes.describe(6022), contains('не найдена'));
      expect(
          KnddbResultCodes.describe(6023), contains('не подходит для продажи'));
      expect(KnddbResultCodes.describe(2), contains('Внутренняя ошибка'));
      expect(
          KnddbResultCodes.describe(6003), contains('повторяющиеся QR-коды'));
      expect(KnddbResultCodes.describe(6045), contains('деактивировать'));
      expect(KnddbResultCodes.describe(6100), contains('Рецепт не найден'));
      expect(KnddbResultCodes.describe(null), isNull);
      expect(KnddbResultCodes.describe(123456), isNull);
    });
  });

  group('Область доступа (scope)', () {
    test('запрос токена не содержит offline_access', () async {
      // Регрессия, найденная на живом сервере: сервер регистрирует только
      // область "api". Область offline_access в OpenIddict разрешена лишь при
      // включённом потоке обновления токена, которого на сервере KNMDB нет,
      // поэтому запрос с ней отклоняется целиком и вход невозможен.
      final (:client, :http) = createClient(
        responder: (request, index) => tokenResponse(),
      );

      await client.signIn('user', 'pwd');

      final body = http.requests.single.body!;
      final form = Uri.splitQueryString(body);

      expect(form['scope'], 'api');
      expect(body, isNot(contains('offline_access')));

      client.close();
    });

    test('область доступа настраивается', () async {
      final stub = StubHttpClient((request, index) => tokenResponse());
      final client = KnddbApiClient(
        options: KnddbClientOptions(scope: 'api custom'),
        httpClient: stub,
      );

      await client.signIn('user', 'pwd');

      expect(Uri.splitQueryString(stub.requests.single.body!)['scope'],
          'api custom');

      client.close();
    });

    test('пустая область доступа не передаётся', () async {
      final stub = StubHttpClient((request, index) => tokenResponse());
      final client = KnddbApiClient(
        options: KnddbClientOptions(scope: ''),
        httpClient: stub,
      );

      await client.signIn('user', 'pwd');

      expect(
        Uri.splitQueryString(stub.requests.single.body!).containsKey('scope'),
        isFalse,
      );

      client.close();
    });
  });

  group('Заголовок User-Agent', () {
    test('отправляется всегда, даже если приложение его не задало', () async {
      // Регрессия: без user-agent сервер KNMDB не может сохранить запись
      // о входе и отвечает кодом результата 2.
      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse();
          }
          return envelope('{ "numberOfStakeholders": 0 }');
        },
      );

      await client.signIn('user', 'pwd');
      await client.getAllStakeholders();

      for (final request in http.requests) {
        expect(request.userAgent, isNotNull,
            reason: 'нет user-agent в ${request.path}');
        expect(request.userAgent, contains('Knddb.TrackAndTrace.SDK'));
      }

      // Первый запрос — токен: с него начинается работа.
      expect(http.requests.first.path, '/connect/token');
      expect(
          http.requests.first.userAgent, contains('Knddb.TrackAndTrace.SDK'));

      client.close();
    });

    test('приложение может задать свой user-agent', () async {
      final stub = StubHttpClient((request, index) {
        if (request.url.path == '/connect/token') {
          return tokenResponse();
        }
        return envelope('{ "numberOfStakeholders": 0 }');
      });

      final client = KnddbApiClient(
        options: KnddbClientOptions(userAgent: 'MyPharmacyApp/2.1'),
        httpClient: stub,
      );

      await client.signIn('user', 'pwd');
      await client.getAllStakeholders();

      for (final request in stub.requests) {
        expect(request.userAgent, 'MyPharmacyApp/2.1');
      }

      client.close();
    });
  });

  group('Хранилище сессии', () {
    test('сохраняет и восстанавливает вход', () async {
      final storage = InMemorySessionStorage();

      final (:client, :http) = createClient(
        responder: (request, index) {
          if (request.url.path == '/connect/token') {
            return tokenResponse(
                accessToken: 'token-saved', refreshToken: 'refresh-saved');
          }
          return envelope('{ "numberOfStakeholders": 0 }');
        },
      );

      final clientWithStorage = KnddbApiClient(
        options: KnddbClientOptions(),
        httpClient: http,
        storage: storage,
      );

      await clientWithStorage.signIn('user', 'pwd', sessionKey: 'user:42');
      expect(storage.saved, isNotNull);

      final restoredClient = KnddbApiClient(
        options: KnddbClientOptions(),
        httpClient: http,
        storage: storage,
      );

      expect(await restoredClient.restoreSession(), isTrue);
      expect(restoredClient.currentSessionKey, 'user:42');
      expect(restoredClient.currentSession!.accessToken, 'token-saved');

      // Пароль на диск не сохраняется.
      expect(storage.rawJson.containsKey('credentials'), isFalse);
      expect(storage.rawJson.containsKey('password'), isFalse);

      client.close();
      clientWithStorage.close();
      restoredClient.close();
    });

    test('восстановление без сохранённых данных возвращает false', () async {
      final storage = InMemorySessionStorage();
      final (:client, :http) =
          createClient(responder: (request, index) => jsonResponse({}));

      final clientWithStorage = KnddbApiClient(
        options: KnddbClientOptions(),
        httpClient: http,
        storage: storage,
      );

      expect(await clientWithStorage.restoreSession(), isFalse);

      client.close();
      clientWithStorage.close();
    });
  });
}

/// Хранилище сессии в памяти — для тестов.
class InMemorySessionStorage implements KnddbSessionStorage {
  /// Сохранённая сессия.
  KnddbSession? saved;

  /// Последний сохранённый JSON.
  Map<String, dynamic> rawJson = <String, dynamic>{};

  @override
  Future<void> save(KnddbSession session) async {
    saved = session;
    rawJson = jsonDecode(jsonEncode(session.toJson())) as Map<String, dynamic>;
  }

  @override
  Future<KnddbSession?> load() async {
    final session = saved;
    if (session == null) {
      return null;
    }

    return KnddbSession.fromJson(rawJson);
  }

  @override
  Future<void> clear() async {
    saved = null;
    rawJson = <String, dynamic>{};
  }
}
