# Tumlegaarden hjemmeside

Tech demo: ASP.NET Core 10 (Razor Pages) med et 16-bit-tema og et sikkert login, der kun er åbent for `@tumlegaarden.dk`.
Der er **ingen JavaScript** på sitet – alt kører på serveren, og CSP-headeren forbyder scripts helt.

## Sikkerhed

| Område | Hvad sitet gør |
|---|---|
| Domæne | Kun `@tumlegaarden.dk` kan oprette konto og logge ind (tjekkes i formularen **og** i en `IUserValidator`, så det også gælder i databasen). Eksakt domæne – ingen subdomæner eller `tumlegaarden.dk.evil.com`. |
| Ejerskab af mail | Kontoen skal bekræftes via link i mail, før man kan logge ind. |
| Adgangskode | Mindst 14 tegn, store og små bogstaver, tal, specialtegn, mindst 6 forskellige tegn. Må ikke indeholde e-mailen, "tumlegaarden" eller almindelige ord/mønstre, og afvises hvis den findes i kendte datalæk (Have I Been Pwned, k-anonymity – adgangskoden forlader aldrig serveren). |
| Hashing | PBKDF2-HMAC-SHA512 med 210.000 iterationer (OWASP-anbefaling). |
| To-faktor | **Obligatorisk** TOTP (authenticator-app). Uden 2FA kan man kun se opsætningssiden. 10 engangs-gendannelseskoder. |
| Brute force | Kontoen låses 15 min efter 5 forkerte forsøg. Maks. 30 login-/oprettelsesforsøg pr. minut pr. IP. |
| Kontoopslag | Login, oprettelse og "glemt kode" svarer ens uanset om kontoen findes. |
| Sessioner | `__Host-`-cookies: `Secure`, `HttpOnly`, `SameSite=Strict`. 30 min. inaktivitet logger ud. Skift af adgangskode logger alle andre sessioner ud. |
| Headers | Streng CSP (ingen scripts), HSTS, `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy: no-referrer`, `no-store` caching, serverheader fjernet. |
| Øvrigt | CSRF-tokens på alle formularer, ingen open redirects, ingen Google Fonts (pixelfonten ligger lokalt – GDPR). |

### SSO via Simply.com
Simply.com tilbyder **ikke** en SSO-/OpenID Connect-tjeneste, som et website kan logge ind imod – deres mailkonti kan kun bruges til selve mailen.
Derfor har sitet sine egne konti, beskyttet som beskrevet ovenfor. Hvis Tumlegaarden senere får Microsoft 365 eller Google Workspace,
kan der tilføjes rigtig SSO (OpenID Connect) med ganske få linjer i `Program.cs`, begrænset til tumlegaarden.dk-tenanten.

## Kør lokalt

```bash
cd src/Tumlegaarden.Web
dotnet run --launch-profile https
```

Åbn https://localhost:7195. Lokalt sendes der ingen mails – bekræftelseslinks skrives i konsollen.
Databasen er SQLite i `App_Data/` og oprettes automatisk.

## Udgiv til Simply.com

1. **Database:** Opret en MS SQL-database i Simply's kontrolpanel.
2. **Mailkonto:** Opret fx `noreply@tumlegaarden.dk`, som sitet sender bekræftelses- og nulstillingsmails fra.
3. **Konfiguration:** Kopiér `appsettings.Production.example.json` til `appsettings.Production.json` og udfyld forbindelsesstreng og mailpassword.
   Filen er i `.gitignore` og skal aldrig committes.
4. **Byg:**
   ```bash
   dotnet publish src/Tumlegaarden.Web -c Release -o publish
   ```
   Udgivelsen er selvstændig (win-x64), så den virker uanset hvilken .NET-version Simply har installeret.
5. **Upload** indholdet af `publish/` plus din `appsettings.Production.json` til webhotellets rodmappe via FTP.
6. **HTTPS:** Slå gratis SSL-certifikat (Let's Encrypt) til for domænet i Simply's kontrolpanel. Sitet kræver HTTPS.
7. Tabellerne oprettes automatisk ved første besøg. Opret dig selv via "Opret konto".

Hvis du bruger et andet domæne end `tumlegaarden.dk`/`www.tumlegaarden.dk`, så tilføj det i `AllowedHosts` i `appsettings.json`.

### Fejlsøgning
- *HTTP 500.30 / 500.19:* sæt `stdoutLogEnabled="true"` i `web.config` midlertidigt og læs `logs/`.
- Mappen `App_Data/keys` skal være skrivbar (krypteringsnøgler til cookies). Slettes den, bliver alle logget ud.
