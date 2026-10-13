# 0002. Clean Architecture w modularnym monolicie

- Status: przyjęta
- Data: 2026-10-13

## Kontekst

System ma dwa klienty (WPF dla kadr, Blazor dla pracowników i kierowników) i jedno API. Reguły biznesowe nie mogą się rozjechać między klientami. Mikroserwisy na starcie dodałyby złożoność wdrożenia bez zysku.

## Decyzja

- Jedna aplikacja serwerowa (modularny monolit) z warstwami Domain, Application, Infrastructure i hostami (Api, Identity, Worker).
- Zależności wskazują tylko do środka: Domain nie zależy od niczego, Application tylko od Domain i Contracts.
- Logika biznesowa i uprawnienia żyją wyłącznie w API. Klienci korzystają z `PersoNet.Contracts` i `PersoNet.ApiClient`.
- Moduły (struktura organizacyjna, kartoteka, zatrudnienie, nieobecności) to foldery per funkcja wewnątrz warstw, nie osobne projekty.
- ViewModele WPF są w osobnym projekcie bez referencji do WPF.

## Konsekwencje

- Reguły zależności pilnują testy w `PersoNet.Architecture.Tests`.
- Moduł, który urośnie, można wydzielić do własnego projektu bez zmiany kontraktu API.
