# Tumlegaarden hjemmeside

Tech demo: ASP.NET Core 10 (Razor Pages) med et 16-bit skrivebordstema og et sikkert login, der kun er åbent for `@tumlegaarden.dk`.
Der er **ingen JavaScript** på sitet – alt kører på serveren, og CSP-headeren forbyder scripts helt.

## Funktioner

- **Kalender** som forside efter login. Begivenheder har dato, tidspunkt (eller hele dagen), navn og brødtekst, ligger i tabellen `Events` og foldes ud ved klik (HTML `<details>`, ingen JavaScript).
- **Disclaimer** ved første besøg: privat side uden kommercielle interesser, kun for inviterede. Skal bekræftes med OK. Indeholder også cookie-informationen.
- **Kun adgang fra Danmark** (IP-filter på alle sider). Se [DEPLOY.md](DEPLOY.md#8a-kun-adgang-fra-danmark).
- **Login** kun for `@tumlegaarden.dk`, kun via invitation, obligatorisk to-faktor.

## Brugerniveauer

| Niveau | Rettigheder |
|---|---|
| **Administrator** | Alt. Inviterer brugere, ændrer niveau, låser, nulstiller to-faktor og sletter. Kan rette og slette alle begivenheder. |
| **Diktator** | Kan oprette begivenheder og rette/slette sine egne. |
| **Børler** | Kan se kalenderen og læse begivenheder. |

Man kan **ikke** oprette sig selv. En konto oprettes kun via et engangs-invitationslink, som en administrator sender fra siden *Brugere*.
Linket udløber efter 72 timer, kan kun bruges én gang, og kun en SHA-256-hash af det gemmes i databasen.
En administrator kan ikke ændre eller slette sig selv, så der altid er mindst én administrator.
Ændringer af niveau, låsning og sletning slår igennem på aktive sessioner inden for et minut.

**Første administrator:** Når databasen er tom ved opstart, sendes en administrator-invitation til adressen i `Bootstrap:AdminEmail`.

## Cookies (GDPR)

Sitet bruger kun nødvendige cookies (login, to-faktor, CSRF-beskyttelse, statusbeskeder og husk-meddelelsen). Den slags kræver ikke samtykke efter cookiebekendtgørelsen,
men brugeren skal informeres. Derfor vises en cookie-meddelelse nederst med link til siden `/Cookies`, som beskriver hver cookie og den persondata der gemmes.
Tilføjer I senere statistik eller andet, der ikke er nødvendigt, skal meddelelsen udvides med et rigtigt til-/fravalg **før** cookies sættes.

## Sikkerhed

| Område | Hvad sitet gør |
|---|---|
| Domæne | Kun `@tumlegaarden.dk` kan oprette konto og logge ind (tjekkes i formularen **og** i en `IUserValidator`, så det også gælder i databasen). Eksakt domæne – ingen subdomæner eller `tumlegaarden.dk.evil.com`. |
| Oprettelse | Kun via engangs-invitation fra en administrator. Linket sendes til adressen og beviser dermed ejerskab af mailen. |
| Adgangskode | Mindst 14 tegn, store og små bogstaver, tal, specialtegn, mindst 6 forskellige tegn. Må ikke indeholde e-mailen, "tumlegaarden" eller almindelige ord/mønstre, og afvises hvis den findes i kendte datalæk (Have I Been Pwned, k-anonymity – adgangskoden forlader aldrig serveren). |
| Hashing | PBKDF2-HMAC-SHA512 med 210.000 iterationer (OWASP-anbefaling). |
| To-faktor | **Obligatorisk** TOTP (authenticator-app). Uden 2FA kan man kun se opsætningssiden. 10 engangs-gendannelseskoder. |
| Brute force | Kontoen låses 15 min efter 5 forkerte forsøg. Maks. 30 login-/oprettelsesforsøg pr. minut pr. IP. |
| Kontoopslag | Login og "glemt kode" svarer ens uanset om kontoen findes. |
| Sessioner | `__Host-`-cookies: `Secure`, `HttpOnly`, `SameSite=Strict`. 30 min. inaktivitet logger ud. Skift af adgangskode logger alle andre sessioner ud. |
| Headers | Streng CSP (ingen scripts), HSTS, `X-Frame-Options: DENY`, `nosniff`, `Referrer-Policy: no-referrer`, `no-store` caching, serverheader fjernet. |
| Øvrigt | CSRF-tokens på alle formularer, ingen open redirects, ingen Google Fonts (pixelfonten ligger lokalt – GDPR). |

### SSO via Simply.com
Simply.com tilbyder **ikke** en SSO-/OpenID Connect-tjeneste, som et website kan logge ind imod – deres mailkonti kan kun bruges til selve mailen.
Derfor har sitet sine egne konti, beskyttet som beskrevet ovenfor. Hvis Tumlegaarden senere får Microsoft 365 eller Google Workspace,
kan der tilføjes rigtig SSO (OpenID Connect) med ganske få linjer i `Program.cs`, begrænset til tumlegaarden.dk-tenanten.

## Browserdemo

`demo/tumlegaarden-demo.html` er en version af sitet, der kører helt i browseren (publiceret som artefakt i Claude), så flowet kan afprøves uden server.
Den har samme regler, roller og tema, plus en indbakke og en authenticator, så man ikke behøver mailserver eller telefon.
Den er kun til at teste. Den rigtige sikkerhed ligger i ASP.NET-projektet.

## Tests

```bash
dotnet test tests/Tumlegaarden.Tests
```

Tester landefilteret (danske og udenlandske adresser, undtagelser, manglende data) og domænekontrollen af e-mails. Kører også ved hvert byg på GitHub.

## Kør lokalt

```bash
cd src/Tumlegaarden.Web
dotnet run --launch-profile https
```

Åbn https://localhost:7195. Lokalt sendes der ingen mails – links skrives i konsollen. Ved første start skrives en invitation til `admin@tumlegaarden.dk` i konsollen; åbn den for at oprette den første administrator.
Databasen er SQLite i `App_Data/` og oprettes automatisk.

### Datamappe
Database-fil, krypteringsnøgler og `appsettings.Production.json` ligger i datamappen: `App:DataPath` hvis sat, ellers mappen
`tumlegaarden-data` ved siden af appens mappe, hvis den findes, ellers `App_Data/` i appen. Den valgte mappe skrives i loggen ved opstart.

### Database
Sitet vælger database ud fra `ConnectionStrings` i denne rækkefølge: `MySql` (bruges hos Simply.com), `SqlServer`, og ellers en SQLite-fil i `App_Data/`.
MySQL-driveren er Oracles `MySql.EntityFrameworkCore`, fordi Pomelo endnu ikke understøtter .NET 10. Testet mod MySQL 8.0.
Får I en SSL-fejl ved forbindelsen, så skift `SslMode=Required` til `SslMode=Preferred`.

## Udgiv til Simply.com

Se **[DEPLOY.md](DEPLOY.md)** for den komplette vejledning.

Kort fortalt: GitHub Actions bygger sitet ved hver push til `main` og lægger det færdige site på grenen `release`, som er repoets standard-gren.
Simply.com's **Importer fra Git** henter `release` ind i `/public_html`. Hemmeligheder og data ligger i `/tumlegaarden-data` ved siden af,
så en import aldrig rører dem. IIS kører appen med shadow copy, så importen kan overskrive filerne, mens sitet kører.

