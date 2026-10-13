# 0005. Hosting na IIS, Worker jako usługa Windows

- Status: przyjęta
- Data: 2026-10-13

## Kontekst

System działa na własnym serwerze. Aplikacja webowa i API mają działać na IIS, a aplikacja desktopowa jest instalowana na komputerach działu kadr.

## Decyzja

- Windows Server z IIS 10 i ASP.NET Core Hosting Bundle dla .NET 10.
- `PersoNet.Api`, `PersoNet.Identity` i `PersoNet.Web` jako osobne witryny IIS, każda we własnej puli aplikacji ("No Managed Code", hosting in-process).
- W IIS włączona funkcja WebSocket Protocol (wymagana przez Blazor Server).
- Wildcard binding i certyfikat dla subdomen firm.
- Klucze Data Protection trwale zapisane poza pulą aplikacji, żeby recykling nie wylogowywał użytkowników.
- `PersoNet.Worker` jako usługa Windows (`AddWindowsService`), bo IIS usypia i recyklinguje pule.
- Aplikacja WPF dystrybuowana instalatorem (MSIX lub Velopack) z automatyczną aktualizacją.
- .NET Aspire (`PersoNet.AppHost`) służy tylko do lokalnego uruchamiania całości.

## Konsekwencje

- Potok wdrożenia buduje paczki `dotnet publish` i wgrywa je z `app_offline.htm`.
- Health checki `/health/live` i `/health/ready` są dostępne we wszystkich środowiskach dla monitoringu.
