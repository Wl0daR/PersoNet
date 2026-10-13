# 0003. PostgreSQL i wspólny schemat dla wielu firm

- Status: przyjęta
- Data: 2026-10-13

## Kontekst

Jedna instalacja PersoNet obsługuje wiele firm (multi-tenant). Dane kadrowe jednej firmy nie mogą być widoczne dla innej, nawet przy błędzie w kodzie.

## Decyzja

- Baza PostgreSQL, dostęp przez EF Core.
- Wszystkie firmy w jednej bazie i jednym schemacie; każda tabela z danymi firmy ma kolumnę `TenantId` (encje oznaczone `ITenantOwned`).
- Izolacja w dwóch warstwach: globalny filtr zapytań EF Core oraz PostgreSQL Row-Level Security.
- Firma bieżącego użytkownika pochodzi z claimu `tenant_id` w tokenie, nigdy z adresu URL ani nagłówka.
- Klucze główne `Guid` w wersji 7.

## Konsekwencje

- Każdy endpoint ma test integracyjny sprawdzający, że użytkownik firmy A nie widzi danych firmy B.
- Duży klient może później dostać osobną bazę, bo connection string można wybierać per firma.

## Realizacja

- Filtr EF Core: nazwany filtr `Tenant` na wszystkich encjach `ITenantOwned`, a `SaveChanges` odrzuca zapis danych innej firmy.
- Row-Level Security: każde otwarcie połączenia ustawia `personet.tenant_id` (`TenantSessionInterceptor`). Migracja każdej tabeli z danymi firmy wywołuje `EnableTenantIsolation`, która włącza RLS z `FORCE` i polityką `tenant_isolation` dla odczytu i zapisu. Brak firmy na połączeniu oznacza brak wierszy.
- Superużytkownik PostgreSQL (np. `postgres`) omija RLS. Na serwerze aplikacje łączą się więc zwykłą rolą bez `BYPASSRLS`, a migracje uruchamia właściciel bazy. Lokalnie na koncie `postgres` działa tylko filtr EF Core, a RLS sprawdzają testy integracyjne na osobnej roli.
- Zadania obejmujące wiele firm (Worker) dostaną osobny, jawny mechanizm, gdy pojawi się pierwsze takie zadanie.
