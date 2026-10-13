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

Aplikacja desktopowa (Windows):

```bash
dotnet run --project src/Clients/PersoNet.Desktop
```

## Budowanie i testy

```bash
dotnet build PersoNet.slnx
dotnet test --solution PersoNet.slnx
dotnet format PersoNet.slnx --verify-no-changes
```

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
