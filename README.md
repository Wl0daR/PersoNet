# PersoNet

System kadrowy dla wielu firm: aplikacja desktopowa (WPF) dla działu kadr, aplikacja webowa (Blazor) dla pracowników i kierowników oraz wspólne ASP.NET Core Web API. Stack: .NET 10, PostgreSQL, hosting na IIS.

Architektura: [docs/architecture.md](docs/architecture.md) · Decyzje: [docs/adr](docs/adr/README.md)

## Wymagania

- .NET 10 SDK
- Docker lub Podman (lokalny PostgreSQL uruchamiany przez Aspire)
- Windows do uruchomienia aplikacji WPF (pozostałe projekty budują się i działają na Linux/macOS)

## Uruchomienie lokalne

```bash
dotnet run --project src/Aspire/PersoNet.AppHost
```

AppHost uruchamia API, serwer logowania, aplikację webową, Worker i PostgreSQL oraz otwiera dashboard z logami i trace'ami.

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
