# PersoNet – model bazy danych (ERD)

Opis struktury bazy PostgreSQL przygotowany tak, żeby czytał go zarówno człowiek, jak i model AI.
Źródło prawdy: `src/Infrastructure/PersoNet.Infrastructure/Persistence/Migrations/PersoNetDbContextModelSnapshot.cs`
(schemat `public`) i `src/Hosts/PersoNet.Identity/Data/Migrations/IdentityDbContextModelSnapshot.cs` (schemat `identity`).
Po każdej migracji zmieniającej tabele zaktualizuj ten plik.

## Zasady obowiązujące wszystkie tabele

- **Multi-tenant.** Każda tabela poza `tenants` ma kolumnę `tenant_id uuid NOT NULL` z FK do `tenants.id`
  i indeksem. Dane jednej firmy (tenanta) nigdy nie mieszają się z innymi.
- **Row-Level Security.** Na każdej tabeli z `tenant_id` działa polityka `tenant_isolation`:
  wiersz jest widoczny tylko, gdy `tenant_id = current_setting('personet.tenant_id')::uuid`.
  Ustawienie wpisuje `TenantSessionInterceptor` przy otwarciu połączenia. EF Core dodatkowo filtruje po tenancie.
- **Klucze.** Wszystkie PK to `id uuid` generowane w aplikacji (nie w bazie).
- **Nazewnictwo.** snake_case: tabele w liczbie mnogiej, `pk_<tabela>`, `fk_<tabela>_<tabela_docelowa>_<kolumna>`,
  `ix_<tabela>_<kolumny>`.
- **Usuwanie.** Wszystkie FK mają `ON DELETE RESTRICT`; nic nie kasuje się kaskadowo.
- **Enumy** są zapisywane jako tekst (nazwa wartości, np. `Approved`), nie jako liczby. Lista wartości niżej.
- **Daty.** `date` = `DateOnly`, `timestamptz` = `DateTimeOffset` (UTC), `timestamp` (bez strefy) = czas lokalny
  z czytnika RCP.

## Diagram

