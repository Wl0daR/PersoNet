# PersoNet

System kadrowy dla wielu firm: aplikacja desktopowa (WPF) dla działu kadr, aplikacja webowa (Blazor) dla pracowników i kierowników oraz wspólne ASP.NET Core Web API. Stack: .NET 10, PostgreSQL, hosting na IIS.

Architektura: [docs/architecture.md](docs/architecture.md) · Decyzje: [docs/adr](docs/adr/README.md)

## Wymagania

- .NET 10 SDK
- PostgreSQL zainstalowany lokalnie (np. [instalator dla Windows](https://www.postgresql.org/download/windows/)); Docker nie jest potrzebny
- Windows do uruchomienia aplikacji WPF (pozostałe projekty budują się i działają na Linux/macOS)

## Uruchomienie lokalne

Jednorazowo utwórz bazę `personet` w lokalnym PostgreSQL i zapisz connection string w User Secrets (poza repozytorium):

```bash
dotnet user-secrets set ConnectionStrings:personet "Host=localhost;Port=5432;Database=personet;Username=postgres;Password=<hasło>" --project src/Aspire/PersoNet.AppHost
```

Potem:

```bash
dotnet run --project src/Aspire/PersoNet.AppHost
```

AppHost uruchamia API, serwer logowania, aplikację webową i Worker jako zwykłe procesy (bez kontenerów) i otwiera dashboard z adresami, logami i trace'ami.

Aplikacja desktopowa dla kadr (Windows), przy działającym AppHost:

```bash
dotnet run --project src/Clients/PersoNet.Desktop
```

Przycisk **Zaloguj przez przeglądarkę** otwiera stronę serwera logowania w domyślnej przeglądarce (np. konto demo poniżej), a po zalogowaniu aplikacja ma menu boczne z ekranami **Start**, **Pracownicy**, **Działy** i **Stanowiska** (Ctrl+1…4). Adresy domyślnie wskazują lokalne środowisko (serwer logowania https://localhost:7057, API https://localhost:7016); dla innego serwera ustawia się je w `appsettings.json` obok programu (`Identity:Authority`, `PersoNetApi:BaseAddress`).

W środowisku Development API i serwer logowania przy starcie same stosują brakujące migracje bazy.

### Logowanie

Na razie działa logowanie kontami lokalnymi (Entra ID dojdzie później). W Development przy starcie powstaje firma demonstracyjna i konto:

- e-mail: `admin@demo.personet.local`
- hasło: `Demo!Haslo123`

Otwórz aplikację webową pod adresem https://localhost:7226 (albo kliknij adres zasobu `web` w dashboardzie). Wymaga ona zalogowania i sama przekierowuje na stronę logowania serwera `PersoNet.Identity` (https://localhost:7057). API przyjmuje tylko tokeny wystawione przez ten serwer (`GET /api/v1/me` zwraca zalogowanego użytkownika). Przy pierwszym uruchomieniu zaufaj certyfikatowi deweloperskiemu HTTPS: `dotnet dev-certs https --trust`.

Konto demo ma rolę kadr (`hr_admin`), więc może zmieniać strukturę firmy (działy i stanowiska, `/api/v1/organization/...`) i kartoteki pracowników (`/api/v1/employees`). Dokumentacja API w Development: https://localhost:7016/scalar. Przycisk **Authorize** loguje przez serwer logowania (np. kontem demo), a potem można wywoływać endpointy z dokumentacji.

## Budowanie i testy

```bash
dotnet build PersoNet.slnx
dotnet test --solution PersoNet.slnx
dotnet format PersoNet.slnx --verify-no-changes
```

Testy integracyjne używają lokalnego PostgreSQL i same tworzą swoje bazy (`personet_api_tests`, `personet_infrastructure_tests`). Domyślnie łączą się jako `postgres` z hasłem `postgres`; jeśli masz inne, ustaw zmienną środowiskową, np. w PowerShell:

```powershell
$env:PERSONET_TEST_POSTGRES = "Host=localhost;Port=5432;Username=postgres;Password=<hasło>"
```

Zrzuty ekranów aplikacji desktopowej (wszystkie ekrany w kilku rozmiarach okna, jasny i ciemny motyw, dane demonstracyjne) robi narzędzie `tools/PersoNet.Desktop.Snapshots`; CI uruchamia je na Windows i dołącza obrazy do buildu. Lokalnie (Windows):

```bash
dotnet run --project tools/PersoNet.Desktop.Snapshots -- snapshots
```

## Migracje bazy

```bash
dotnet tool restore
dotnet ef migrations add <Nazwa> --project src/Infrastructure/PersoNet.Infrastructure --output-dir Persistence/Migrations
```

Konta i dane serwera logowania mają osobne migracje (schemat `identity`):

```bash
dotnet ef migrations add <Nazwa> --project src/Hosts/PersoNet.Identity --output-dir Data/Migrations
```

Migracje przechodzą przez Pull Request jak kod. Na serwerze uruchamiane są osobnym krokiem wdrożenia, nie przy starcie aplikacji.

## Struktura

```text
src/
  Core/            Domain, Application
  Infrastructure/  EF Core, integracje
  Hosts/           Api, Identity, Worker
  Shared/          Contracts, ApiClient
  Clients/         Web (Blazor), Desktop (WPF), Desktop.Presentation
  Aspire/          AppHost, ServiceDefaults
tests/             testy jednostkowe, integracyjne i architektury
docs/              architektura i ADR
```

## Zasady pracy

- Praca na krótkich gałęziach (`feature/`, `fix/`, `chore/`), wszystko przez Pull Request do `main`.
- Commity w formacie [Conventional Commits](https://www.conventionalcommits.org/pl/) (`feat(absences): ...`).
- Build traktuje ostrzeżenia jako błędy; styl kodu wymusza `.editorconfig`.
- Kod, nazwy i komentarze po angielsku; dokumentacja i teksty UI po polsku.
