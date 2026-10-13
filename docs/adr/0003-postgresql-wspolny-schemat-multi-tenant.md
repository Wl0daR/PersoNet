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
