/// Описание проблемы в формате RFC 7807 (Problem Details for HTTP APIs).
///
/// KNMDB API возвращает объект этой формы для ошибок уровня ASP.NET Core:
/// `400 Bad Request`, `404 Not Found`, `500 Internal Server Error` и т. п.
///
/// Нестандартные поля сервера попадают в [extensions].
class ApiProblemDetails {
  /// Создаёт описание проблемы.
  const ApiProblemDetails({
    this.type,
    this.title,
    this.status,
    this.detail,
    this.instance,
    this.extensions,
  });

  /// Разбирает описание проблемы из JSON.
  ///
  /// Незнакомые свойства верхнего уровня складываются в [extensions].
  factory ApiProblemDetails.fromJson(Map<String, dynamic> json) {
    const known = {'type', 'title', 'status', 'detail', 'instance'};

    final extensions = <String, dynamic>{};
    for (final entry in json.entries) {
      if (!known.contains(entry.key)) {
        extensions[entry.key] = entry.value;
      }
    }

    return ApiProblemDetails(
      type: json['type'] as String?,
      title: json['title'] as String?,
      status: (json['status'] as num?)?.toInt(),
      detail: json['detail'] as String?,
      instance: json['instance'] as String?,
      extensions: extensions.isEmpty ? null : extensions,
    );
  }

  /// URI-идентификатор типа проблемы.
  final String? type;

  /// Краткое человекочитаемое название проблемы (например, `Not Found`).
  final String? title;

  /// HTTP-код ответа, продублированный в теле.
  final int? status;

  /// Подробное описание конкретной ошибки.
  final String? detail;

  /// URI конкретного экземпляра проблемы.
  final String? instance;

  /// Дополнительные (нестандартные) поля, добавленные сервером.
  ///
  /// Например, `errors` со списком ошибок валидации по полям.
  final Map<String, dynamic>? extensions;

  /// Наиболее информативное текстовое описание проблемы из доступных полей.
  String? get message => detail ?? title;

  @override
  String toString() {
    final statusText = status?.toString() ?? '?';
    final text = message;
    return text == null || text.isEmpty
        ? 'HTTP $statusText'
        : 'HTTP $statusText: $text';
  }
}
