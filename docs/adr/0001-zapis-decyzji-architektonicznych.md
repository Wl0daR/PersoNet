# 0001. Zapisujemy decyzje architektoniczne jako ADR

- Status: przyjęta
- Data: 2026-10-13

## Kontekst

Projekt ma być prowadzony jak w software house i rozwijany przez lata. Bez zapisu decyzji nowa osoba w zespole nie wie, dlaczego coś zrobiono tak, a nie inaczej.

## Decyzja

Każdą decyzję, która wpływa na strukturę systemu, wybór technologii lub sposób pracy, zapisujemy w `docs/adr` w formacie: kontekst, decyzja, konsekwencje. ADR przechodzi przez Pull Request jak kod.

## Konsekwencje

- Decyzje są przeglądane i można do nich wrócić.
- Zmiana decyzji wymaga nowego ADR, który wskazuje ten zastępowany.
