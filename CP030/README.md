# Тото Анализатор 6/49

Конзолно .NET 8 приложение за изтегляне и статистически анализ на исторически тиражи на Спорт Тото 6/49.

## Бърз старт

```bash
# Изисква .NET 8 SDK  (https://dot.net)
cd TotoAnalyzer
dotnet restore
dotnet run
```

## Структура на проекта

```
TotoAnalyzer/
├── TotoAnalyzer.csproj   ← .NET 8 project (NuGet: DocumentFormat.OpenXml)
├── Program.cs            ← Интерактивно меню + entry point
├── DataLoader.cs         ← HTTP изтегляне + парсване на TXT и DOCX
├── Statistics.cs         ← LINQ анализи (топ числа, двойки, разпределение)
├── Visualizer.cs         ← ASCII диаграми (bar chart, heat map)
└── Models/
    └── Draw.cs           ← Модел на един тираж
```

## Функционалност

| Опция | Описание | Визуализация |
|-------|----------|--------------|
| [1] | Избор на период (от год. до год.) | — |
| [2] | Топ N най-чести числа | Хоризонтален bar chart (#) |
| [3] | Горещи двойки числа | Класирана листа с мини-bar |
| [4] | Разпределение по десетици + Топлинна карта | Bar chart + 7×7 heat map |

## Технически детайли

- **Платформа:** .NET 8, Console App, top-level statements + async/await  
- **HTTP:** `HttpClient` + `SocketsHttpHandler`; без HtmlAgilityPack  
- **HTML парсване:** `Regex` (без HtmlAgilityPack)  
- **TXT парсване:** `Regex` за дата + числа; опит UTF-8 → fallback CP-1251  
- **DOCX парсване:** `DocumentFormat.OpenXml` (официален Microsoft пакет)  
- **LINQ методи:** `GetTopNumbers`, `GetHotPairs`, `GetDistributionByDecade`  
- **Визуализации:** нормализиран bar chart, 7×7 heat map с `Console.ForegroundColor`  