```mermaid
erDiagram
    tenants ||--o{ company_settings : "1 (unikalne)"
    tenants ||--o{ departments : ""
    tenants ||--o{ positions : ""
    tenants ||--o{ employees : ""
    tenants ||--o{ roles : ""

    departments |o--o{ departments : "parent_id (drzewo)"
    departments |o--o{ employees : "department_id"
    positions |o--o{ employees : "position_id"
    departments |o..o{ positions : "department_ids (tablica, bez FK)"
    employees |o--o{ employees : "manager_id (przełożony)"

    employees ||--o{ contracts : "employee_id"
    employees ||--o{ leave_entitlements : "employee_id"
    employees ||--o{ leave_requests : "employee_id"
    employees ||--o{ work_days : "employee_id"
    employees ||--o{ clock_events : "employee_id"
    employees ||--o{ role_assignments : "employee_id"
    roles ||--o{ role_assignments : "role_id"
    employees |o..o{ audit_log : "employee_id (bez FK)"

    tenants ||..o{ AspNetUsers : "tenant_id (schemat identity, bez FK)"
    AspNetUsers |o..o| employees : "email = email (logiczne powiązanie)"
    AspNetUsers |o..o{ leave_requests : "decided_by (bez FK)"
    AspNetUsers |o..o{ audit_log : "user_id (bez FK)"

    tenants {
        uuid id PK
        varchar(200) name "NOT NULL"
        varchar(63) subdomain UK "NOT NULL"
        boolean is_active
    }

    company_settings {
        uuid id PK
        uuid tenant_id FK,UK "jeden wiersz na firmę"
        varchar(200) name "NOT NULL"
        varchar(300) legal_name
        varchar(10) tax_id "NIP"
        varchar(14) regon
        varchar(200) street
        varchar(6) postal_code
        varchar(100) city
        varchar(30) phone
        varchar(254) email
        text[] portal_leave_types "LeaveType[] dostępne w portalu"
        varchar(20) work_time_source "enum WorkTimeSource"
    }

    departments {
        uuid id PK
        uuid tenant_id FK
        uuid parent_id FK "NULL = dział główny"
        varchar(20) code "UK z tenant_id"
        varchar(200) name "NOT NULL"
        boolean is_active
    }

    positions {
        uuid id PK
        uuid tenant_id FK
        varchar(200) name "NOT NULL"
        varchar(200) normalized_name "UK z tenant_id"
        boolean is_active
        uuid[] department_ids "działy stanowiska; puste = każdy dział"
    }

    employees {
        uuid id PK
        uuid tenant_id FK
        varchar(20) number "numer ewidencyjny, UK z tenant_id"
        varchar(100) first_name "NOT NULL"
        varchar(100) last_name "NOT NULL"
        varchar(254) email "UK z tenant_id, gdy NOT NULL"
        varchar(30) phone
        date hire_date "NOT NULL"
        date termination_date "NULL = zatrudniony"
        uuid department_id FK
        uuid position_id FK
        uuid manager_id FK "employees.id"
        varchar(50) clock_id "ID w RCP (Roger), UK z tenant_id, gdy NOT NULL"
        varchar(512) search_text "UPPER(nazwisko imię numer email)"
    }

    contracts {
        uuid id PK
        uuid tenant_id FK
        uuid employee_id FK
        varchar(20) type "enum ContractType"
        date start_date "NOT NULL"
        date end_date "NULL = bezterminowa"
        date terminated_on "rozwiązanie przed end_date"
        numeric(4_3) working_time "etat, np. 1.000 / 0.500; NULL dla umów cywilnych"
    }

    leave_entitlements {
        uuid id PK
        uuid tenant_id FK
        uuid employee_id FK
        int year "UK (tenant_id, employee_id, year)"
        int days "wymiar roczny w dniach"
        int carried_over_days "urlop zaległy"
    }

    leave_requests {
        uuid id PK
        uuid tenant_id FK
        uuid employee_id FK
        varchar(20) type "enum LeaveType"
        varchar(20) status "enum LeaveStatus"
        date start_date "NOT NULL"
        date end_date "NOT NULL"
        int days "dni robocze"
        boolean is_hourly "urlop w godzinach"
        numeric(6_2) hours "godziny pobrane z puli"
        varchar(500) note
        timestamptz created_at
        uuid decided_by "AspNetUsers.id"
        timestamptz decided_at
        varchar(500) decision_note "np. powód odrzucenia"
    }

    work_days {
        uuid id PK
        uuid tenant_id FK
        uuid employee_id FK
        date date "UK (tenant_id, employee_id, date)"
        time start_time
        time end_time
        int break_minutes
        varchar(20) absence "enum AbsenceType, NULL = obecny"
        varchar(20) origin "enum WorkDayOrigin"
        varchar(20) problem "enum ClockProblem"
        varchar(200) note
    }

    clock_events {
        uuid id PK
        uuid tenant_id FK
        uuid employee_id FK
        timestamp occurred_at "czas lokalny z czytnika"
        varchar(10) direction "enum ClockDirection"
        varchar(20) source "enum ClockEventSource"
        varchar(100) device "czytnik / terminal"
        timestamptz recorded_at "kiedy zapisano w PersoNet"
    }

    roles {
        uuid id PK
        uuid tenant_id FK
        varchar(100) name "NOT NULL"
        varchar(100) normalized_name "UK z tenant_id"
        varchar(500) description
        text[] permissions "kody uprawnień"
    }

    role_assignments {
        uuid id PK
        uuid tenant_id FK
        uuid employee_id FK "UK (employee_id, role_id)"
        uuid role_id FK
    }

    audit_log {
        uuid id PK
        uuid tenant_id FK
        timestamptz occurred_at
        varchar(16) action "enum AuditAction"
        varchar(64) entity_type "np. Employee, LeaveRequest"
        uuid entity_id
        uuid employee_id "pracownik, którego dotyczy wpis"
        uuid user_id "AspNetUsers.id"
        varchar(256) user_name
        jsonb changes "pola zmienione: stara i nowa wartość"
        jsonb state "pełny stan po zmianie"
    }

    AspNetUsers {
        uuid id PK
        uuid tenant_id
        varchar user_name UK "znormalizowany"
        varchar email
        varchar display_name
        varchar password_hash
        boolean two_factor_enabled
        timestamptz lockout_end
        int access_failed_count
    }
```

## Tabele – krótki opis

| Tabela | Moduł | Co przechowuje |
|---|---|---|
| `tenants` | Tenancy | Firma (klient SaaS). Jedyna tabela bez `tenant_id` i bez RLS. |
| `company_settings` | Firma | Dane firmy i ustawienia (jeden wiersz na tenanta): NIP, REGON, adres, rodzaje urlopów w portalu, źródło czasu pracy. |
| `departments` | Struktura | Drzewo działów (`parent_id` → ten sam stół). |
| `positions` | Struktura | Słownik stanowisk wspólny dla całej firmy, niezależny od działów. |
| `employees` | Kadry | Kartoteka pracownika; dział, stanowisko i przełożony są przypisane tutaj. |
| `contracts` | Kadry | Umowy pracownika (historia, jeden pracownik ma wiele umów). |
| `leave_entitlements` | Urlopy | Roczny wymiar urlopu pracownika (dni + zaległe). 1 dzień = 8 h. |
| `leave_requests` | Urlopy | Wnioski urlopowe i ich decyzje. |
| `work_days` | Czas pracy | Dzień pracy pracownika (wejście, wyjście, przerwa, nieobecność) – wynik ręczny albo wyliczony z odbić. |
| `clock_events` | Czas pracy | Surowe odbicia RCP (wejście/wyjście), z ręki, z pliku lub z integracji (Roger PR Master). |
| `roles` | Uprawnienia | Role z listą kodów uprawnień. |
| `role_assignments` | Uprawnienia | Przypisanie ról pracownikom (N:M employees ↔ roles). |
| `audit_log` | Audyt | Historia zmian encji (kto, kiedy, co) – zapisywana automatycznie przy `SaveChanges`. |
| `identity.AspNetUsers` | Logowanie | Konta logowania (ASP.NET Identity + OpenIddict) w osobnym schemacie i osobnym `DbContext`. |

