# 0004. Własny serwer OIDC z federacją Entra ID

- Status: przyjęta
- Data: 2026-10-13

## Kontekst

Część firm-klientów używa Microsoft Entra ID, część nie. W jednej firmie mogą być osoby z kontem Entra i bez niego.

## Decyzja

- `PersoNet.Identity` to serwer OpenID Connect oparty na OpenIddict i ASP.NET Core Identity. Jest jedynym wystawcą tokenów dla API.
- Entra ID jest zewnętrznym dostawcą tożsamości. Po podaniu e-maila domena decyduje: logowanie przez Microsoft albo konto lokalne z MFA (TOTP).
- Konto powstaje wyłącznie z kartoteki pracownika. Pierwsze logowanie przez Entra wiąże konto po identyfikatorze `oid`.
- Blazor działa jako BFF (tokeny po stronie serwera), WPF używa Authorization Code + PKCE przez przeglądarkę systemową.
- Endpointy API wymagają uprawnień (np. `absences.approve`), nie ról.

## Konsekwencje

- API, WPF i Blazor nie zależą od sposobu logowania.
- Administrator firmy-klienta raz wyraża zgodę na aplikację w swoim Entra, a operator wiąże jego tenant z firmą w PersoNet.
