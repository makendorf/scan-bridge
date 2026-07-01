/* ── ScanBridge Action Type Definitions ── */

const ACTION_TYPES = {
    Log: {
        name: 'Логирование',
        description: 'Записывает данные сканирования в системный лог приложения.',
        settings: []
    },
    Replacement: {
        name: 'Замена символов',
        description: 'Заменяет или удаляет подстроки в данных сканирования по заданным правилам. Поддерживает замену всех вхождений, только начала или конца строки.',
        settings: [{ key: 'Replacements', label: 'Правила замены', type: 'replacements' }]
    },
    ClipboardPaste: {
        name: 'Вставка в активное окно',
        description: 'Вставляет данные сканирования в текущее активное окно. Работает только на Windows.',
        settings: [
            { key: 'Mode', label: 'Режим вставки', type: 'select',
              options: ['clipboard', 'keyboard'], default: 'clipboard',
              optionLabels: { clipboard: 'Ctrl+V (буфер обмена)', keyboard: 'Клавиатура (печать)' },
              hint: 'Ctrl+V — быстрый способ через буфер обмена.\nКлавиатура — эмулирует посимвольный ввод, как будто текст печатают с клавиатуры.' },
            { key: 'AppendNewline', label: 'Добавить перенос строки', type: 'select', options: ['false', 'true'], default: 'false' }
        ]
    },
    WindowPaste: {
        name: 'Вставка в выбранное окно',
        description: 'Находит окно по заголовку, активирует его и вставляет данные. Работает только на Windows.',
        settings: [
            { key: 'WindowTitle', label: 'Заголовок окна', type: 'text', default: '',
              hint: 'Часть заголовка окна (регистр не важен). Например: «Notepad», «Excel», «1С»' },
            { key: 'Mode', label: 'Режим вставки', type: 'select',
              options: ['clipboard', 'keyboard'], default: 'clipboard',
              optionLabels: { clipboard: 'Ctrl+V (буфер обмена)', keyboard: 'Клавиатура (печать)' },
              hint: 'Ctrl+V — быстрый способ через буфер обмена.\nКлавиатура — эмулирует посимвольный ввод.' },
            { key: 'ActivationDelay', label: 'Задержка после активации (мс)', type: 'number', default: '200',
              hint: 'Время ожидания после активации окна перед вставкой. Увеличьте, если окно не успевает открыться.' },
            { key: 'AppendNewline', label: 'Добавить перенос строки', type: 'select', options: ['false', 'true'], default: 'false' }
        ]
    },
    Export: {
        name: 'Экспорт',
        description: 'Сохраняет результат сканирования в файл (локально, FTP, SFTP) или отправляет на HTTP-эндпоинт. Поддерживает форматы JSON и XML.',
        settings: [
            { key: 'Destination', label: 'Назначение', type: 'select', options: ['folder', 'ftp', 'sftp', 'http'], default: 'folder',
              optionLabels: { folder: 'Локальная папка', ftp: 'FTP-сервер', sftp: 'SFTP-сервер', http: 'HTTP POST' } },
            { key: 'FolderPath', label: 'Папка для файлов', type: 'text', default: 'C:\\Output', showWhen: 'Destination=folder' },
            { key: 'FtpHost', label: 'Хост', type: 'text', default: '', showWhen: 'Destination=ftp|sftp' },
            { key: 'FtpPort', label: 'Порт', type: 'number', default: '21', showWhen: 'Destination=ftp|sftp' },
            { key: 'FtpUser', label: 'Логин', type: 'text', default: '', showWhen: 'Destination=ftp|sftp' },
            { key: 'FtpPass', label: 'Пароль', type: 'text', default: '', showWhen: 'Destination=ftp|sftp' },
            { key: 'FtpRemotePath', label: 'Удалённая папка', type: 'text', default: '/', showWhen: 'Destination=ftp|sftp' },
            { key: 'FtpPassive', label: 'Пассивный режим (FTP)', type: 'select', options: ['true', 'false'], default: 'true', showWhen: 'Destination=ftp' },
            { key: 'HttpUrl', label: 'URL API', type: 'text', default: 'http://localhost/api/scan', showWhen: 'Destination=http',
              hint: 'Полный URL эндпоинта для POST-запроса' },
            { key: 'HttpContentType', label: 'Content-Type', type: 'text', default: '', showWhen: 'Destination=http',
              hint: 'Оставьте пустым для автопределения (application/json или application/xml)' },
            { key: 'HttpHeaders', label: 'Заголовки (JSON)', type: 'text', default: '{}', showWhen: 'Destination=http',
              hint: 'Дополнительные заголовки в формате JSON:\n{"Authorization": "Bearer token", "X-Custom": "value"}' },
            { key: 'Format', label: 'Формат', type: 'select', options: ['json', 'xml'], default: 'json' },
            { key: 'FilenameTemplate', label: 'Шаблон имени файла', type: 'text', default: '{timestamp}_{scanner}_{data}',
              hint: '{timestamp} — дата/время (yyyyMMdd_HHmmss_fff)\n{scanner} — имя сканера\n{data} — распознанные данные\n{format} — формат штрихкода' },
            { key: 'Tags', label: 'Теги в файле', type: 'tags' }
        ]
    },
    Telegram: {
        name: 'Уведомление Telegram',
        description: 'Отправляет сообщение о сканировании в Telegram-чат через бота. Создайте бота через @BotFather и укажите его токен и ID чата.',
        settings: [
            { key: 'BotToken', label: 'Токен бота', type: 'text', default: '',
              hint: 'Токен от @BotFather' },
            { key: 'ChatIds', label: 'ID чатов (через запятую)', type: 'text', default: '',
              hint: 'Перешлите сообщение боту @userinfobot чтобы узнать ваш ID' },
            { key: 'MessageTemplate', label: 'Шаблон сообщения', type: 'text',
              default: 'Scan: {data}',
              hint: '{data} — данные\n{format} — формат\n{scanner} — сканер\n{timestamp} — время\n{raw} — исходные' }
        ]
    },
    Email: {
        name: 'Уведомление Email',
        description: 'Отправляет письмо с данными сканирования через SMTP. Поддерживает шифрование SSL/TLS и плейсхолдеры в теме и теле письма.',
        settings: [
            { key: 'SmtpHost', label: 'SMTP хост', type: 'text', default: 'smtp.gmail.com' },
            { key: 'SmtpPort', label: 'Порт', type: 'number', default: '587' },
            { key: 'SmtpSecurity', label: 'Шифрование', type: 'select',
              options: ['None', 'SslOnConnect', 'StartTls'], default: 'StartTls' },
            { key: 'SmtpUser', label: 'Логин SMTP', type: 'text', default: '' },
            { key: 'SmtpPass', label: 'Пароль SMTP', type: 'text', default: '',
              hint: 'Для Gmail — пароль приложений' },
            { key: 'From', label: 'Отправитель', type: 'text', default: '' },
            { key: 'To', label: 'Получатели (через запятую)', type: 'text', default: '' },
            { key: 'Subject', label: 'Тема письма', type: 'text', default: 'Scan: {data}',
              hint: '{data} — данные\n{format} — формат\n{scanner} — сканер\n{timestamp} — время' },
            { key: 'Body', label: 'Тело письма', type: 'text', default: '{data} ({format})',
              hint: 'Те же плейсхолдеры' }
        ]
    },
    DataEnrichment: {
        name: 'Обогащение данных',
        description: 'Отправляет данные сканирования на внешний API и сохраняет полученный ответ в метаданные скана. Поддерживает GET и POST запросы.',
        settings: [
            { key: 'Url', label: 'URL', type: 'text', default: '' },
            { key: 'Method', label: 'Метод', type: 'select', options: ['GET', 'POST'], default: 'GET' },
            { key: 'Headers', label: 'Заголовки (JSON)', type: 'text', default: '{}' },
            { key: 'ResponseField', label: 'Поле ответа', type: 'text', default: '',
              hint: 'Извлечь конкретное поле из JSON-ответа. Пусто = весь ответ' },
            { key: 'QueryParam', label: 'Имя параметра', type: 'text', default: 'data',
              hint: 'Имя параметра запроса (GET) или ключ в теле (POST)' },
            { key: 'TimeoutSeconds', label: 'Таймаут (сек)', type: 'number', default: '10' }
        ]
    },
    Validation: {
        name: 'Валидация',
        description: 'Проверяет данные сканирования по заданным правилам. Режим «Формат» заменяет встроенную валидацию парсера — можно задать допустимые форматы, типы контента и требования к содержимому.',
        settings: [
            { key: 'ValidationType', label: 'Тип', type: 'select',
              options: ['format', 'regex', 'dictionary', 'range'], default: 'format',
              optionLabels: { format: 'Формат скана', regex: 'Регулярное выражение', dictionary: 'Словарь значений', range: 'Числовой диапазон' } },
            { key: 'RequireNonEmpty', label: 'Отклонять пустые', type: 'select', options: ['false', 'true'], default: 'true',
              showWhen: 'ValidationType=format',
              hint: 'Отклонять сканы с пустыми данными' },
            { key: 'RequireBarcode', label: 'Только штрихкоды', type: 'select', options: ['false', 'true'], default: 'false',
              showWhen: 'ValidationType=format',
              hint: 'Пропускать только распознанные форматы штрихкодов (EAN, UPC, Code128, UUID)' },
            { key: 'AllowedFormats', label: 'Допустимые форматы', type: 'text', default: '',
              showWhen: 'ValidationType=format',
              hint: 'Через запятую: EAN-8, EAN-13, UPC-A, GTIN-14, Code128, GS1-128, UUID, Numeric, QR, Unknown.\nПусто = все форматы' },
            { key: 'AllowedContentTypes', label: 'Допустимые типы QR', type: 'text', default: '',
              showWhen: 'ValidationType=format',
              hint: 'Через запятую: Url, Json, VCard, Wifi, Text.\nПусто = все типы' },
            { key: 'Pattern', label: 'Паттерн regex', type: 'text', default: '^[A-Z0-9]+$',
              showWhen: 'ValidationType=regex' },
            { key: 'MinLength', label: 'Мин. длина', type: 'number', default: '0',
              showWhen: 'ValidationType=regex' },
            { key: 'MaxLength', label: 'Макс. длина', type: 'number', default: '9999',
              showWhen: 'ValidationType=regex' },
            { key: 'DictionaryPath', label: 'Путь к файлу', type: 'text', default: '',
              showWhen: 'ValidationType=dictionary',
              hint: 'Текстовый файл, одно значение на строку' },
            { key: 'DictionaryUrl', label: 'URL словаря', type: 'text', default: '',
              showWhen: 'ValidationType=dictionary',
              hint: 'URL со списком значений (по одному на строку)' },
            { key: 'MinValue', label: 'Мин. значение', type: 'text', default: '',
              showWhen: 'ValidationType=range' },
            { key: 'MaxValue', label: 'Макс. значение', type: 'text', default: '',
              showWhen: 'ValidationType=range' },
            { key: 'OnFailure', label: 'При ошибке', type: 'select',
              options: ['skip', 'warn'], default: 'skip',
              optionLabels: { skip: 'Прервать цепочку', warn: 'Только предупредить' } }
        ]
    },
    Aggregation: {
        name: 'Агрегация',
        description: 'Накапливает результаты сканирования и отправляет пакетами: по количеству (после N сканов) или по времени (каждые N секунд). Пакет передаётся следующему действию в цепочке.',
        settings: [
            { key: 'Mode', label: 'Режим', type: 'select',
              options: ['count', 'time'], default: 'count',
              optionLabels: { count: 'По количеству', time: 'По времени' } },
            { key: 'CountThreshold', label: 'Количество для отправки', type: 'number', default: '10',
              showWhen: 'Mode=count' },
            { key: 'IntervalSeconds', label: 'Интервал (сек)', type: 'number', default: '60',
              showWhen: 'Mode=time' },
            { key: 'MaxBufferSize', label: 'Макс. буфер', type: 'number', default: '1000' },
            { key: 'BatchFormat', label: 'Формат пакета', type: 'select',
              options: ['json', 'csv'], default: 'json' }
        ]
    },
    DatabaseQuery: {
        name: 'Запрос к БД',
        description: 'Выполняет SQL-запрос к базе данных, подставляя данные сканирования в шаблон запроса. Результат заменяет данные скана для последующих действий. Поддерживает MySQL, PostgreSQL и MSSQL.',
        settings: [
            { key: 'ConnectionType', label: 'Тип БД', type: 'select',
              options: ['mysql', 'postgresql', 'mssql'], default: 'mysql' },
            { key: 'ConnectionString', label: 'Строка подключения', type: 'text', default: '' },
            { key: 'QueryTemplate', label: 'SQL запрос', type: 'text', default: '',
              hint: 'Используйте {data} как плейсхолдер для данных сканирования' },
            { key: 'ResultField', label: 'Поле результата', type: 'text', default: '',
              hint: 'Имя колонки. Пусто = первая колонка' },
            { key: 'TimeoutSeconds', label: 'Таймаут (сек)', type: 'number', default: '30' }
        ]
    },
    Pause: {
        name: 'Пауза',
        description: 'Приостанавливает выполнение цепочки действий на указанное время. Полезно для добавления задержки между действиями.',
        settings: [
            { key: 'DelayMs', label: 'Задержка (мс)', type: 'number', default: '1000',
              hint: 'Время ожидания в миллисекундах (от 1 до 60000)' }
        ]
    }
};

const TAG_SOURCES = [
    { value: 'Timestamp', label: 'Время сканирования' },
    { value: 'ScannerName', label: 'Имя сканера' },
    { value: 'RawData', label: 'Исходные данные' },
    { value: 'ParsedData', label: 'Распознанные данные' },
    { value: 'Format', label: 'Формат кода' },
    { value: 'IsValid', label: 'Валидность' },
    { value: 'ContentType', label: 'Тип контента (URL/JSON/WiFi/...)' },
    { value: 'ParsedContent', label: 'Распарсенный контент QR' },
    { value: 'Custom', label: 'Своё значение' }
];