## Powiązania, których nie widać w kluczach obcych

- **Konto ↔ pracownik**: `identity.AspNetUsers.email` = `employees.email` w tym samym `tenant_id`
  (porównanie bez wielkości liter). Nie ma kolumny `user_id` w `employees`.
- `leave_requests.decided_by`, `audit_log.user_id` → `identity.AspNetUsers.id` (inny `DbContext`, brak FK).
- `audit_log.entity_type` + `entity_id` → dowolna tabela (polimorficzne odwołanie); `audit_log.employee_id`
  pozwala pokazać całą historię na karcie pracownika.
- `clock_events` → `work_days`: dni z `origin = Clock` są wyliczane z odbić tego samego pracownika i dnia.
- `employees.clock_id` = identyfikator pracownika w systemie RCP; po nim import odbić znajduje pracownika.

## Wartości enumów (zapisywane jako tekst)

| Kolumna | Wartości |
|---|---|
| `contracts.type` (ContractType) | `Probation` (na okres próbny), `FixedTerm` (na czas określony), `Indefinite` (na czas nieokreślony), `Mandate` (zlecenie), `SpecificWork` (o dzieło), `B2B` |
| `leave_requests.type` (LeaveType) | `Annual` (wypoczynkowy), `OnDemand` (na żądanie, max 4 dni), `Occasional` (okolicznościowy), `Unpaid` (bezpłatny), `ChildCare` (opieka nad dzieckiem, art. 188 KP), `ForceMajeure` (siła wyższa, art. 148¹ KP) |
| `leave_requests.status` (LeaveStatus) | `Pending`, `Approved`, `Rejected`, `Cancelled` |
| `company_settings.work_time_source` (WorkTimeSource) | `Manual`, `FileImport`, `RogerPrMaster`, `Api` |
| `work_days.absence` (AbsenceType) | `Sickness` (L4), `CareAllowance` (zasiłek opiekuńczy), `Excused` (usprawiedliwiona), `Unexcused` (nieusprawiedliwiona) |
| `work_days.origin` (WorkDayOrigin) | `Manual`, `Clock` |
| `work_days.problem` (ClockProblem) | `None`, `MissingOut`, `MissingIn`, `TooLong` |
| `clock_events.direction` (ClockDirection) | `In`, `Out` |
| `clock_events.source` (ClockEventSource) | `Manual`, `File`, `Integration` |
| `audit_log.action` (AuditAction) | `Created`, `Changed`, `Deleted` |
| `roles.permissions` (kody) | `organization.manage`, `employees.read`, `employees.manage`, `leave.read`, `leave.manage`, `accounts.manage`, `audit.read`, `roles.manage`, `settings.manage`, `reports.read`, `worktime.read`, `worktime.manage` |

## Indeksy unikalne (reguły biznesowe w bazie)

- `tenants(subdomain)`
- `company_settings(tenant_id)` – jeden wiersz ustawień na firmę
- `departments(tenant_id, code)`, `positions(tenant_id, normalized_name)`, `roles(tenant_id, normalized_name)`
- `employees(tenant_id, number)`; `employees(tenant_id, email) WHERE email IS NOT NULL`;
  `employees(tenant_id, clock_id) WHERE clock_id IS NOT NULL`
- `leave_entitlements(tenant_id, employee_id, year)`
- `work_days(tenant_id, employee_id, date)` – jeden dzień pracy na pracownika i datę
- `clock_events(tenant_id, employee_id, occurred_at, direction)` – ten sam import nie dubluje odbić
- `role_assignments(employee_id, role_id)`

Tabele OpenIddict (`OpenIddictApplications`, `OpenIddictAuthorizations`, `OpenIddictScopes`, `OpenIddictTokens`)
i pozostałe tabele ASP.NET Identity (`AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, …) w schemacie `identity`
są standardowe i pominięte na diagramie.
