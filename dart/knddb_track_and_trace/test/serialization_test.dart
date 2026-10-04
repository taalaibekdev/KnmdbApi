import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';
import 'package:test/test.dart';

void main() {
  group('Окружения', () {
    test('тестовый контур используется по умолчанию', () {
      final options = KnddbClientOptions();

      expect(options.environment, KnddbEnvironment.test);
      expect(options.effectiveBaseUrl, 'https://testndbapi.med.kg/');
      expect(options.resolvedEnvironment, KnddbEnvironment.test);
      expect(options.isProduction, isFalse);
    });

    test('боевой контур резолвится в свой адрес', () {
      final options =
          KnddbClientOptions(environment: KnddbEnvironment.production);

      expect(options.effectiveBaseUrl, 'https://ndbapi.med.kg/');
      expect(options.resolvedEnvironment, KnddbEnvironment.production);
      expect(options.isProduction, isTrue);
    });

    test('явный адрес имеет приоритет над контуром', () {
      final options = KnddbClientOptions(
        environment: KnddbEnvironment.production,
        baseUrl: 'http://localhost:5001',
      );

      expect(options.effectiveBaseUrl, 'http://localhost:5001/');
      expect(options.resolvedEnvironment, isNull);
      expect(options.isProduction, isFalse);
    });

    test('определяет контур по адресу', () {
      expect(
        KnddbEnvironment.fromBaseUrl('https://testndbapi.med.kg/'),
        KnddbEnvironment.test,
      );
      expect(
        KnddbEnvironment.fromBaseUrl('https://ndbapi.med.kg/'),
        KnddbEnvironment.production,
      );
      expect(KnddbEnvironment.fromBaseUrl('http://localhost:5001/'), isNull);
      expect(KnddbEnvironment.fromBaseUrl(null), isNull);
    });

    test('добавляет завершающий слэш', () {
      final options = KnddbClientOptions(baseUrl: 'https://testndbapi.med.kg');

      expect(options.effectiveBaseUrl, 'https://testndbapi.med.kg/');
    });

    test('проверяет корректность настроек', () {
      expect(
        () => KnddbClientOptions(baseUrl: 'not-a-uri').validate(),
        throwsA(isA<ArgumentError>()),
      );

      expect(
        () => KnddbClientOptions(baseUrl: 'ftp://example.com/').validate(),
        throwsA(isA<ArgumentError>()),
      );

      expect(
        () => KnddbClientOptions(timeout: Duration.zero).validate(),
        throwsA(isA<ArgumentError>()),
      );

      expect(KnddbClientOptions().validate(), 'https://testndbapi.med.kg/');
    });
  });

  group('Перечисления', () {
    test('значения совпадают с серверными', () {
      expect(StakeholderType.producer.value, 1);
      expect(StakeholderType.pharmacy.value, 5);
      expect(StakeholderType.warehouse.value, 7);

      expect(ProductState.production.value, 1);
      expect(ProductState.import.value, 2);
      expect(ProductState.sales.value, 3);
      expect(ProductState.stock.value, 13);
      expect(ProductState.deactivationCancelled.value, 18);

      expect(ProductStatus.inTransfer.value, 3);
      expect(TransferState.initiated.value, 1);
      expect(TransferType.accept.value, 2);

      expect(ConsumptionType.systemIn.value, 0);
      expect(ConsumptionType.systemOut.value, 10);
      expect(ConsumptionType.sample.value, 100);

      expect(TrackAndTraceStatus.notSpecified.value, 0);
      expect(TrackAndTraceStatus.labelAndTraceMandatory.value, 3);
    });

    test('разбираются из числа', () {
      expect(StakeholderType.fromWire(5), StakeholderType.pharmacy);
      expect(ProductState.fromWire(10), ProductState.transferAccepted);
      expect(ConsumptionType.fromWire(40), ConsumptionType.disposalForDeadline);
      expect(TrackAndTraceStatus.fromWire(3),
          TrackAndTraceStatus.labelAndTraceMandatory);
    });

    test('разбираются из имени члена', () {
      expect(StakeholderType.fromWire('Pharmacy'), StakeholderType.pharmacy);
      expect(StakeholderType.fromWire('medicalOrganization'),
          StakeholderType.medicalOrganization);
      expect(ProductState.fromWire('TransferInitiated'),
          ProductState.transferInitiated);
      expect(TransferState.fromWire('Accepted'), TransferState.accepted);
    });

    test('разбираются из человекочитаемого названия', () {
      expect(
        TrackAndTraceStatus.fromWire('Label And TraceMandatory'),
        TrackAndTraceStatus.labelAndTraceMandatory,
      );
      expect(TrackAndTraceStatus.fromWire('Not Required'),
          TrackAndTraceStatus.notTracked);
      expect(ProductState.fromWire('Return Transfer Cancelled'),
          ProductState.returnTransferCancelled);
      expect(ConsumptionType.fromWire('DISPOSAL FOR DEADLINE'),
          ConsumptionType.disposalForDeadline);
      expect(StakeholderType.fromWire('WAREHOUSE'), StakeholderType.warehouse);
    });

    test('неизвестное значение даёт null', () {
      expect(StakeholderType.fromWire(999), isNull);
      expect(StakeholderType.fromWire('Неизвестное'), isNull);
      expect(ProductState.fromWire(null), isNull);
      expect(ConsumptionType.fromWire(''), isNull);
    });

    test('причины списания имеют русские названия', () {
      expect(
          ConsumptionType.disposalForDeadline.reasonRu, 'Истёк срок годности');
      expect(ConsumptionType.revision.reasonRu, 'Инвентаризационная недостача');
      expect(ConsumptionType.sample.reasonRu, 'Образец');
    });

    test('статус маркировки определяет необходимость прослеживаемости', () {
      expect(TrackAndTraceStatus.labelAndTraceMandatory.requiresUnitTracking,
          isTrue);
      expect(TrackAndTraceStatus.labelMandatory.requiresUnitTracking, isFalse);
      expect(TrackAndTraceStatus.notTracked.requiresUnitTracking, isFalse);
    });
  });

  group('Форматирование дат', () {
    test('в запрос дата уходит без времени', () {
      final request = ImportDeclarationRequest(
        gtin: '04600000000017',
        batchNo: 'B-2025-01',
        expirationDate: DateTime.utc(2027, 1, 1),
        price: 120.5,
        productionDate: DateTime.utc(2025, 1, 1),
        documentDate: DateTime.utc(2025, 2, 1),
        documentNo: 'ГТД-12345',
        qrTypeId: 1,
        qrCodes: const ['010460000000001721SN0000000000000001'],
      );

      final json = request.toJson();

      expect(json['expirationDate'], '2027-01-01');
      expect(json['productionDate'], '2025-01-01');
      expect(json['documentDate'], '2025-02-01');
      expect(json['gtin'], '04600000000017');
      expect(json['price'], 120.5);
      expect(json['qrTypeId'], 1);
      expect(json['qrCodes'], hasLength(1));
    });

    test('время отбрасывается, даже если задано явно', () {
      // Регрессия: сервер KNMDB не принимает дату вида «2026-03-15T12:00:00Z»
      // и отвечает кодом результата 2.
      final request = StockDeclarationRequest(
        declarationDate: DateTime.utc(2026, 3, 15, 10, 30, 0),
        stakeholderCode: 100,
      );

      expect(request.toJson()['declarationDate'], '2026-03-15');
    });

    test('принимается дата без времени', () {
      final request = StockDeclarationRequest(
        declarationDate: DateTime.parse('2026-03-15'),
        stakeholderCode: 100,
      );

      expect(request.toJson()['declarationDate'], '2026-03-15');
    });

    test('локальное время сохраняет свой календарный день', () {
      // В Dart нет аналога DateTimeOffset: DateTime хранит либо локальное
      // время, либо UTC. Для локальной даты берётся локальный календарный день —
      // это то, что имел в виду оператор.
      final request = StockDeclarationRequest(
        declarationDate: DateTime(2026, 3, 15, 23, 30),
        stakeholderCode: 100,
      );

      expect(request.toJson()['declarationDate'], '2026-03-15');
    });

    test('отметка времени lastUpdate остаётся полной', () {
      // Для синхронизации справочника нужна точная отметка времени,
      // поэтому поле lastUpdate формат даты не урезает.
      final request = GetMedicineListRequest(
        lastUpdate: DateTime.utc(2026, 2, 21, 10, 30, 0),
      );

      expect(request.toJson()['lastUpdate'], '2026-02-21T10:30:00.000Z');
    });

    test('необязательные поля со значением null не попадают в JSON', () {
      final request = GetTransferListByFilterRequest(declarationId: 42);
      final json = request.toJson();

      expect(json['declarationId'], 42);
      expect(json.containsKey('documentNo'), isFalse);
      expect(json.containsKey('currentState'), isFalse);
      expect(json.containsKey('declarationDateFrom'), isFalse);
    });

    test('перечисления в запросе уходят числами', () {
      final request = GetTransferListByFilterRequest(
        currentState: TransferState.accepted,
        transferType: TransferType.accept,
        isReturn: true,
      );

      final json = request.toJson();

      expect(json['currentState'], 2);
      expect(json['transferType'], 2);
      expect(json['isReturn'], isTrue);
    });

    test('причина списания в запросе деактивации уходит числом', () {
      final request = DeactivateDeclarationRequest(
        consumptionType: ConsumptionType.disposalForDeadline,
        description: 'Акт №18',
        details: const [DeactivateDeclarationDetail(qrCode: '01...')],
      );

      final json = request.toJson();

      expect(json['consumptionType'], 40);
      expect(json['description'], 'Акт №18');
      expect(json['details'], hasLength(1));
    });

    test('флаг аптечной продажи уходит с серверным именем поля', () {
      final request = SalesDeclarationRequest(
        isPharmacyConsumption: true,
        details: const [SalesDeclarationDetail(qrCode: '01...', price: 165)],
      );

      final json = request.toJson();

      // Историческое написание на сервере — «Consuption».
      expect(json['isPharmacyConsuption'], isTrue);
      expect(json.containsKey('isPharmacyConsumption'), isFalse);
    });

    test('частичная продажа передаёт количество', () {
      final request = SalesDeclarationRequest(
        isPharmacyConsumption: true,
        details: const [
          SalesDeclarationDetail(
            qrCode: '01...',
            price: 60,
            isPartialSale: true,
            partialSaleAmount: 10,
          ),
        ],
      );

      final detail = (request.toJson()['details']! as List<Object?>).first
          as Map<String, dynamic>;

      expect(detail['isPartialSale'], isTrue);
      expect(detail['partialSaleAmount'], 10);
    });
  });

  group('Разбор ответов', () {
    test('список организаций', () {
      final response = GetAllStakeholdersResponse.fromJson(<String, dynamic>{
        'numberOfStakeholders': 2,
        'stakeholders': [
          {
            'code': 100,
            'name': 'ОсОО «Фарма»',
            'type': 5,
            'taxNumber': '0123456789',
            'city': 'Бишкек',
            'parentCode': 5,
            'parentName': 'Головная организация',
          },
          {
            'code': 200,
            'name': 'Склад №1',
            'type': 'Warehouse',
            'parentCode': null,
          },
        ],
      });

      expect(response.numberOfStakeholders, 2);
      expect(response.stakeholders, hasLength(2));
      expect(response.stakeholders![0].code, 100);
      expect(response.stakeholders![0].type, StakeholderType.pharmacy);
      expect(response.stakeholders![0].parentCode, 5);
      expect(response.stakeholders![1].type, StakeholderType.warehouse);
      expect(response.stakeholders![1].parentCode, isNull);
    });

    test('проверка лекарства, включая поля вне OpenAPI-схемы', () {
      final result = ProductInquiryResult.fromJson(<String, dynamic>{
        'productBoxId': 777,
        'productPackageItemId': '0f8fad5b-d9cb-469f-a165-70867728950e',
        'productName': 'Парацетамол',
        'gtin': '04600000000017',
        'serialNumber': 'SN0000000000000001',
        'batchNumber': 'B-2025-01',
        'productionDate': '2025-01-01T00:00:00+06:00',
        'expirationDate': '2027-01-01T00:00:00+06:00',
        'qrCode': '0104600000000017...',
        'stakeHolderName': 'Аптека №5',
        'stakeholderTaxNumber': '0123456789',
        'isExpired': false,
        'isAvailableForSale': true,
        'isSuspendedOrRecalled': false,
        'productStatus': 1,
        'productState': 10,
        'suspendRecallInfo': null,
        'productInquiryHistory': [
          {
            'declarationNumber': 9001,
            'stakeHolder': 'Импортёр ОсОО',
            'state': 2,
            'stateDate': '2025-02-01T09:15:00+06:00',
            'price': 100.5,
            'partialSaleAmount': null,
          },
        ],
        'overallRetailPrice': '150.00',
        'certificateNumber': 'KG-12345',
        'instructionForUseDocId': '3fa85f64-5717-4562-b3fc-2c963f66afa6',
        'isFomsDrug': true,
        'compensation': '50%',
        'consumptionType': null,
        'manufacturerName': 'Завод «Фарм»',
        'partialSaleRemainingAmount': null,
      });

      expect(result.productBoxId, 777);
      expect(result.stakeHolderName, 'Аптека №5');
      expect(result.stakeholderTaxNumber, '0123456789');
      expect(result.isAvailableForSale, isTrue);
      expect(result.isExpired, isFalse);
      expect(result.productStatus, ProductStatus.inStock);
      expect(result.productState, ProductState.transferAccepted);
      expect(result.consumptionType, isNull);
      expect(result.productInquiryHistory, hasLength(1));
      expect(result.productInquiryHistory![0].state, ProductState.import);
      expect(result.productInquiryHistory![0].price, 100.5);
      expect(result.partialSaleRemainingAmount, isNull);
      expect(result.verificationVerdict, 'ok');
    });

    test('вердикт проверки для отозванной упаковки', () {
      final result = ProductInquiryResult.fromJson(<String, dynamic>{
        'productBoxId': 1,
        'productPackageItemId': 'x',
        'productionDate': '2025-01-01T00:00:00Z',
        'expirationDate': '2027-01-01T00:00:00Z',
        'isExpired': false,
        'isAvailableForSale': false,
        'isSuspendedOrRecalled': true,
        'productStatus': 2,
        'productState': 4,
        'suspendRecallInfo': {
          'startDate': '2025-03-01T00:00:00Z',
          'reason': 'Брак серии'
        },
      });

      expect(result.verificationVerdict, 'recalled');
      expect(result.verificationMessage, 'Упаковка отозвана: Брак серии');
      expect(result.suspendRecallInfo!.reason, 'Брак серии');
    });

    test('вердикт проверки для просроченной упаковки', () {
      final result = ProductInquiryResult.fromJson(<String, dynamic>{
        'productBoxId': 1,
        'productPackageItemId': 'x',
        'productionDate': '2020-01-01T00:00:00Z',
        'expirationDate': '2022-01-01T00:00:00Z',
        'isExpired': true,
        'isAvailableForSale': false,
        'isSuspendedOrRecalled': false,
        'productStatus': 1,
        'productState': 13,
      });

      expect(result.verificationVerdict, 'expired');
      expect(result.verificationMessage, 'Срок годности истёк');
    });

    test('справочник препаратов со статусом маркировки', () {
      final response = GetMedicineListResponse.fromJson(<String, dynamic>{
        'numberOfMedicines': 1,
        'medicineList': [
          {
            'gtin': '04600000000017',
            'brandName': 'Парацетамол',
            'fullBrandName': 'Парацетамол, таблетки 500 мг №20',
            'manufacturerCompany': 'Завод «Фарм»',
            'country': 'Кыргызстан',
            'atcCode': 'N02BE01',
            'trackAndTraceStatus': 'Label And TraceMandatory',
            'lastUpdate': '2026-02-21T10:30:00+06:00',
          },
        ],
      });

      expect(response.numberOfMedicines, 1);
      expect(response.medicineList![0].trackAndTraceStatus,
          TrackAndTraceStatus.labelAndTraceMandatory);
      expect(response.medicineList![0].atcCode, 'N02BE01');
    });

    test('остатки на складе', () {
      final response =
          GetStockInheldListByGtinResponse.fromJson(<String, dynamic>{
        'stockInheldList': [
          {
            'id': 15,
            'currentStakeholderId': '0f8fad5b-d9cb-469f-a165-70867728950e',
            'drugPackageItemId': '1f8fad5b-d9cb-469f-a165-70867728950e',
            'gtin': '04600000000017',
            'fullBrandName': 'Парацетамол',
            'batchNumber': 'B-2025-01',
            'expirationDate': '2027-01-01T00:00:00Z',
            'serialNumber': 'SN1',
            'declarationDate': '2025-02-01T00:00:00Z',
            'price': 120.5,
            'partialSaleAmount': null,
            'currentState': 13,
            'qrCode': '01...',
          },
        ],
      });

      final box = response.stockInheldList![0];

      expect(box.id, 15);
      expect(box.currentState, ProductState.stock);
      expect(box.price, 120.5);
      expect(box.partialSaleAmount, isNull);
    });

    test('отмена складской декларации с неуспешным результатом', () {
      final response =
          StockDeclarationCancelResponse.fromJson(<String, dynamic>{
        'declarationId': 15,
        'isSuccess': false,
        'message': 'Декларация уже отменена',
      });

      expect(response.declarationId, 15);
      expect(response.isSuccess, isFalse);
      expect(response.message, 'Декларация уже отменена');
    });

    test('числа, пришедшие строками, читаются корректно', () {
      final response = StockDeclarationResponse.fromJson(<String, dynamic>{
        'declarationId': '12345',
        'declarationDate': '2026-01-15T10:00:00+06:00',
      });

      expect(response.declarationId, 12345);
    });

    test('дата в формате yymmdd распознаётся', () {
      final result = ProductInquiryResult.fromJson(<String, dynamic>{
        'productBoxId': 1,
        'productPackageItemId': 'x',
        'productionDate': '250101',
        'expirationDate': '270101',
        'isExpired': false,
        'isAvailableForSale': true,
        'isSuspendedOrRecalled': false,
        'productStatus': 1,
        'productState': 2,
      });

      expect(result.productionDate, DateTime.utc(2025, 1, 1));
      expect(result.expirationDate, DateTime.utc(2027, 1, 1));
    });
  });

  group('ProblemDetails', () {
    test('разбирается вместе с нестандартными полями', () {
      final problem = ApiProblemDetails.fromJson(<String, dynamic>{
        'type': 'https://tools.ietf.org/html/rfc7231#section-6.5.1',
        'title': 'One or more validation errors occurred.',
        'status': 400,
        'detail': 'Поле qrCodes не может быть пустым.',
        'instance': '/api/TrackAndTrace/ImportDeclaration',
        'traceId': '00-8f1a-01',
        'errors': {
          'qrCodes': ['Поле обязательно'],
        },
      });

      expect(problem.status, 400);
      expect(problem.message, 'Поле qrCodes не может быть пустым.');
      expect(problem.extensions, isNotNull);
      expect(problem.extensions!['errors'], isNotNull);
      expect(problem.extensions!['traceId'], '00-8f1a-01');
    });
  });
}
