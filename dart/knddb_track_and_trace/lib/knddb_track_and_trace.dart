/// Dart/Flutter SDK для Track and Trace API системы KNMDB / KNDDB
/// (Кыргызская национальная база лекарственных средств).
///
/// Один пакет закрывает все задачи интеграции: аутентификация по OAuth 2.0,
/// автопродление токена, смена пользователя «на ходу», типизированные модели
/// и все методы API.
///
/// ## Быстрый старт
///
/// ```dart
/// import 'package:knddb_track_and_trace/knddb_track_and_trace.dart';
///
/// final client = KnddbApiClient(
///   options: KnddbClientOptions(environment: KnddbEnvironment.test),
/// );
///
/// await client.signIn('ваш_логин', 'ваш_пароль');
///
/// final stakeholders = await client.getAllStakeholders();
/// for (final organization in stakeholders.stakeholders ?? []) {
///   print('${organization.code}: ${organization.name} (${organization.type.label})');
/// }
///
/// client.close();
/// ```
///
/// ## Контуры
///
/// | Контур | Базовый адрес |
/// |---|---|
/// | [KnddbEnvironment.test] | `https://testndbapi.med.kg/` (по умолчанию) |
/// | [KnddbEnvironment.production] | `https://ndbapi.med.kg/` |
///
/// Начинайте с тестового контура: на боевом все операции реальны и необратимы.
///
/// ## Права доступа
///
/// Роли API настраиваются администратором департамента лекарственных средств
/// через административную панель KNMDB: интеграции достаточно логина и пароля.
/// Если метод вернул `403`, обратитесь к администратору департамента.
///
/// ## Ошибки бизнес-логики
///
/// Сервер возвращает ошибки бизнес-логики в конверте
/// `{ resultCode, resultMessage, actionResult }` **с HTTP-статусом 200**.
///
/// ```dart
/// try {
///   await client.salesDeclaration(request);
/// } on KnddbApiException catch (error) {
///   if (error.isBusinessError) {
///     // resultCode 6023 — упаковка уже продана
///     print('${error.resultCode}: ${error.resultDescription}');
///   }
/// }
/// ```
///
/// SDK разбирает конверт автоматически и выбрасывает [KnddbApiException]
/// при любом ненулевом `resultCode`. Готовые проверки:
/// [KnddbApiException.isBusinessError],
/// [KnddbApiException.isProductNotFound],
/// [KnddbApiException.isProductNotSuitableForSale];
/// расшифровка кодов — [KnddbResultCodes.describe].
///
/// ## Документация
///
/// - README пакета — установка, быстрый старт, смена пользователя;
/// - `docs/SCENARIOS.md` — пошаговые сценарии «от импортёра до аптеки»;
/// - `docs/API-REFERENCE.md` — все методы и поля;
/// - `docs/ENUMS.md` — значения всех перечислений.
library;

export 'src/environment.dart' show KnddbEnvironment;
export 'src/exceptions.dart'
    show
        KnddbApiException,
        KnddbAuthenticationException,
        KnddbConfigurationException;
export 'src/knddb_api_client.dart' show KnddbApiClient, KnddbJsonDecoder;
export 'src/models/api/api_problem_details.dart' show ApiProblemDetails;
export 'src/models/auth/knddb_credentials.dart' show KnddbCredentials;
export 'src/models/enums/consumption_type.dart'
    show ConsumptionType, TrackAndTraceStatus;
export 'src/models/enums/product_state.dart' show ProductState, ProductStatus;
export 'src/models/enums/stakeholder_type.dart' show StakeholderType;
export 'src/models/enums/transfer.dart' show TransferState, TransferType;
export 'src/models/requests/declaration_requests.dart'
    show
        ImportDeclarationRequest,
        ProductionDeclarationRequest,
        TransferAcceptRequest,
        TransferCancelRequest,
        TransferDeclarationDetail,
        TransferDeclarationRequest,
        TransferReturnRequest,
        TransferReturnRequestDetail;
export 'src/models/requests/inquiry_and_filter_requests.dart'
    show
        GetMedicineListRequest,
        GetPartialSaleInfoRequest,
        GetStockInheldListByGtinRequest,
        GetTransferListByFilterRequest,
        ProductInquiryWithGtinSnRequest,
        ProductInquiryWithQrCodeRequest;
export 'src/models/requests/sales_and_stock_requests.dart'
    show
        DeactivateDeclarationDetail,
        DeactivateDeclarationRequest,
        SalesDeclarationBoxCancelRequest,
        SalesDeclarationCancelRequest,
        SalesDeclarationDetail,
        SalesDeclarationRequest,
        StockDeclarationCancelRequest,
        StockDeclarationDetail,
        StockDeclarationRequest;
export 'src/models/responses/declaration_responses.dart'
    show
        DeactivateDeclarationResponse,
        ImportDeclarationResponse,
        ProductionDeclarationResponse,
        SalesDeclarationResponse,
        StockDeclarationCancelResponse,
        StockDeclarationResponse,
        TransferDeclarationCancelResponse,
        TransferDeclarationResponse;
export 'src/models/responses/dictionary_responses.dart'
    show
        GetAllStakeholdersResponse,
        GetStockInheldListByGtinResponse,
        GetStockInheldListResponse,
        GetSupportedQRTypesResponse,
        GetTransferDeclarationResponse,
        GetTransferListByFilterResponse,
        QRTypeInfo,
        StakeholderInfo,
        StockInheldInfo,
        StockInheldSummaryInfo,
        TransferDeclarationDetailItem,
        TransferDeclarationInfo;
export 'src/models/responses/product_inquiry_responses.dart'
    show
        GetMedicineListResponse,
        GetPartialSaleInfoResponse,
        MedicineInfo,
        ProductInquiryHistory,
        ProductInquiryResult,
        SuspendRecallInfo;
export 'src/options.dart' show KnddbClientOptions;
export 'src/result_codes.dart' show KnddbResultCodes;
export 'src/session.dart' show KnddbSession;
export 'src/session_storage.dart' show KnddbSessionStorage;
