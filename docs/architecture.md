# Architektura PersoNet

Pełny plan architektury (zakres, role, model danych, logowanie, konwencje API, testy, etapy) jest w dokumencie [PersoNet – plan architektury i projektu](https://claude.ai/code/artifact/966e3675-682b-4c86-bf7f-a2414477e839). Decyzje wiążące dla kodu są zapisane w [ADR](adr/README.md).

## Projekty

| Projekt | Rola |
| --- | --- |
| `PersoNet.Domain` | Encje, value objecty, reguły biznesowe. Bez zależności. |
| `PersoNet.Application` | Przypadki użycia (komendy, zapytania), interfejsy dla infrastruktury. |
| `PersoNet.Infrastructure` | EF Core i PostgreSQL, e-mail, pliki. |
| `PersoNet.Api` | ASP.NET Core Web API, jedyne miejsce logiki i uprawnień. |
| `PersoNet.Identity` | Serwer OpenID Connect (konta lokalne i Entra ID). |
| `PersoNet.Worker` | Zadania w tle, usługa Windows. |
| `PersoNet.Contracts` | DTO wspólne dla API i klientów. |
| `PersoNet.ApiClient` | Klient HTTP API dla WPF i Blazor. |
| `PersoNet.Web` | Blazor Web App dla pracowników i kierowników. |
| `PersoNet.Desktop` | Aplikacja WPF dla działu kadr (widoki). |
| `PersoNet.Desktop.Presentation` | ViewModele WPF, bez referencji do WPF. |
| `PersoNet.AppHost`, `PersoNet.ServiceDefaults` | Lokalne uruchamianie (Aspire), telemetria, health checki. |
